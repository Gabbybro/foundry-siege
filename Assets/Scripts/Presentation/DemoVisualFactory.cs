using UnityEngine;
using static SiegeDemo.DemoPrimitives;

namespace SiegeDemo
{
    internal static class DemoVisualFactory
    {
        internal const string VisualName = "__DemoVisual";

        internal static void Part(GameObject go, PartDefinition part)
        {
            if (!go || part == null) return;
            Vector2 size = part.size;
            if (!Finite(size.x) || !Finite(size.y) || size.x <= 0 || size.y <= 0) return;
            Transform root = Replace(go.transform, VisualName);
            if (part.kind == PartKind.Target) { Target(root, size); return; }
            Beam(root, size, part.kind == PartKind.Platform);
        }

        private static void Beam(Transform root, Vector2 size, bool platform)
        {
            bool vertical = size.y > size.x;
            // Orient the artwork along the long axis without changing the physical root.
            if (vertical) root.localRotation = Quaternion.Euler(0, 0, 90);
            float length = vertical ? size.y : size.x;
            float width = vertical ? size.x : size.y;
            Draw(root, "Steel outline", Vector2.zero, new Vector2(length, width), Ink, 10, Shape.Plate);
            Draw(root, "Steel web", Vector2.zero, new Vector2(length * .965f, width * .7f), platform ? new Color(.28f, .34f, .37f) : Steel, 11);
            Draw(root, "Upper flange", new Vector2(0, width * .37f), new Vector2(length * .97f, width * .16f), PaleSteel, 12);
            Draw(root, "Lower flange", new Vector2(0, -width * .38f), new Vector2(length * .97f, width * .14f), new Color(.23f, .29f, .32f), 12);
            int panels = Mathf.Clamp(Mathf.FloorToInt(length / .7f), 1, 18);
            for (int i = 0; i < panels; i++)
            {
                float x = -length * .5f + length * (i + .5f) / panels;
                Draw(root, "Web recess", new Vector2(x, 0), new Vector2(length / panels * .77f, width * .34f), new Color(.23f, .29f, .31f), 12, Shape.Plate);
                Line(root, "Diagonal rib", new Vector2(x - length / panels * .3f, -width * .13f), new Vector2(x + length / panels * .3f, width * .13f), Mathf.Min(.045f, width * .07f), Steel, 13);
            }
            float cap = Mathf.Min(length * .15f, .25f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (length * .5f - cap * .65f);
                Draw(root, "Orange end plate", new Vector2(x, 0), new Vector2(cap, width * .91f), Orange, 14, Shape.Plate);
                for (int bolt = -1; bolt <= 1; bolt += 2)
                {
                    Vector2 p = new Vector2(x, bolt * width * .28f);
                    float radius = Mathf.Min(.075f, width * .16f);
                    Draw(root, "Rivet socket", p, Vector2.one * radius, Ink, 15, Shape.Disc);
                    Draw(root, "Rivet highlight", p + Vector2.up * radius * .1f, Vector2.one * radius * .45f, PaleSteel, 16, Shape.Disc);
                }
            }
            if (!platform) Draw(root, "Identification stripe", new Vector2(-length * .24f, width * .17f), new Vector2(Mathf.Min(.4f, length * .18f), width * .045f), PaleSteel, 15);
        }

        private static void Target(Transform root, Vector2 size)
        {
            // All normalized shapes remain within the configured collision silhouette.
            root.localScale = new Vector3(size.x, size.y, 1);
            Draw(root, "Armored shoulders", new Vector2(0, .08f), new Vector2(.98f, .63f), Ink, 25, Shape.Plate);
            Draw(root, "Left armor", new Vector2(-.29f, .06f), new Vector2(.32f, .55f), Steel, 26, Shape.Plate, -8);
            Draw(root, "Right armor", new Vector2(.29f, .06f), new Vector2(.32f, .55f), Steel, 26, Shape.Plate, 8);
            Draw(root, "Orange breastplate", new Vector2(0, -.05f), new Vector2(.45f, .43f), Orange, 27, Shape.Plate);
            Draw(root, "Head", new Vector2(0, .31f), new Vector2(.47f, .34f), Ink, 28, Shape.Plate);
            Draw(root, "Head armor", new Vector2(0, .37f), new Vector2(.43f, .12f), PaleSteel, 29, Shape.Plate);
            Draw(root, "Red optic socket", new Vector2(0, .26f), new Vector2(.38f, .12f), new Color(.27f, .07f, .07f), 29);
            Draw(root, "Red optic", new Vector2(0, .27f), new Vector2(.3f, .045f), new Color(1, .22f, .15f), 30);
            for (int side = -1; side <= 1; side += 2)
            {
                Draw(root, "Armored leg", new Vector2(side * .25f, -.32f), new Vector2(.29f, .32f), Ink, 28, Shape.Plate, side * -12);
                Draw(root, "Knee plate", new Vector2(side * .23f, -.28f), new Vector2(.16f, .19f), Steel, 29, Shape.Plate);
                Draw(root, "Heavy boot", new Vector2(side * .27f, -.435f), new Vector2(.35f, .115f), PaleSteel, 30, Shape.Plate);
                Draw(root, "Shoulder marker", new Vector2(side * .34f, .16f), new Vector2(.06f, .12f), Orange, 29);
            }
            Draw(root, "Core vent", new Vector2(0, -.075f), new Vector2(.22f, .035f), Ink, 30);
            Draw(root, "Core vent", new Vector2(0, -.14f), new Vector2(.22f, .035f), Ink, 30);
        }

