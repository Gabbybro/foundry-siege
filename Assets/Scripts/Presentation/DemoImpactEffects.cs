using System.Collections.Generic;
using UnityEngine;
using static SiegeDemo.DemoPrimitives;

namespace SiegeDemo
{
    // A small managed visual pool. Debris is integrated here, with no rigidbodies/colliders.
    internal sealed class DemoImpactEffects
    {
        private sealed class Particle
        {
            internal SpriteRenderer renderer;
            internal Vector2 velocity;
            internal Color color;
            internal float age, lifetime, spin, size;
            internal bool ring, active;
        }
        private struct DamageLabel { internal Vector2 position; internal string text; internal float age; internal bool destroyed; }
        private const int Capacity = 128;
        private readonly Particle[] particles = new Particle[Capacity];
        private readonly List<DamageLabel> labels = new List<DamageLabel>(16);
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private readonly System.Random random = new System.Random(149);
        private readonly Transform root;
        private int cursor, frame = -1, bursts;

        internal DemoImpactEffects(Transform parent) { root = Node(parent, "Impact feedback (no physics)", Vector2.zero); }
        private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        internal void Play(Vector2 position, float damage, bool destroyed)
        {
            if (!root || !Finite(position.x) || !Finite(position.y) || !Finite(damage) || damage < 0 || (!destroyed && damage <= 0)) return;
            if (frame != Time.frameCount) { seen.Clear(); bursts = 0; frame = Time.frameCount; }
            var key = new Vector3Int(Mathf.RoundToInt(position.x * 20), Mathf.RoundToInt(position.y * 20), destroyed ? 1 : 0);
            if (!seen.Add(key) || bursts++ >= 12) return;
            int count = destroyed ? 14 : 5;
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0, Mathf.PI * 2);
                float speed = Range(destroyed ? 1.2f : .6f, destroyed ? 4 : 2.5f);
                Emit(position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed + Vector2.up * .5f,
                    i % 3 == 0 ? PaleSteel : Orange, Range(.035f, destroyed ? .13f : .08f), Range(.25f, .65f), false);
            }
            Emit(position, Vector2.zero, destroyed ? Orange : Cyan, destroyed ? .65f : .3f, .28f, true);
            if (destroyed)
                labels.RemoveAll(label => label.age < .1f && (label.position - position).sqrMagnitude < .01f);
            if (labels.Count >= 16) labels.RemoveAt(0);
            labels.Add(new DamageLabel { position = position, text = destroyed ? "摧毁" : "−" + damage.ToString("0.#"), destroyed = destroyed });
        }

        private void Emit(Vector2 position, Vector2 velocity, Color color, float size, float lifetime, bool ring)
        {
            int index = cursor;
            cursor = (cursor + 1) % Capacity;
            var particle = particles[index];
            if (particle == null)
            {
                particle = new Particle { renderer = Draw(root, "Visual spark", position, Vector2.one, color, 100) };
                particles[index] = particle;
            }
            particle.renderer.gameObject.SetActive(true);
            particle.renderer.sprite = GetSprite(ring ? Shape.Ring : Shape.Box);
            particle.renderer.transform.position = new Vector3(position.x, position.y, 0);
            particle.renderer.transform.localScale = Vector3.one * size;
            particle.renderer.transform.localRotation = Quaternion.identity;
            particle.renderer.color = color;
            particle.velocity = velocity; particle.color = color; particle.size = size;
            particle.age = 0; particle.lifetime = lifetime; particle.ring = ring;
            particle.spin = Range(-240, 240); particle.active = true;
        }

        internal void Tick(float delta)
        {
            if (!root) return;
            delta = Mathf.Min(delta, .05f);
            foreach (var particle in particles)
            {
                if (particle == null || !particle.active) continue;
                particle.age += delta;
                float t = particle.age / particle.lifetime;
                if (t >= 1) { particle.active = false; particle.renderer.gameObject.SetActive(false); continue; }
                if (particle.ring) particle.renderer.transform.localScale = Vector3.one * particle.size * (1 + t * 2.2f);
                else
                {
                    particle.velocity.y -= 6 * delta;
                    particle.renderer.transform.position += (Vector3)(particle.velocity * delta);
                    particle.renderer.transform.Rotate(0, 0, particle.spin * delta);
                }
                Color color = particle.color; color.a = 1 - t;
                particle.renderer.color = color;
            }
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                var label = labels[i]; label.age += delta;
                if (label.age >= .85f) labels.RemoveAt(i); else labels[i] = label;
            }
        }

        internal void DrawLabels(Camera camera, GUIStyle style)
        {
            if (!camera || Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            foreach (var label in labels)
            {
                Vector3 screen = camera.WorldToScreenPoint(label.position + Vector2.up * (.45f + label.age * .7f));
                float y = Screen.height - screen.y;
                if (screen.z < 0 || y < 110 || y > Screen.height - 115) continue;
                var rect = new Rect(screen.x - 65, y - 14, 130, 30);
                GUI.color = new Color(.02f, .04f, .05f, Mathf.Clamp01((.85f - label.age) * 4));
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), label.text, style);
                Color tint = label.destroyed ? Orange : PaleSteel; tint.a = GUI.color.a;
                GUI.color = tint;
                GUI.Label(rect, label.text, style);
            }
            GUI.color = previous;
        }

        internal void Clear()
        {
            labels.Clear(); seen.Clear(); bursts = 0; frame = -1; cursor = 0;
            foreach (var particle in particles)
            {
                if (particle == null) continue;
                particle.active = false;
                if (particle.renderer) particle.renderer.gameObject.SetActive(false);
            }
        }

        internal void Dispose() { Clear(); DemoPrimitives.Dispose(root ? root.gameObject : null); }
    }
}
