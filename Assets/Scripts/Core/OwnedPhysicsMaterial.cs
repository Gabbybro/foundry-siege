using UnityEngine;

namespace SiegeDemo
{
    public sealed class OwnedPhysicsMaterial : MonoBehaviour
    {
        public PhysicsMaterial2D Material;
        private void OnDestroy()
        {
            if (!Material) return;
            if (Application.isPlaying) Destroy(Material); else DestroyImmediate(Material);
        }
    }
}
