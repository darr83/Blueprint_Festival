using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The things only for a camping field: the shower block, the camp info point with its lockers,
/// and the camp shop. Each is built round its own middle, facing south (+Z).
/// </summary>
public static partial class SiteProps
{
    private static readonly Color ShowerBlue = new("#8FC3DD");
    private static readonly Color ShowerBlueDark = new("#5C93B0");

    /// <summary>True for the camp stalls drawn here.</summary>
    private static bool CampStall(MeshBuilder b, Node3D node, StallKind kind)
    {
        switch (kind)
        {
            case StallKind.ShowerBlock:
                ShowerBlock(b, node);
                return true;
            case StallKind.CampInfo:
                CampInfoTent(b, node);
                return true;
            case StallKind.CampShop:
                CampShopHut(b, node);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Where the people serving stand: an attendant at each end of the showers, two behind the info point and the shop.</summary>
    private static Vector3[]? CampStaff(StallKind kind) => kind switch
    {
        StallKind.ShowerBlock => new[] { new Vector3(-3.97f, 0, 0.5f), new Vector3(3.97f, 0, 0.5f) },
        StallKind.CampInfo => new[] { new Vector3(-0.9f, 0, 0.2f), new Vector3(0.9f, 0, 0.2f) },
        StallKind.CampShop => new[] { new Vector3(-0.7f, 0, -0.2f), new Vector3(0.7f, 0, -0.2f) },
        _ => null,
    };

    /// <summary>A long pale blue cabin with six shower doors along the front and a water tank on the roof.</summary>
    private static void ShowerBlock(MeshBuilder b, Node3D node)
    {
        const int cubicles = 6;
        const float width = 1.25f;
        b.Box(new Vector3(0, 0.08f, 0), new Vector3((cubicles * width) + 0.2f, 0.16f, 2.2f), Charcoal);
        b.Box(new Vector3(0, 1.3f, 0), new Vector3(cubicles * width, 2.3f, 2.0f), ShowerBlue);
        b.Box(new Vector3(0, 2.5f, 0), new Vector3((cubicles * width) + 0.2f, 0.12f, 2.2f), Cream, new Vector3(3, 0, 0));
        for (var i = 0; i < cubicles; i++)
        {
            var x = (i - ((cubicles - 1) / 2f)) * width;
            b.Box(new Vector3(x, 1.15f, 1.01f), new Vector3(0.82f, 1.9f, 0.03f), Cream);
            b.Box(new Vector3(x + 0.3f, 1.12f, 1.04f), new Vector3(0.06f, 0.16f, 0.04f), Black);
            // A little frosted window over each door, and a step up.
            b.Box(new Vector3(x, 2.2f, 1.02f), new Vector3(0.6f, 0.18f, 0.02f), Glass);
            b.Box(new Vector3(x, 0.2f, 1.2f), new Vector3(0.9f, 0.08f, 0.35f), SteelDark);
            if (i < cubicles - 1)
            {
                b.Box(new Vector3(x + (width / 2), 1.3f, 1.01f), new Vector3(0.05f, 2.3f, 0.04f), ShowerBlueDark);
            }
        }

        // A round tank on the roof and a pipe down the side, so the block reads as showers from far off.
        b.Tube(new Vector3(-2.2f, 2.95f, -0.3f), new Vector3(1.0f, 0.8f, 1.0f), Steel);
        b.Tube(new Vector3(-2.2f, 3.37f, -0.3f), new Vector3(1.04f, 0.05f, 1.04f), SteelDark);
        b.Tube(new Vector3((cubicles * width / 2) + 0.08f, 1.4f, -0.5f), new Vector3(0.08f, 2.6f, 0.08f), SteelDark);

        // A big shower head on a pole above the sign.
        var at = new Vector3(2.4f, 2.6f, 0.3f);
        b.Tube(at + new Vector3(0, 0.4f, 0), new Vector3(0.07f, 0.8f, 0.07f), Steel);
        b.Box(at + new Vector3(0.2f, 0.82f, 0), new Vector3(0.5f, 0.07f, 0.07f), Steel);
        b.Cone(at + new Vector3(0.45f, 0.68f, 0), new Vector3(0.42f, 0.24f, 0.42f), Steel, new Vector3(180, 0, 0));
        for (var k = 0; k < 4; k++)
        {
            b.Box(at + new Vector3(0.32f + (k * 0.08f), 0.42f - (k % 2 * 0.08f), 0), new Vector3(0.03f, 0.18f, 0.03f), ShowerBlue);
        }

        b.Box(new Vector3(0, 2.8f, 0.7f), new Vector3(2.6f, 0.44f, 0.05f), ShowerBlueDark);
        Painted(node, "Showers", new Vector3(0, 2.8f, 0.73f), 2.3f, 0.32f, Cream);
    }

    /// <summary>A canvas tent with a counter, a wall of lockers at the back and an "i" on a pole.</summary>
    private static void CampInfoTent(MeshBuilder b, Node3D node)
    {
        const float w = 4.2f, d = 3.4f, eaves = 2.3f;
        Canopy(b, node, w, d, eaves, TrailerGreen, Canvas, "Camp info and lockers", Black, Sunflower);
        Counter(b, w - 0.4f, (d / 2) - 0.4f, Canvas, TrailerGreen);
        b.Box(new Vector3(0, 1.0f, (-d / 2) + 0.25f), new Vector3(w - 0.4f, 2.0f, 0.45f), Charcoal);
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 9; column++)
            {
                var x = (-(w - 0.4f) / 2) + 0.25f + (column * ((w - 0.9f) / 8));
                var y = 0.25f + (row * 0.38f);
                b.Box(new Vector3(x, y, (-d / 2) + 0.48f), new Vector3(0.34f, 0.32f, 0.02f), (row + column) % 4 == 0 ? Sunflower : SteelDark);
            }
        }

        var at = new Vector3((w / 2) + 0.15f, 3.4f, d / 2);
        b.Tube(new Vector3(at.X, at.Y / 2, at.Z), new Vector3(0.07f, at.Y, 0.07f), Steel);
        b.Tube(at + new Vector3(0, 0.4f, 0), new Vector3(0.8f, 0.08f, 0.8f), TrailerGreen, new Vector3(90, 0, 0));
        b.Box(at + new Vector3(0, 0.33f, 0.05f), new Vector3(0.1f, 0.34f, 0.02f), Colors.White);
        b.Box(at + new Vector3(0, 0.6f, 0.05f), new Vector3(0.1f, 0.1f, 0.02f), Colors.White);
    }

