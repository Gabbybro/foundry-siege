using UnityEngine;

namespace SiegeDemo
{
    internal static class PiercingGeometry
    {
        internal static bool TryExit(CombatProjectile projectile, Collider2D selected, Vector2 direction, float maximumThickness, out Vector2 exit)
        {
            exit = default;
            // Every authored part is a BoxCollider2D; unsupported shapes fail without spending.
            var box = selected as BoxCollider2D;
            if (!box) return false;
            Vector2 localPosition = box.transform.InverseTransformPoint(projectile.Body.position);
            Vector2 localDirection = box.transform.InverseTransformVector(direction);
            Vector2 half = box.size * .5f;
            Vector2 minimum = box.offset - half, maximum = box.offset + half;
            if (!Intersect(localPosition, localDirection, minimum, maximum, out float enter, out float leave) || leave < 0 || leave - enter > maximumThickness + .0001f) return false;
            Vector2 scale = box.transform.lossyScale;
            Vector2 padding = new Vector2(projectile.Radius / Mathf.Abs(scale.x), projectile.Radius / Mathf.Abs(scale.y));
            if (!Intersect(localPosition, localDirection, minimum - padding, maximum + padding, out _, out float clearance)) return false;
            float travel = Mathf.Max(0, clearance) + .015f;
            // A sweep prevents crossing the selected wood and accidentally skipping the next wall.
            foreach (var hit in Physics2D.CircleCastAll(projectile.Body.position, projectile.Radius, direction, travel))
            {
                Collider2D collider = hit.collider;
                if (!collider || collider.isTrigger || collider == selected || collider.attachedRigidbody == projectile.Body ||
                    (selected.attachedRigidbody && collider.attachedRigidbody == selected.attachedRigidbody) || Physics2D.GetIgnoreCollision(projectile.Collider, collider)) continue;
                return false;
            }
            exit = projectile.Body.position + direction * travel;
            return true;
        }

        private static bool Intersect(Vector2 origin, Vector2 direction, Vector2 min, Vector2 max, out float enter, out float leave)
        {
            enter = float.NegativeInfinity; leave = float.PositiveInfinity;
            return Slab(origin.x, direction.x, min.x, max.x, ref enter, ref leave) &&
                Slab(origin.y, direction.y, min.y, max.y, ref enter, ref leave) && enter <= leave;
        }
        private static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float leave)
        {
            if (Mathf.Abs(direction) < .000001f) return origin >= min && origin <= max;
            float a = (min - origin) / direction, b = (max - origin) / direction;
            enter = Mathf.Max(enter, Mathf.Min(a, b));
            leave = Mathf.Min(leave, Mathf.Max(a, b));
            return enter <= leave;
        }
    }
}
