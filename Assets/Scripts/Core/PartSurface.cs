using UnityEngine;

namespace SiegeDemo
{
    [DisallowMultipleComponent]
    public sealed class PartSurface : MonoBehaviour
    {
        public PartDefinition Definition { get; private set; }
        public MaterialId Material => Definition == null ? MaterialId.Legacy : Definition.material;
        public bool IsGround => Definition != null && (Definition.isGround || (Definition.material == MaterialId.Legacy && Definition.kind == PartKind.Platform));
        public bool IsIndestructible => Definition != null && (Definition.indestructible || (Definition.material == MaterialId.Legacy && (Definition.isStatic || Definition.kind == PartKind.Platform)));
        public void Initialize(PartDefinition definition) { Definition = definition; }
    }
}
