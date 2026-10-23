using System.Collections.Generic;
using UnityEngine;

namespace SiegeDemo
{
    /// <summary>Smoke evidence only: never stops, removes, or changes the projectile.</summary>
    [DisallowMultipleComponent]
    public sealed class ProjectileProbe : MonoBehaviour
    {
        private int dynamicHits;
        private uint sessionVersion;
        private double countedStep = double.NaN;
        private readonly HashSet<ulong> counted = new HashSet<ulong>();

        public int DynamicHits
        {
            get { RefreshSession(); return dynamicHits; }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            RefreshSession();
            if (collision != null && collision.collider && collision.otherCollider)
            {
                Rigidbody2D other = collision.rigidbody;
                if (other && other.bodyType == RigidbodyType2D.Dynamic)
                {
                    double step = Time.fixedTimeAsDouble;
                    if (countedStep != step) { counted.Clear(); countedStep = step; }
                    if (counted.Add(ImpactCollision.PairKey(collision.collider.GetInstanceID(), collision.otherCollider.GetInstanceID()))) dynamicHits++;
                }
            }
            ImpactCollision.Process(collision);
        }

        private void OnCollisionStay2D(Collision2D collision) => ImpactCollision.Process(collision);

        private void RefreshSession()
        {
            if (sessionVersion == DamageableBody.SessionVersion) return;
            sessionVersion = DamageableBody.SessionVersion;
            dynamicHits = 0;
            counted.Clear();
            countedStep = double.NaN;
        }
    }
}