    /// <summary>A little wooden hut with a hatch, gas canisters and folding chairs out the front, and a tent on the roof.</summary>
    private static void CampShopHut(MeshBuilder b, Node3D node)
    {
        const float w = 4.0f, d = 2.2f;
        b.Box(new Vector3(0, 1.15f, -0.2f), new Vector3(w, 2.3f, d), Wood);
        b.Box(new Vector3(0, 2.4f, -0.1f), new Vector3(w + 0.3f, 0.12f, d + 0.6f), WoodDark, new Vector3(6, 0, 0));
        b.Box(new Vector3(0, 1.3f, 0.92f), new Vector3(w - 0.8f, 0.9f, 0.03f), Black);
        b.Box(new Vector3(0, 0.85f, 1.05f), new Vector3(w - 0.6f, 0.08f, 0.35f), WoodDark);
        b.Box(new Vector3(0, 2.0f, 0.95f), new Vector3(w - 0.4f, 0.4f, 0.05f), TrailerGreen);
        Painted(node, "Camp shop", new Vector3(0, 2.0f, 0.98f), w - 0.8f, 0.3f, Cream);

        // Gas canisters in a row on the counter, and two folding chairs by the door.
        for (var i = 0; i < 5; i++)
        {
            b.Tube(new Vector3(-1.2f + (i * 0.25f), 1.0f, 1.05f), new Vector3(0.16f, 0.22f, 0.16f), i % 2 == 0 ? Pink : Sky);
        }

        foreach (var x in new[] { 1.1f, 1.6f })
        {
            b.Box(new Vector3(x, 0.45f, 1.5f), new Vector3(0.44f, 0.05f, 0.42f), new Color("#3E7CB1"));
            b.Box(new Vector3(x, 0.75f, 1.32f), new Vector3(0.44f, 0.5f, 0.04f), new Color("#3E7CB1"), new Vector3(-12, 0, 0));
            b.Box(new Vector3(x, 0.22f, 1.5f), new Vector3(0.4f, 0.44f, 0.03f), SteelDark, new Vector3(0, 0, 0));
        }

        // A little orange ridge tent on the roof says what the shop is for.
        var top = new Vector3(0, 2.5f, -0.2f);
        b.Box(top + new Vector3(-0.32f, 0.32f, 0), new Vector3(0.05f, 0.8f, 1.2f), new Color("#F08A4B"), new Vector3(0, 0, -40));
        b.Box(top + new Vector3(0.32f, 0.32f, 0), new Vector3(0.05f, 0.8f, 1.2f), new Color("#F08A4B").Darkened(0.15f), new Vector3(0, 0, 40));
    }
}
