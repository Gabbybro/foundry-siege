using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    /// <summary>Explicit development-player fixtures. Does not change the live battle's world.</summary>
    public static class CombatRegression
    {
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("COMBAT_REGRESSION: " + message);
        }
        private static bool Near(float a, float b, float tolerance = .025f) => Mathf.Abs(a - b) <= tolerance;
        private static BuildLoadout Build(ActiveAbility active, params PassiveAbility[] passives) => new BuildLoadout { active = active, passives = passives };

        public static IEnumerator Run(BattleController battle)
        {
            if (!battle) throw new ArgumentNullException(nameof(battle));
            float priorTimeScale = Time.timeScale;
            Time.timeScale = 1;
            try
            {
            // Fixtures lie outside the real level and are not children of battle.World, so
            // the root's boundary recycling and enemy counters never act on these entities.
            using (var f = new Fixture())
            {
                f.Part("wood one", new Vector2(81, 20), new Vector2(.25f, 3), MaterialId.Wood);
                f.Part("wood two", new Vector2(82.5f, 20), new Vector2(.25f, 3), MaterialId.Wood);
                f.Configure(Build(ActiveAbility.Split, PassiveAbility.WoodPiercing, PassiveAbility.GroundBounce, PassiveAbility.StoneRicochet));
                CombatProjectile mother = f.Launch(new Vector2(80, 20), new Vector2(10, 0));
                yield return new WaitForSeconds(.4f);
                Require(mother.WoodPiercingUsed && mother.Body.position.x > 81.2f && mother.Body.position.x < 82.4f, "one wood pierced, second wood blocks the same body");
                float mass = mother.Body.mass;
                Vector2 center = mother.Body.position;
                Vector2 forward = mother.Body.linearVelocity.sqrMagnitude > .000001f ? mother.Body.linearVelocity.normalized : mother.EffectiveDirection;
                Require(f.Runtime.TryUseActive(center, out _), "split click does not steer and can be at the drone center");
                Require(mother.IsRetired && f.Runtime.CurrentShot.activeUsed && f.Runtime.ActiveProjectiles == 3, "mother replaced by three children");
                var children = f.World.GetComponentsInChildren<CombatProjectile>();
                Require(children.Length == 3, "exactly three active split entities");
                float total = 0;
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i]; total += child.Body.mass;
                    Require(child.Kind == ProjectileKind.Split && Near(child.Body.mass, mass / 3, .0001f), "split mass allocation");
                    Require(Near(child.Body.linearVelocity.magnitude, f.Settings.split.speed), "fixed split speed");
                    Require(!child.WoodPiercingUsed && !child.GroundBounceUsed && !child.StoneRicochetUsed, "child opportunities refreshed");
                    Require(Vector2.Distance(center, child.Body.position) < .001f && Vector2.Dot(child.Body.linearVelocity, forward) > 0, "birth does not skip an obstacle");
                    for (int j = 0; j < i; j++) Require(Physics2D.GetIgnoreCollision(child.Collider, children[j].Collider), "siblings ignore collision");
                }
                Require(Near(total, mass, .0001f) && !f.Runtime.ActiveAvailable, "mass conserved, no new active");
                yield return new WaitForSeconds(.35f);
                Require(f.Runtime.CurrentShot.activeUsed, "active remains spent");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("thick wood", new Vector2(82, 20), new Vector2(2, 3), MaterialId.Wood);
                f.Configure(Build(ActiveAbility.None, PassiveAbility.WoodPiercing));
                var p = f.Launch(new Vector2(80, 20), new Vector2(10, 0));
                yield return new WaitForSeconds(.2f);
                Require(!p.WoodPiercingUsed && p.Body.position.x < 81, "too-thick wood does not consume or cross");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Configure(Build(ActiveAbility.Dash, PassiveAbility.ApexDive));
                var p = f.Launch(new Vector2(80, 20), new Vector2(6, 8));
                Require(!f.Runtime.TryUseActive(p.Body.position, out _) && !f.Runtime.CurrentShot.activeUsed, "zero dash direction is free");
                f.Settings.dash.angleWidth = 90;
                Require(!f.Runtime.TryUseActive(p.Body.position + Vector2.left * 10, out _) && !f.Runtime.CurrentShot.activeUsed, "invalid fixed-direction arc is free");
                f.Settings.dash.angleWidth = 360;
                float speed = p.Body.linearVelocity.magnitude;
                Require(f.Runtime.TryUseActive(p.Body.position + Vector2.down * 10, out _), "downward dash accepted");
                Require(Near(p.Body.linearVelocity.magnitude, speed + f.Settings.dash.speedIncrement) && p.Body.linearVelocity.y < 0, "dash uses current actual speed plus increment");
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Require(!p.DiveUsed && !f.Runtime.TryUseActive(p.Body.position + Vector2.right, out _), "forced descent is not apex, active once");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Configure(Build(ActiveAbility.None, PassiveAbility.ApexDive, PassiveAbility.RestExplosion));
                var p = f.Launch(new Vector2(80, 20), new Vector2(2, .4f), 1);
                Require(f.Runtime.HasUnsettledEffects, "airborne low speed cannot settle the shot");
                yield return new WaitForSeconds(.15f);
                Require(p.DiveUsed && p.Body.linearVelocity.y <= -f.Settings.apexDive.downSpeed && !p.IsCountingDown, "natural apex dives once without an air explosion");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Configure(Build(ActiveAbility.Split, PassiveAbility.ApexDive));
                var p = f.Launch(new Vector2(80, 20), new Vector2(10, -1));
                Require(f.Runtime.TryUseActive(p.Body.position, out _), "descending original splits");
                int diving = 0, rising = 0;
                foreach (var child in f.World.GetComponentsInChildren<CombatProjectile>())
                    if (child.DiveUsed) { diving++; Require(child.Body.linearVelocity.y <= -f.Settings.apexDive.downSpeed, "descending child immediately dives"); }
                    else { rising++; Require(child.Body.linearVelocity.y > 0 && Near(child.Body.linearVelocity.magnitude, f.Settings.split.speed), "rising child retains split speed"); }
                Require(diving == 2 && rising == 1, "split refreshes individual dive opportunities");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("ground and stone", new Vector2(80, 18), new Vector2(12, .4f), MaterialId.Stone, ground: true, indestructible: true);
                f.Configure(Build(ActiveAbility.None, PassiveAbility.GroundBounce, PassiveAbility.StoneRicochet));
                var p = f.Launch(new Vector2(80, 19), new Vector2(3, -5));
                yield return new WaitForSeconds(.22f);
                Require(p.GroundBounceUsed && !p.StoneRicochetUsed && p.Body.linearVelocity.y >= f.Settings.groundBounce.minimumUpSpeed - .05f, "ground has priority over stone");
                Require(Near(p.Body.linearVelocity.x, 3 * f.Settings.groundBounce.horizontalRetention), "ground retains configured horizontal speed");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("glancing stone", new Vector2(80, 18), new Vector2(12, .4f), MaterialId.Stone, indestructible: true);
                f.Configure(Build(ActiveAbility.None, PassiveAbility.StoneRicochet));
                var p = f.Launch(new Vector2(79, 19), new Vector2(8, -2));
                yield return new WaitForSeconds(.45f);
                Require(p.StoneRicochetUsed && p.Body.linearVelocity.x > 0 && p.Body.linearVelocity.y >= f.Settings.stoneRicochet.minimumUpSpeed - .05f, "glancing stone rises along the original forward side");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("head-on stone", new Vector2(80, 18), new Vector2(12, .4f), MaterialId.Stone, indestructible: true);
                f.Configure(Build(ActiveAbility.None, PassiveAbility.StoneRicochet));
                var p = f.Launch(new Vector2(80, 19), new Vector2(0, -5));
                yield return new WaitForSeconds(.25f);
                Require(!p.StoneRicochetUsed, "head-on stone does not spend ricochet");
            }
            yield return null;
            using (var f = new Fixture())
            {
                var glass = f.Part("glass left", new Vector2(79, 20), new Vector2(.2f, 2), MaterialId.Glass, hp: 1);
                var glassTwo = f.Part("second glass", new Vector2(75, 24), new Vector2(.2f, 2), MaterialId.Glass, hp: 1);
                DamageSource glassSource = default;
                glass.Destroyed += hit => glassSource = hit.LastDamageSource;
                f.Configure(Build(ActiveAbility.None, PassiveAbility.GlassShards, PassiveAbility.WoodPiercing, PassiveAbility.GroundBounce,
                    PassiveAbility.StoneRicochet, PassiveAbility.ApexDive, PassiveAbility.RestExplosion));
                var p = f.Launch(new Vector2(80, 20), new Vector2(-10, 0));
                yield return new WaitForSeconds(.18f);
                Require(!glass || glass.IsDestroyed, "real projectile collision broke glass");
                Require(glassSource.kind == DamageKind.Collision && glassSource.directProjectile && glassSource.incidentDirection.x < 0 &&
                    glassSource.generation == Fixture.Generation && glassSource.shotId == 1, "collision source preserves incident motion and shot identity");
                Require(f.Runtime.CurrentShot.shardsUsed && f.CreatedShards.Count == 2, "exactly two shards from first direct glass break");
                foreach (var velocity in f.CreatedShards) Require(velocity.x < 0 && Near(velocity.magnitude, f.Settings.shards.speed), "shard fan follows leftward incident velocity");
                foreach (var shard in f.World.GetComponentsInChildren<CombatProjectile>())
                    if (shard.Kind == ProjectileKind.Shard) Require(!shard.DiveUsed && !shard.IsCountingDown && !shard.WoodPiercingUsed, "shards inherit no passives");
                glassTwo.ApplyImpact(50, 10, new DamageSource { kind = DamageKind.Collision, generation = Fixture.Generation, shotId = 1,
                    projectileId = p.ProjectileId, projectileKind = ProjectileKind.Original, directProjectile = true, incidentDirection = Vector2.left });
                yield return null; yield return null;
                Require(f.CreatedShards.Count == 2, "second valid direct break cannot refresh shared shard quota");
            }
            yield return null;
            using (var f = new Fixture())
            {
                var source = f.Part("shock origin", new Vector2(80, 20), new Vector2(.3f, .3f), MaterialId.Metal, target: true, hp: 1);
                var wall = f.Part("snapshot blocker", new Vector2(81, 20), new Vector2(.25f, 3), MaterialId.Wood, hp: 1);
                var behind = f.Part("behind wall", new Vector2(82, 20), new Vector2(.3f, .3f), MaterialId.Metal, target: true, hp: 100, dynamic: true);
                var next = f.Part("second origin", new Vector2(80, 21.8f), new Vector2(.3f, .3f), MaterialId.Metal, target: true, hp: 100);
                var boundary = f.Part("boundary elimination", new Vector2(90, 20), Vector2.one, MaterialId.Metal, target: true, hp: 1);
                f.Configure(Build(ActiveAbility.None, PassiveAbility.DeathShock));
                f.Launch(new Vector2(75, 28), new Vector2(1, 0));
                source.ApplyDirectDamage(2, new DamageSource { kind = DamageKind.Collision });
                yield return null; yield return null;
                Require(!wall || wall.IsDestroyed, "exposed blocking wall receives direct shock damage");
                Require(Near(behind.HitPoints, 100) && behind.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude < .0001f, "trigger-time wall snapshot blocks both damage and impulse");
                next.ApplyDirectDamage(200, new DamageSource { kind = DamageKind.RestExplosion });
                yield return null; yield return null;
                Require(behind.HitPoints < 100 && behind.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude > .0001f, "later shock sees cleared route and damages/pushes survivors");
                int shocks = f.Count(CombatEventKind.DeathShock);
                boundary.Eliminate(); boundary.Eliminate();
                yield return null; yield return null;
                Require(f.Count(CombatEventKind.DeathShock) == shocks, "boundary elimination never makes a death shock");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("rest floor", new Vector2(80, 18), new Vector2(10, .4f), MaterialId.Stone, ground: true, indestructible: true);
                var victim = f.Part("explosion survivor", new Vector2(81, 18.6f), new Vector2(.3f, .3f), MaterialId.Metal, target: true, hp: 100, dynamic: true);
                f.Settings.rest.stableSeconds = .12f; f.Settings.rest.countdownSeconds = .2f;
                f.Configure(Build(ActiveAbility.None, PassiveAbility.RestExplosion));
                var p = f.Launch(new Vector2(80, 18.4f), Vector2.zero, 1);
                yield return new WaitForSeconds(.18f);
                Require(p.IsCountingDown && f.Runtime.PendingEffects > 0, "supported low speed starts pending countdown");
                p.Body.linearVelocity = new Vector2(2, 1);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Require(!p.IsCountingDown, "renewed motion resets countdown");
                p.Body.position = new Vector2(80, 18.4f); p.Body.linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(.5f);
                Require(f.Count(CombatEventKind.RestExplosion) == 1 && (!p || p.IsRetired) && victim.HitPoints < 100, "one rest explosion removes projectile and causes direct damage");
            }
            yield return null;
            using (var f = new Fixture())
            {
                f.Part("cancel floor", new Vector2(80, 18), new Vector2(10, .4f), MaterialId.Stone, ground: true, indestructible: true);
                f.Settings.rest.stableSeconds = .1f; f.Settings.rest.countdownSeconds = .5f;
                f.Configure(Build(ActiveAbility.None, PassiveAbility.RestExplosion));
                var p = f.Launch(new Vector2(80, 18.4f), Vector2.zero, 1);
                yield return new WaitForSeconds(.2f);
                Require(p.IsCountingDown, "cancellation fixture reaches countdown");
                f.Runtime.EndShot(); f.Runtime.EndShot();
                yield return new WaitForSeconds(.65f);
                Require(f.Count(CombatEventKind.RestExplosion) == 0 && f.Runtime.ActiveProjectiles == 0 && f.Runtime.PendingEffects == 0 && !f.Runtime.HasUnsettledEffects, "repeated EndShot cleans without a timeout explosion");
            }
            Debug.Log("COMBAT_REGRESSION_SUCCESS: native collision, active formulas, per-body quotas, apex, ground/stone priority, shards, radial occlusion snapshot, rest reset and EndShot");
            }
            finally { Time.timeScale = priorTimeScale; }
        }

        private sealed class Fixture : IDisposable
        {
            internal readonly GameObject Root;
            internal readonly Transform World;
            internal readonly CombatRuntime Runtime;
            internal readonly AbilitySettings Settings = new AbilitySettings();
            internal readonly List<CombatEvent> Events = new List<CombatEvent>();
            internal readonly List<Vector2> CreatedShards = new List<Vector2>();
            internal const int Generation = 7101;
            internal Fixture()
            {
                Root = new GameObject("Combat regression fixture");
                World = new GameObject("Fixture world").transform; World.SetParent(Root.transform);
                Runtime = Root.AddComponent<CombatRuntime>();
            }
            internal void Configure(BuildLoadout build) => Runtime.Configure(new CombatContext
            {
                world = World, generation = Generation, level = new LevelData(), build = build, settings = Settings,
                onEvent = message => { Require(message.generation == Generation && message.shotId == 1, "event carries generation and shot"); Events.Add(message); },
                onProjectileCreated = (go, kind) => { if (kind == ProjectileKind.Shard) CreatedShards.Add(go.GetComponent<Rigidbody2D>().linearVelocity); }
            });
            internal CombatProjectile Launch(Vector2 position, Vector2 velocity, float gravityScale = 0)
            {
                var go = new GameObject("fixture original"); go.transform.SetParent(World); go.transform.position = position;
                var body = go.AddComponent<Rigidbody2D>(); body.mass = 3; body.gravityScale = gravityScale;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                go.AddComponent<CircleCollider2D>().radius = .2f;
                go.AddComponent<ProjectileProbe>(); body.linearVelocity = velocity;
                Runtime.BeginShot(body, 1);
                return go.GetComponent<CombatProjectile>();
            }
            internal DamageableBody Part(string name, Vector2 position, Vector2 size, MaterialId material,
                bool ground = false, bool indestructible = false, bool target = false, float hp = 1000, bool dynamic = false)
            {
                var go = new GameObject(name); go.transform.SetParent(World); go.transform.position = position;
                go.AddComponent<BoxCollider2D>().size = size;
                if (dynamic) { var body = go.AddComponent<Rigidbody2D>(); body.mass = 1; body.gravityScale = 0; }
                var part = new PartDefinition { id = name, material = material, isGround = ground, indestructible = indestructible,
                    isStatic = !dynamic, kind = target ? PartKind.Target : PartKind.Beam, size = size, hitPoints = hp, impulseThreshold = 0 };
                go.AddComponent<PartSurface>().Initialize(part);
                var health = go.AddComponent<DamageableBody>(); health.Initialize(part); return health;
            }
            internal int Count(CombatEventKind kind) { int count = 0; foreach (var message in Events) if (message.kind == kind) count++; return count; }
            public void Dispose() { if (Root) { Runtime.EndShot(); Root.SetActive(false); UnityEngine.Object.Destroy(Root); } }
        }
    }
}
