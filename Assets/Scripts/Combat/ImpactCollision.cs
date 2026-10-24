using System;
using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    /// <summary>One authoritative path for damageable and projectile callbacks.</summary>
    internal static class ImpactCollision
    {
        private static readonly HashSet<ulong> processed = new HashSet<ulong>();
        private static double processedStep = double.NaN;

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static ulong PairKey(int a, int b)
        {
            uint first = unchecked((uint)a), second = unchecked((uint)b);
            return first < second ? ((ulong)first << 32) | second : ((ulong)second << 32) | first;
        }

        internal static void ResetSession()
        {
            processed.Clear();
            processedStep = double.NaN;
        }

        internal static float CalculateDamage(float impulse, float approachSpeed, float threshold, float multiplier, float hp)
        {
            if (!Finite(impulse) || !Finite(approachSpeed) || !Finite(threshold) || !Finite(multiplier) || !Finite(hp) ||
                approachSpeed < DamageableBody.MinimumApproachSpeed || impulse <= threshold || threshold < 0 || multiplier <= 0 || hp <= 0)
                return 0;
            return (float)Math.Min(hp, ((double)impulse - threshold) * multiplier);
        }

        internal static void Process(Collision2D collision)
        {
            if (collision == null || !collision.enabled || !collision.collider || !collision.otherCollider) return;
            Collider2D a = collision.collider, b = collision.otherCollider;
            if (a.isTrigger || b.isTrigger || (a.attachedRigidbody && a.attachedRigidbody == b.attachedRigidbody)) return;
            DamageableBody first = FindDamageable(a), second = FindDamageable(b);
            CombatProjectile projectileA = a.attachedRigidbody ? a.attachedRigidbody.GetComponent<CombatProjectile>() : null;
            CombatProjectile projectileB = b.attachedRigidbody ? b.attachedRigidbody.GetComponent<CombatProjectile>() : null;
            if (!first && !second && !projectileA && !projectileB) return;

            double step = Time.fixedTimeAsDouble;
            if (processedStep != step) { processed.Clear(); processedStep = step; }
            // Collider pair, rather than rigidbody pair: all contact points in this callback
            // are summed; the reverse callback and ProjectileProbe observe the same pair.
            if (!processed.Add(PairKey(a.GetInstanceID(), b.GetInstanceID()))) return;

            double impulse = 0;
            float approach = 0;
            ContactPoint2D representative = default;
            float largestImpulse = -1;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D point = collision.GetContact(i);
                if (!point.enabled) continue;
                if (point.normalImpulse > largestImpulse) { representative = point; largestImpulse = point.normalImpulse; }
                if (!Finite(point.normalImpulse) || point.normalImpulse <= 0) continue;
                impulse += point.normalImpulse;
                // Contact velocity includes angular motion. Use normal speed only, excluding
                // scraping. Absolute projection is invariant under the two callback orientations;
                // a positive solver normal impulse is additionally required above.
                float speed = Mathf.Abs(Vector2.Dot(point.relativeVelocity, point.normal));
                if (Finite(speed)) approach = Mathf.Max(approach, speed);
            }
            if (largestImpulse < 0) return;
            // Capture geometry/incident motion while a glass or wood collider still exists.
            var planA = projectileA ? projectileA.PrepareContact(b, representative.normal, representative.point) : default;
            var planB = projectileB ? projectileB.PrepareContact(a, representative.normal, representative.point) : default;
            DamageSource fromA = projectileA ? projectileA.Source : new DamageSource { kind = DamageKind.Collision };
            DamageSource fromB = projectileB ? projectileB.Source : new DamageSource { kind = DamageKind.Collision };
            if (impulse > 0 && approach >= DamageableBody.MinimumApproachSpeed) DamageableBody.ImpactEvents++;
            float total = (float)Math.Min(float.MaxValue, impulse);
            // Both bodies receive the same physical impulse with their own material thresholds.
            // This also works when only one side is damageable (the drone has no HP component).
            if (first) first.ApplyImpact(total, approach, fromB);
            if (second && second != first) second.ApplyImpact(total, approach, fromA);
            if (projectileA) projectileA.ResolveContact(planA);
            if (projectileB) projectileB.ResolveContact(planB);
        }

        private static DamageableBody FindDamageable(Collider2D collider)
        {
            Rigidbody2D body = collider.attachedRigidbody;
            return body ? body.GetComponent<DamageableBody>() : collider.GetComponent<DamageableBody>();
        }
    }
}