        internal static void Projectile(GameObject go)
        {
            if (!go) return;
            Transform root = Replace(go.transform, VisualName);
            Draw(root, "Drone frame", Vector2.zero, new Vector2(.72f, .43f), Ink, 50, Shape.Plate);
            Draw(root, "Drone shell", new Vector2(.015f, .035f), new Vector2(.57f, .34f), PaleSteel, 51, Shape.Plate);
            Draw(root, "Nose armor", new Vector2(.28f, 0), new Vector2(.2f, .3f), Steel, 52, Shape.Plate, -12);
            Draw(root, "Optic socket", new Vector2(.34f, .005f), new Vector2(.09f, .19f), Ink, 53);
            Draw(root, "Optic", new Vector2(.38f, .005f), new Vector2(.05f, .12f), Cyan, 54);
            Draw(root, "Center stripe", new Vector2(-.07f, .04f), new Vector2(.1f, .33f), Steel, 52, Shape.Plate, -20);
            Draw(root, "Warning chevron", new Vector2(.1f, .08f), new Vector2(.07f, .14f), Orange, 53, Shape.Plate, -20);
            Draw(root, "Engine housing", new Vector2(-.34f, 0), new Vector2(.19f, .28f), Ink, 52, Shape.Plate);
            Transform exhaust = Draw(root, "Cyan thrust", new Vector2(-.5f, 0), new Vector2(.22f, .12f), Cyan, 49, Shape.Triangle).transform;
            exhaust.localRotation = Quaternion.Euler(0, 0, 180);
            for (int side = -1; side <= 1; side += 2)
            {
                Draw(root, "Stabilizer", new Vector2(-.1f, side * .26f), new Vector2(.31f, .105f), Ink, 49, Shape.Plate, side * -22);
                Draw(root, "Stabilizer lamp", new Vector2(-.15f, side * .29f), new Vector2(.1f, .035f), Cyan, 50);
            }
            root.gameObject.AddComponent<DemoDroneVisual>().Configure(go.GetComponent<Rigidbody2D>(), exhaust);
        }

        internal static void Launcher(Vector2 origin, Transform parent)
        {
            if (!parent || !Finite(origin.x) || !Finite(origin.y)) return;
            Transform root = Replace(parent, "__DemoLauncher");
            root.position = new Vector3(origin.x, origin.y, 0);
            Draw(root, "Base outline", new Vector2(0, -1.06f), new Vector2(2.35f, .29f), Ink, -4, Shape.Plate);
            Draw(root, "Base deck", new Vector2(0, -.94f), new Vector2(2.17f, .16f), Steel, -3);
            for (int side = -1; side <= 1; side += 2)
            {
                Draw(root, "Hydraulic leg", new Vector2(side * .68f, -.66f), new Vector2(.2f, .65f), Ink, -3, Shape.Plate, side * -20);
                Draw(root, "Piston", new Vector2(side * .61f, -.51f), new Vector2(.075f, .42f), PaleSteel, -2, Shape.Box, side * -20);
                Draw(root, "Base hazard", new Vector2(side * .8f, -.96f), new Vector2(.18f, .14f), Orange, -1, Shape.Box, -25);
            }
            Draw(root, "Rotator housing", new Vector2(0, -.38f), new Vector2(.77f, .85f), Steel, -2, Shape.Plate);
            Draw(root, "Rotator socket", new Vector2(0, -.18f), Vector2.one * .55f, Ink, -1, Shape.Disc);
            Draw(root, "Rotator rim", new Vector2(0, -.18f), Vector2.one * .39f, Orange, 0, Shape.Ring);
            Transform rail = Node(root, "Aim rail", Vector2.zero);
            Draw(rail, "Rail frame", new Vector2(-.05f, -.19f), new Vector2(1.95f, .26f), Ink, 1, Shape.Plate);
            Draw(rail, "Rail housing", new Vector2(-.08f, -.15f), new Vector2(1.74f, .14f), PaleSteel, 2, Shape.Plate);
            Draw(rail, "Magnetic rail", new Vector2(.05f, -.08f), new Vector2(1.7f, .035f), Cyan, 3);
            Draw(rail, "Breech", new Vector2(-.86f, -.02f), new Vector2(.22f, .55f), Steel, 3, Shape.Plate);
            for (int i = 0; i < 4; i++) Draw(rail, "Charge block", new Vector2(-.63f + i * .13f, -.18f), new Vector2(.065f, .1f), Orange, 4);
            Draw(root, "Platform status", new Vector2(.25f, -.39f), new Vector2(.12f, .045f), Cyan, 4);
            root.gameObject.AddComponent<DemoLauncherVisual>().Configure(rail);
        }
    }
}
