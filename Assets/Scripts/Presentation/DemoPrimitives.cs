using UnityEngine;

namespace SiegeDemo
{
    // Shared, bounded procedural assets. No physics components are created here.
    internal static class DemoPrimitives
    {
        internal enum Shape { Box, Disc, Ring, Plate, Triangle }
        private static readonly Sprite[] Sprites = new Sprite[5];
        internal static readonly Color Ink = new Color(.065f, .09f, .115f);
        internal static readonly Color Steel = new Color(.46f, .53f, .56f);
        internal static readonly Color PaleSteel = new Color(.78f, .83f, .81f);
        internal static readonly Color Cyan = new Color(.24f, .91f, .96f);
        internal static readonly Color Orange = new Color(.98f, .48f, .18f);

        internal static Sprite GetSprite(Shape shape)
        {
            int index = (int)shape;
            if (Sprites[index]) return Sprites[index];
            const int resolution = 64;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Demo procedural " + shape,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                float u = (x + .5f) / resolution * 2 - 1;
                float v = (y + .5f) / resolution * 2 - 1;
                float radius = Mathf.Sqrt(u * u + v * v);
                float alpha = 1;
                if (shape == Shape.Disc) alpha = Mathf.Clamp01((1 - radius) * 32);
                if (shape == Shape.Ring) alpha = Mathf.Clamp01((1 - radius) * 32) * Mathf.Clamp01((radius - .79f) * 32);
                if (shape == Shape.Plate) alpha = Mathf.Clamp01((1.76f - Mathf.Abs(u) - Mathf.Abs(v)) * 32);
                if (shape == Shape.Triangle) alpha = u >= -1 && Mathf.Abs(v) < (1 - u) * .5f ? 1 : 0;
                pixels[y * resolution + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, resolution, resolution), new Vector2(.5f, .5f), resolution, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Sprites[index] = sprite;
            return sprite;
        }

        internal static Transform Node(Transform parent, string name, Vector2 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = new Vector3(position.x, position.y, 0);
            return node;
        }

        internal static SpriteRenderer Draw(Transform parent, string name, Vector2 position, Vector2 size, Color color, int order, Shape shape = Shape.Box, float angle = 0)
        {
            var node = Node(parent, name, position);
            node.localScale = new Vector3(size.x, size.y, 1);
            node.localRotation = Quaternion.Euler(0, 0, angle);
            var renderer = node.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(shape);
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        internal static void Line(Transform parent, string name, Vector2 a, Vector2 b, float width, Color color, int order)
        {
            Vector2 difference = b - a;
            Draw(parent, name, (a + b) * .5f, new Vector2(difference.magnitude, width), color, order,
                Shape.Box, Mathf.Atan2(difference.y, difference.x) * Mathf.Rad2Deg);
        }

        internal static Transform Replace(Transform parent, string name)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child.name != name) continue;
                child.gameObject.SetActive(false);
                Dispose(child.gameObject);
            }
            return Node(parent, name, Vector2.zero);
        }

        internal static void Dispose(Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
