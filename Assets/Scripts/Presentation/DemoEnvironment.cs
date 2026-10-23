using UnityEngine;
using static SiegeDemo.DemoPrimitives;

namespace SiegeDemo
{
    internal static class DemoEnvironment
    {
        internal static Transform Create(Transform parent)
        {
            Transform root = Node(parent, "Foundry backdrop (visual only)", Vector2.zero);
            // Fixed default ground top comes from the integration contract (-3.6 m).
            for (int i = 0; i < 14; i++)
            {
                float y = -7 + i * 2;
                Color color = Color.Lerp(new Color(.2f, .29f, .34f), new Color(.075f, .13f, .19f), i / 13f);
                Draw(root, "Sky gradient", new Vector2(0, y), new Vector2(100, 2.02f), color, -220);
            }
            Draw(root, "Diffuse sun", new Vector2(4.3f, 7.2f), Vector2.one * 5.6f, new Color(.49f, .65f, .69f, .045f), -215, Shape.Disc);
            Draw(root, "Diffuse sun", new Vector2(4.3f, 7.2f), Vector2.one * 3.7f, new Color(.59f, .75f, .75f, .07f), -214, Shape.Disc);
            for (int i = 0; i < 16; i++)
            {
                float x = -29 + i * 4;
                float height = 4 + Mathf.Abs(Mathf.Sin(i * 7.17f)) * 7;
                Color silhouette = i % 2 == 0 ? new Color(.17f, .24f, .29f) : new Color(.19f, .27f, .31f);
                Draw(root, "Distant tower", new Vector2(x, -3.6f + height * .5f), new Vector2(2.1f + (i % 3) * .4f, height), silhouette, -200);
                Draw(root, "Tower cap", new Vector2(x, height - 3.48f), new Vector2(2.45f, .18f), new Color(.22f, .3f, .34f), -199);
                Line(root, "Tower antenna", new Vector2(x + .6f, height - 3.6f), new Vector2(x + .6f, height - 2.3f), .035f, silhouette, -198);
                for (int floor = 0; floor < 5; floor++)
                    Draw(root, "Facade slit", new Vector2(x + .6f, floor * 1.3f - 1.8f), new Vector2(.055f, .52f), new Color(.35f, .47f, .5f, .4f), -197);
                if (i % 3 == 0) Draw(root, "Obstruction lamp", new Vector2(x + .6f, height - 2.3f), Vector2.one * .08f, new Color(.8f, .32f, .17f, .7f), -196, Shape.Disc);
            }
            // Raised transfer gantry and truss provide industrial depth behind the playfield.
            for (int i = -2; i < 5; i++)
            {
                float x = i * 8;
                var dark = new Color(.14f, .21f, .25f);
                Draw(root, "Gantry pylon", new Vector2(x, 1.7f), new Vector2(.3f, 10.3f), dark, -170);
                Line(root, "Pylon brace", new Vector2(x - 1.1f, -3.5f), new Vector2(x + .8f, 6.8f), .075f, dark, -169);
                Draw(root, "Gantry deck", new Vector2(x + 4, 5.2f), new Vector2(8.05f, .28f), dark, -165);
                Line(root, "Gantry rail", new Vector2(x, 5.84f), new Vector2(x + 8, 5.84f), .04f, new Color(.3f, .39f, .41f), -164);
                for (int j = 0; j < 8; j++)
                {
                    float step = x + j;
                    Line(root, "Rail upright", new Vector2(step, 5.3f), new Vector2(step, 5.84f), .035f, dark, -164);
                    Line(root, "Truss", new Vector2(step, 4.92f), new Vector2(step + 1, 4.25f), .055f, dark, -166);
                    if (j % 3 == 0) Draw(root, "Gantry marker", new Vector2(step, 5.14f), new Vector2(.08f, .065f), new Color(.8f, .45f, .23f, .6f), -160);
                }
            }
            for (int i = 0; i < 4; i++)
                Draw(root, "Atmospheric haze", new Vector2(0, -.7f + i * .6f), new Vector2(90, .65f), new Color(.39f, .53f, .55f, .035f), -145);
            // The fascia has no collider and never substitutes for configured ground physics.
            Draw(root, "Ground fascia", new Vector2(0, -5.8f), new Vector2(100, 4.38f), new Color(.09f, .13f, .16f), -90);
            for (int i = -12; i <= 12; i++)
            {
                float x = i * 2.7f;
                Draw(root, "Ground panel", new Vector2(x, -4.18f), new Vector2(2.62f, .85f), new Color(.23f, .28f, .3f), -89, Shape.Plate);
                Draw(root, "Panel inset", new Vector2(x, -4.18f), new Vector2(2.32f, .57f), new Color(.16f, .21f, .24f), -88, Shape.Plate);
                Draw(root, "Ground trim", new Vector2(x, -3.66f), new Vector2(2.64f, .1f), Steel, -87);
                Draw(root, "Panel seam", new Vector2(x + 1.29f, -4.7f), new Vector2(.045f, 1.5f), new Color(.055f, .08f, .1f), -86);
                Draw(root, "Caution dash", new Vector2(x + .8f, -3.84f), new Vector2(.25f, .12f), new Color(.72f, .4f, .18f), -85, Shape.Box, -35);
            }
            return root;
        }
    }
}
