using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    [DisallowMultipleComponent]
    public sealed partial class CombatRuntime : MonoBehaviour, ICombatRuntime
    {
        private CombatContext context;
        private BuildLoadout build;
        private readonly List<CombatProjectile> projectiles = new List<CombatProjectile>();
        private readonly List<DamageableBody> subscriptions = new List<DamageableBody>();
        private CombatProjectile original;
        private int nextProjectileId;
        public ShotContext CurrentShot { get; private set; }
        public int ActiveProjectiles
        {
            get { int count = 0; foreach (var p in projectiles) if (Alive(p)) count++; return count; }
        }
        public bool ActiveAvailable => IsCurrentShot && !CurrentShot.activeUsed && build.active != ActiveAbility.None &&
            Alive(original) && original.Kind == ProjectileKind.Original && original.IsAirborne;
        public int PendingEffects
        {
            get { int count = effects.Count; foreach (var p in projectiles) if (Alive(p) && p.IsCountingDown) count++; return count; }
        }
        public bool HasUnsettledEffects
        {
            get
            {
                if (!IsCurrentShot) return false;
                if (PendingEffects > 0 || ActiveProjectiles > 0) return true;
                if (!context.world) return false;
                var thresholds = context.level.settlement;
                foreach (var body in context.world.GetComponentsInChildren<Rigidbody2D>())
                    if (body.simulated && body.bodyType == RigidbodyType2D.Dynamic && !body.IsSleeping() &&
                        (body.linearVelocity.magnitude > thresholds.linearThreshold || Mathf.Abs(body.angularVelocity) > thresholds.angularThreshold)) return true;
                return false;
            }
        }
        private bool IsCurrentShot => context != null && CurrentShot != null && !CurrentShot.ended && CurrentShot.generation == context.generation;
        internal AbilitySettings Settings => context.settings;
        internal bool Has(PassiveAbility ability) => build != null && build.Has(ability);
        internal bool Alive(CombatProjectile p) => p && p.gameObject.activeInHierarchy && !p.IsRetired &&
            IsCurrentShot && p.Shot == CurrentShot && p.Body && p.Body.simulated;

        public void Configure(CombatContext value)
        {
            if (value == null || !value.world || value.level == null || value.settings == null || value.build == null)
                throw new ArgumentException("CombatContext requires world, level, build and ability settings.", nameof(value));
            EndShot();
            CurrentShot = null;
            Unsubscribe();
            context = value;
            build = value.build.Copy();
            nextProjectileId = 0;
            foreach (var body in value.world.GetComponentsInChildren<DamageableBody>())
            {
                subscriptions.Add(body);
                body.Destroyed += OnBodyDestroyed;
            }
        }

        public void BeginShot(Rigidbody2D body, int shotId)
        {
            if (context == null) throw new InvalidOperationException("Configure combat before BeginShot.");
            if (!body || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated)
                throw new ArgumentException("The original must already be a simulated dynamic body.", nameof(body));
            EndShot();
            CurrentShot = new ShotContext { generation = context.generation, shotId = shotId };
            original = Attach(body, ProjectileKind.Original);
        }

        public bool TryUseActive(Vector2 worldTarget, out string reason)
        {
            reason = "当前没有可用的空中主动";
            if (!ActiveAvailable || Time.timeScale <= 0) return false;
            if (!ImpactCollision.Finite(worldTarget.x) || !ImpactCollision.Finite(worldTarget.y)) { reason = "点击方向无效"; return false; }
            if (build.active == ActiveAbility.Dash)
            {
                Vector2 direction = worldTarget - original.Body.position;
                if (direction.sqrMagnitude < .0001f) { reason = "请点击无人机之外的方向"; return false; }
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                if (!CombatRules.InDirectionArc(angle, Settings.dash.centerAngle, Settings.dash.angleWidth))
                { reason = "该方向不在冲刺范围内"; return false; }
                float speed = original.Body.linearVelocity.magnitude + Settings.dash.speedIncrement;
                if (!ImpactCollision.Finite(speed) || speed <= 0) { reason = "当前冲刺速度无效"; return false; }
                CurrentShot.activeUsed = true;
                original.SetForcedVelocity(direction.normalized * speed);
                Emit(CombatEventKind.Dash, original.Body.position, direction.normalized, speed, original.ProjectileId);
            }
            else if (build.active == ActiveAbility.Split)
            {
                CombatProjectile mother = original;
                Vector2 actualVelocity = mother.Body.linearVelocity;
                Vector2 direction = actualVelocity.sqrMagnitude > .000001f ? actualVelocity.normalized : mother.EffectiveDirection;
                Vector2 position = mother.Body.position;
                float mass = mother.Body.mass / 3f, radius = mother.Radius * Settings.split.radiusScale;
                CurrentShot.activeUsed = true;
                mother.Retire(); // Replacement is never a death, stop, or explosion.
                original = null;
                var children = new List<CombatProjectile>(3);
                for (int i = -1; i <= 1; i++)
                    children.Add(CreateProjectile(position, CombatRules.Rotate(direction, i * Settings.split.spreadAngle) * Settings.split.speed,
                        mass, radius, ProjectileKind.Split));
                IgnoreSiblings(children);
                Emit(CombatEventKind.Split, position, direction, 3, mother.ProjectileId);
            }
            else return false;
            reason = string.Empty;
            return true;
        }

        public void EndShot()
        {
            if (CurrentShot != null) CurrentShot.ended = true;
            effects.Clear();
            foreach (var p in projectiles) if (p) p.Retire();
            projectiles.Clear();
            original = null;
            shockVictims.Clear();
        }

        private CombatProjectile Attach(Rigidbody2D body, ProjectileKind kind)
        {
            var p = body.GetComponent<CombatProjectile>();
            if (!p) p = body.gameObject.AddComponent<CombatProjectile>();
            if (!body.GetComponent<ProjectileProbe>()) body.gameObject.AddComponent<ProjectileProbe>();
            p.Initialize(this, CurrentShot, ++nextProjectileId, kind);
            projectiles.Add(p);
            return p;
        }

        private CombatProjectile CreateProjectile(Vector2 position, Vector2 velocity, float mass, float radius, ProjectileKind kind)
        {
            var go = new GameObject(kind + " " + (nextProjectileId + 1));
            go.transform.SetParent(context.world, false);
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.mass = mass;
            body.gravityScale = 1;
            body.linearDamping = 0;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = radius;
            body.linearVelocity = velocity;
            CombatProjectile p = Attach(body, kind);
            try { context.onProjectileCreated?.Invoke(go, kind); } catch (Exception ex) { Debug.LogException(ex, this); }
            return p;
        }

        private static void IgnoreSiblings(List<CombatProjectile> siblings)
        {
            for (int i = 0; i < siblings.Count; i++)
                for (int j = i + 1; j < siblings.Count; j++)
                    if (siblings[i] && siblings[j]) Physics2D.IgnoreCollision(siblings[i].Collider, siblings[j].Collider);
        }

        internal void Emit(CombatEventKind kind, Vector2 position, Vector2 direction, float magnitude, int projectileId)
        {
            if (!IsCurrentShot) return;
            var message = new CombatEvent { kind = kind, generation = CurrentShot.generation, shotId = CurrentShot.shotId,
                projectileId = projectileId, position = position, direction = direction, magnitude = magnitude };
            try { context.onEvent?.Invoke(message); } catch (Exception ex) { Debug.LogException(ex, this); }
        }

        private void Unsubscribe()
        {
            foreach (var body in subscriptions) if (body) body.Destroyed -= OnBodyDestroyed;
            subscriptions.Clear();
        }
        private void OnDestroy() { EndShot(); Unsubscribe(); }
    }
}
