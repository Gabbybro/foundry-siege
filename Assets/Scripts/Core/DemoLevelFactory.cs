using UnityEngine;

namespace SiegeDemo
{
    public static class DemoLevelFactory
    {
        public static void Rebuild(LevelData data, Transform root, bool gameplay)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Object.Destroy(child); else Object.DestroyImmediate(child);
            }
            foreach (var placement in LevelConfiguration.Expand(data))
            {
                var part = System.Array.Find(data.parts, p => p.id == placement.partId);
                var go = new GameObject(placement.id);
                go.transform.SetParent(root, false);
                go.transform.position = placement.position;
                go.transform.rotation = Quaternion.Euler(0, 0, placement.rotation);
                var marker = go.AddComponent<LevelObjectMarker>();
                marker.objectId = placement.id;
                marker.partId = placement.partId;
                go.AddComponent<PartSurface>().Initialize(part);
                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = part.size;
                var material = new PhysicsMaterial2D(part.id) { friction = part.friction, bounciness = part.bounciness };
                collider.sharedMaterial = material;
                go.AddComponent<OwnedPhysicsMaterial>().Material = material;
                if (!part.isStatic)
                {
                    var body = go.AddComponent<Rigidbody2D>();
                    body.simulated = false;
                    body.position = placement.position;
                    body.rotation = placement.rotation;
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0;
                    body.mass = part.mass;
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    body.angularDamping = .12f;
                }
                if (gameplay) go.AddComponent<DamageableBody>().Initialize(part);
                DemoPresentation.DecoratePart(go, part);
            }
            // Register the authored poses before activating CCD/contact simulation.
            Physics2D.SyncTransforms();
            if (gameplay) foreach (var body in root.GetComponentsInChildren<Rigidbody2D>()) body.simulated = true;
        }
    }
}
