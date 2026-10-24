using System;
using UnityEngine;

namespace SiegeDemo
{
    [DisallowMultipleComponent]
    public sealed class DamageableBody : MonoBehaviour
    {
        public const float MinimumApproachSpeed = 1f;
        private bool initialized, immutable;
        private float impulseThreshold, damageMultiplier;
        private uint initializedSession;

        public float HitPoints { get; private set; }
        public bool IsDestroyed { get; private set; }
        public bool IsTarget { get; private set; }
        public DamageSource LastDamageSource { get; private set; }
        public bool IsIndestructible => immutable;
        public event Action<DamageableBody> Destroyed;
        /// <summary>The float is actual HP removed, excluding overkill.</summary>
        public event Action<DamageableBody, float> Damaged;

        public static int ImpactEvents { get; internal set; }
        public static int DamageEvents { get; private set; }
        public static int DestructionEvents { get; private set; }
        internal static uint SessionVersion { get; private set; }

        public void Initialize(PartDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!ImpactCollision.Finite(definition.hitPoints) || definition.hitPoints <= 0 ||
                !ImpactCollision.Finite(definition.impulseThreshold) || definition.impulseThreshold < 0 ||
                !ImpactCollision.Finite(definition.damageMultiplier) || definition.damageMultiplier < 0 ||
                !Enum.IsDefined(typeof(PartKind), definition.kind))
                throw new ArgumentException("Part HP must be positive; impulse threshold and damage multiplier must be finite and non-negative; kind must be supported.", nameof(definition));
            if (IsDestroyed) throw new InvalidOperationException("A destroyed part must be rebuilt for a new session.");
            HitPoints = definition.hitPoints;
            impulseThreshold = definition.impulseThreshold;
            damageMultiplier = definition.damageMultiplier;
            IsTarget = definition.kind == PartKind.Target;
            immutable = definition.indestructible || (definition.material == MaterialId.Legacy &&
                (definition.isStatic || definition.kind == PartKind.Platform));
            LastDamageSource = default;
            initializedSession = SessionVersion;
            initialized = true;
        }

        public void ApplyImpact(float impulse, float approachSpeed)
            => ApplyImpact(impulse, approachSpeed, new DamageSource { kind = DamageKind.Collision });

        public void ApplyImpact(float impulse, float approachSpeed, DamageSource source)
        {
            if (!initialized || initializedSession != SessionVersion || IsDestroyed || !isActiveAndEnabled || immutable) return;
            float damage = ImpactCollision.CalculateDamage(impulse, approachSpeed, impulseThreshold, damageMultiplier, HitPoints);
            ApplyDamage(damage, source);
        }

        public void ApplyDirectDamage(float amount, DamageSource source)
        {
            if (!initialized || initializedSession != SessionVersion || IsDestroyed || !isActiveAndEnabled || immutable ||
                !ImpactCollision.Finite(amount) || amount <= 0) return;
            ApplyDamage(Mathf.Min(amount, HitPoints), source);
        }

        private void ApplyDamage(float damage, DamageSource source)
        {
            if (damage <= 0) return;
            LastDamageSource = source;
            HitPoints = Mathf.Max(0, HitPoints - damage);
            DamageEvents++;
            if (HitPoints <= 0)
            {
                // Guard and remove support before callbacks, including reentrant event handlers.
                BeginDestruction();
                RaiseDamaged(damage);
                FinishDestruction();
            }
            else RaiseDamaged(damage);
        }

        /// <summary>Boundary elimination; idempotent, and does not create an impact damage event.</summary>
        public void Eliminate()
        {
            if (!initialized || initializedSession != SessionVersion || IsDestroyed) return;
            LastDamageSource = new DamageSource { kind = DamageKind.Boundary };
            BeginDestruction();
            FinishDestruction();
        }

        private void BeginDestruction()
        {
            IsDestroyed = true;
            HitPoints = 0;
            DestructionEvents++;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var body in GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
        }

        private void FinishDestruction()
        {
            var handlers = Destroyed;
            if (handlers != null && initializedSession == SessionVersion)
                foreach (Action<DamageableBody> handler in handlers.GetInvocationList())
                {
                    if (initializedSession != SessionVersion) break;
                    try { handler(this); } catch (Exception ex) { Debug.LogException(ex, this); }
                }
            // Destroy is deferred by Unity, so deactivate now to remove renderers and settlement bodies.
            if (this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private void RaiseDamaged(float damage)
        {
            var handlers = Damaged;
            if (handlers == null) return;
            foreach (Action<DamageableBody, float> handler in handlers.GetInvocationList())
            {
                if (initializedSession != SessionVersion) break;
                try { handler(this, damage); } catch (Exception ex) { Debug.LogException(ex, this); }
            }
        }

        private void OnCollisionEnter2D(Collision2D collision) => ImpactCollision.Process(collision);
        private void OnCollisionStay2D(Collision2D collision) => ImpactCollision.Process(collision);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSession()
        {
            ImpactEvents = DamageEvents = DestructionEvents = 0;
            unchecked { SessionVersion++; }
            ImpactCollision.ResetSession();
        }
    }
}
