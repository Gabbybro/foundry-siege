using UnityEngine;

namespace BirdReplica
{
    // This probe is used by the installation smoke scene, not the final damage system.
    public sealed class BirdImpactProbe : MonoBehaviour
    {
        public int DynamicHits { get; private set; }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.rigidbody != null && collision.rigidbody.bodyType == RigidbodyType2D.Dynamic)
                DynamicHits++;
        }
    }
}
