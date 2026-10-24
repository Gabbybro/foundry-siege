using System;

namespace SiegeDemo
{
    public static class MaterialDefaults
    {
        // Per-piece authoring presets, never inferred from volume or Transform scale.
        // Identity, dimensions, role and all three flags deliberately survive a switch.
        public static void Apply(PartDefinition part, MaterialId material)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            switch (material)
            {
                case MaterialId.Wood: Set(part, 1.4f, 8, 1.5f, 1, .7f, 0); break;
                case MaterialId.Glass: Set(part, .65f, 3, .7f, 1.35f, .45f, 0); break;
                case MaterialId.Stone: Set(part, 5, 28, 3, .65f, .8f, 0); break;
                case MaterialId.Metal: Set(part, 3, 16, 3, .75f, .65f, 0); break;
                default: throw new ArgumentOutOfRangeException(nameof(material), "材质预设只支持 Wood/Glass/Stone/Metal；Legacy 只兼容旧配置。");
            }
            part.material = material;
        }
        private static void Set(PartDefinition part, float mass, float hp, float threshold, float multiplier, float friction, float bounce)
        {
            part.mass = mass; part.hitPoints = hp; part.impulseThreshold = threshold;
            part.damageMultiplier = multiplier; part.friction = friction; part.bounciness = bounce;
        }
    }
}
