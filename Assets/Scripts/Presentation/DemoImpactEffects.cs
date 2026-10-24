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
            internal bool ring, active, gravity;
        }
        private struct DamageLabel { internal Vector2 position; internal string text; internal float age; internal Color tint; }
        private const int Capacity = 128;
        private readonly Particle[] particles = new Particle[Capacity];
        private readonly List<DamageLabel> labels = new List<DamageLabel>(16);
        private readonly HashSet<Vector3Int> seen = new HashSet<Vector3Int>();
        private readonly Dictionary<long, float> countdownTimes = new Dictionary<long, float>();
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
            labels.Add(new DamageLabel { position = position, text = destroyed ? "摧毁" : "−" + damage.ToString("0.#"), tint = destroyed ? Orange : PaleSteel });
        }

        internal void PlayAbility(CombatEvent feedback)
        {
            Vector2 position = feedback.position;
            if (!root || !Finite(position.x) || !Finite(position.y)) return;
            if (frame != Time.frameCount) { seen.Clear(); bursts = 0; frame = Time.frameCount; }
            if (!seen.Add(new Vector3Int(Mathf.RoundToInt(position.x * 20), Mathf.RoundToInt(position.y * 20), 10 + (int)feedback.kind)) || bursts++ >= 12) return;
            if (feedback.kind == CombatEventKind.RestCountdown)
            {
                long key = unchecked(((long)feedback.generation << 40) ^ ((long)feedback.shotId << 24) ^ (uint)feedback.projectileId);
                if (countdownTimes.TryGetValue(key, out float next) && Time.time < next) return;
                if (countdownTimes.Count >= 64) countdownTimes.Clear();
                countdownTimes[key] = Time.time + .4f;
            }
            Vector2 direction = feedback.direction;
            direction = Finite(direction.x) && Finite(direction.y) && direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.right;
            Color tint = Cyan;
            string label;
            switch (feedback.kind)
            {
                case CombatEventKind.Split:
                    label = "空中分裂";
                    for (int i = -1; i <= 1; i++) Ray(position, Rotate(direction, i * 23), tint, .9f);
                    break;
                case CombatEventKind.Dash:
                    label = "空中冲刺"; tint = Orange;
                    for (int i = 0; i < 3; i++) Ray(position - direction * i * .28f, -direction, tint, .55f + i * .12f);
                    break;
                case CombatEventKind.WoodPiercing:
                    label = "穿木"; tint = new Color(.9f, .65f, .32f);
                    Ray(position, direction, tint, .8f); Spray(position, tint, 6);
                    break;
                case CombatEventKind.GroundBounce:
                    label = "触地反弹";
                    Ray(position, new Vector2(-.45f, .8f), tint, .7f); Ray(position, new Vector2(.45f, .8f), tint, .7f);
                    break;
                case CombatEventKind.StoneRicochet:
                    label = "石面跳弹"; tint = PaleSteel;
                    Ray(position, direction, tint, 1); Spray(position, tint, 5);
                    break;
                case CombatEventKind.ApexDive:
                    label = "顶点俯冲"; tint = Orange;
                    for (int i = -1; i <= 1; i++) Ray(position + Vector2.right * i * .12f, Vector2.down, tint, .8f - Mathf.Abs(i) * .2f);
                    break;
                case CombatEventKind.GlassShards:
                    label = "破片扩散"; tint = new Color(.5f, .96f, 1);
                    Ray(position, Rotate(direction, 18), tint, .85f); Ray(position, Rotate(direction, -18), tint, .85f);
                    Spray(position, tint, 7);
                    break;
                case CombatEventKind.RestCountdown:
                    label = "停驻计时"; tint = Orange;
                    break;
                case CombatEventKind.RestExplosion:
                    label = "停驻爆破"; tint = Orange;
                    Emit(position, Vector2.zero, tint, 1.4f, .5f, true); Spray(position, tint, 15);
                    break;
                case CombatEventKind.DeathShock:
                    label = "连锁冲击"; tint = new Color(.72f, .57f, 1);
                    Emit(position, Vector2.zero, tint, 1.3f, .48f, true); Spray(position, tint, 10);
                    break;
                default: return;
            }
            Emit(position, Vector2.zero, tint, .36f, .3f, true);
            if (labels.Count >= 16) labels.RemoveAt(0);
            labels.Add(new DamageLabel { position = position, text = label, tint = tint });
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(direction.x * Mathf.Cos(angle) - direction.y * Mathf.Sin(angle), direction.x * Mathf.Sin(angle) + direction.y * Mathf.Cos(angle));
        }

        private void Ray(Vector2 position, Vector2 direction, Color tint, float length)
        {
            direction.Normalize();
            var particle = Emit(position + direction * length * .5f, Vector2.zero, tint, 1, .22f, false);
            particle.gravity = false; particle.spin = 0;
            particle.renderer.transform.localScale = new Vector3(length, .04f, 1);
            particle.renderer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void Spray(Vector2 position, Color tint, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Range(0, Mathf.PI * 2);
                Emit(position, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(.9f, 3.6f), tint, Range(.03f, .08f), Range(.2f, .6f), false);
            }
        }

        private Particle Emit(Vector2 position, Vector2 velocity, Color color, float size, float lifetime, bool ring)
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
            particle.gravity = true;
            return particle;
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
                    if (particle.gravity) particle.velocity.y -= 6 * delta;
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
                Color tint = label.tint; tint.a = GUI.color.a;
                GUI.color = tint;
                GUI.Label(rect, label.text, style);
            }
            GUI.color = previous;
        }

        internal void Clear()
        {
            labels.Clear(); seen.Clear(); countdownTimes.Clear(); bursts = 0; frame = -1; cursor = 0;
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
