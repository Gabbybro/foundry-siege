using UnityEngine;

namespace SiegeDemo
{
    /// <summary>Deterministic, stateless calculations shared by runtime and regression checks.</summary>
    public static class CombatRules
    {
        public static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad, c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(vector.x * c - vector.y * s, vector.x * s + vector.y * c);
        }
        public static bool InDirectionArc(float angle, float center, float width) =>
            ImpactCollision.Finite(angle) && ImpactCollision.Finite(center) && ImpactCollision.Finite(width) && width >= 0 &&
            (width >= 360 || Mathf.Abs(Mathf.DeltaAngle(center, angle)) <= width * .5f + .0001f);
        public static float RadialFalloff(float distance, RadialEffectSettings settings) =>
            settings.radius <= 0 ? 0 : Mathf.Pow(Mathf.Clamp01(1 - distance / settings.radius), settings.falloffPower);
    }
}
