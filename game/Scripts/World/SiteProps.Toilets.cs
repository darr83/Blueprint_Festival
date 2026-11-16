using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The newer toilets: a back-to-back block with doors on both sides, a wide accessible cabin
/// with a ramp, and two kinds of urinal (a round stand and a screened trough). Doors and lamps are
/// named like the cabins in a row (Door{i}, Lamp{i}Vacant, Lamp{i}Engaged), so they open and
/// light up the same way.
/// </summary>
public static partial class SiteProps
{
    private static readonly Color AccessBlue = new("#2D6CC0");
    private static readonly Color Porcelain = new("#F4F6F7");

    /// <summary>One cabin, its door and its lamps, built at x along a row whose fronts face +Z.</summary>
    private static void Cabin(MeshBuilder b, Node3D parent, int number, float x, float z, float width = 1.14f, float doorWidth = 0.8f)
    {
        b.Box(new Vector3(x, 0.06f, z), new Vector3(width + 0.06f, 0.12f, 1.25f), Charcoal);
        b.Box(new Vector3(x, 1.2f, z), new Vector3(width, 2.16f, 1.18f), Teal);
        b.Box(new Vector3(x, 2.32f, z), new Vector3(width + 0.06f, 0.1f, 1.24f), Cream, new Vector3(4, 0, 0));
        b.Box(new Vector3(x, 1.12f, z + 0.585f), new Vector3(doorWidth, 1.86f, 0.02f), new Color("#22302E"));
        b.Tube(new Vector3(x - (width * 0.37f), 2.5f, z - 0.45f), new Vector3(0.09f, 0.5f, 0.09f), Black);

        var half = doorWidth / 2f;
        var door = new Node3D { Name = $"Door{number}", Position = new Vector3(x - half, 0, z + 0.6f) };
        var leaf = new MeshBuilder();
        leaf.Box(new Vector3(half, 1.12f, 0.005f), new Vector3(doorWidth, 1.86f, 0.03f), Cream);
        leaf.Box(new Vector3(doorWidth - 0.1f, 1.1f, 0.035f), new Vector3(0.06f, 0.16f, 0.04f), Black);
        door.AddChild(leaf.BuildNode("Leaf"));
        foreach (var (sign, colour) in new[] { ("Vacant", new Color("#1E9E52")), ("Engaged", new Color("#D9483B")) })
        {
            var plate = new MeshBuilder();
            plate.Box(new Vector3(half, 1.72f, 0.03f), new Vector3(Mathf.Min(0.62f, doorWidth - 0.18f), 0.3f, 0.03f), colour);
            var plateNode = plate.BuildNode(sign);
            plateNode.Visible = sign == "Vacant";
            door.AddChild(plateNode);

            var lamp = new MeshBuilder();
            lamp.Tube(new Vector3(x, 2.43f, z - 0.15f), new Vector3(0.2f, 0.1f, 0.2f), Charcoal);
            lamp.Ball(new Vector3(x, 2.58f, z - 0.15f), 0.34f, colour.Lightened(0.15f));
            var lampNode = lamp.BuildNode($"Lamp{number}{sign}");
            lampNode.Visible = sign == "Vacant";
            parent.AddChild(lampNode);
        }

        parent.AddChild(door);
    }

    /// <summary>Twelve cabins in two rows of six, backs together. The back row is turned round, so its doors face north.</summary>
    private static void BackToBack(MeshBuilder b, Node3D node)
    {
        const int each = 6;
        var back = new Node3D { Name = "BackRow", RotationDegrees = new Vector3(0, 180, 0) };
        node.AddChild(back);
        var backShapes = new MeshBuilder();
        for (var i = 0; i < each; i++)
        {
            var x = (i - ((each - 1) / 2f)) * 1.25f;
            Cabin(b, node, i, x, 0.62f);

            // Numbered on from the front row; turned round, the back row's x runs the other way.
            Cabin(backShapes, back, each + i, x, 0.62f);
        }

        back.AddChild(backShapes.BuildNode("Shapes"));

        // One long sign along the ridge, painted on both sides.
        b.Box(new Vector3(0, 2.66f, 0), new Vector3(3.2f, 0.46f, 0.08f), TealDark);
        b.Tube(new Vector3(-1.5f, 2.5f, 0), new Vector3(0.05f, 0.34f, 0.05f), SteelDark);
        b.Tube(new Vector3(1.5f, 2.5f, 0), new Vector3(0.05f, 0.34f, 0.05f), SteelDark);
        Painted(node, "Toilets", new Vector3(0, 2.66f, 0.05f), 2.8f, 0.32f, Cream);
        Painted(back, "Toilets", new Vector3(0, 2.66f, 0.05f), 2.8f, 0.32f, Cream);
    }

    /// <summary>A wide cabin on a low deck, a ramp with rails down to the ground in front, and the wheelchair sign.</summary>
    private static void AccessibleToilet(MeshBuilder b, Node3D node)
    {
        const float deck = 0.22f;
        const float z = -0.55f;
        var cabin = new Node3D { Name = "Cabin", Position = new Vector3(0, deck, 0) };
        node.AddChild(cabin);
        var shapes = new MeshBuilder();
        Cabin(shapes, cabin, 0, 0, z, 2.2f, 1.05f);
        shapes.Box(new Vector3(0, 1.2f, z - 0.75f), new Vector3(2.2f, 2.16f, 0.4f), Teal);
        shapes.Box(new Vector3(0, 2.32f, z - 0.75f), new Vector3(2.26f, 0.1f, 0.42f), Cream);
        cabin.AddChild(shapes.BuildNode("Shapes"));

        // The deck the cabin stands on, and a gentle ramp down to the grass with a rail each side.
        b.Box(new Vector3(0, deck / 2f, z - 0.15f), new Vector3(2.5f, deck, 2.0f), SteelDark);
        b.Box(new Vector3(0, deck / 2f, 0.95f), new Vector3(1.3f, 0.06f, 1.1f), Steel, new Vector3(11, 0, 0));
        foreach (var side in new[] { -0.68f, 0.68f })
        {
            b.Box(new Vector3(side, 0.75f, 0.75f), new Vector3(0.05f, 0.05f, 1.7f), Sunflower, new Vector3(8, 0, 0));
            foreach (var along in new[] { 0.05f, 0.85f, 1.45f })
            {
                b.Tube(new Vector3(side, 0.45f, along), new Vector3(0.05f, 0.75f, 0.05f), SteelDark);
            }
        }

        // The sign: a blue plate on the door and a bigger one on the roof, with the wheelchair figure in white.
        WheelchairSign(b, new Vector3(0.82f, deck + 1.45f, z + 0.6f), 0.34f);
        b.Box(new Vector3(0, deck + 2.72f, z + 0.45f), new Vector3(1.9f, 0.5f, 0.06f), AccessBlue);
        WheelchairSign(b, new Vector3(-0.66f, deck + 2.72f, z + 0.49f), 0.4f);
        Painted(node, "Accessible", new Vector3(0.2f, deck + 2.72f, z + 0.5f), 1.25f, 0.28f, Colors.White);
        b.Tube(new Vector3(-0.8f, deck + 2.42f, z + 0.45f), new Vector3(0.04f, 0.2f, 0.04f), SteelDark);
        b.Tube(new Vector3(0.8f, deck + 2.42f, z + 0.45f), new Vector3(0.04f, 0.2f, 0.04f), SteelDark);
    }

    /// <summary>A square blue plate with a white figure in a wheelchair on it, facing +Z, of the given size.</summary>
    private static void WheelchairSign(MeshBuilder b, Vector3 at, float size)
    {
        var s = size;
        b.Box(at, new Vector3(s, s, 0.03f), AccessBlue);
        var front = at.Z + 0.02f;
        b.Ball(new Vector3(at.X - (0.04f * s), at.Y + (0.33f * s), front), 0.13f * s, Colors.White);
        b.Box(new Vector3(at.X - (0.08f * s), at.Y + (0.08f * s), front), new Vector3(0.09f * s, 0.32f * s, 0.01f), Colors.White);
        b.Box(new Vector3(at.X + (0.07f * s), at.Y - (0.05f * s), front), new Vector3(0.3f * s, 0.08f * s, 0.01f), Colors.White);
        b.Box(new Vector3(at.X + (0.2f * s), at.Y - (0.2f * s), front), new Vector3(0.08f * s, 0.26f * s, 0.01f), Colors.White, new Vector3(0, 0, -20));

        // The wheel: a white ring, drawn as a white disc with a blue one inside it.
        b.Tube(new Vector3(at.X - (0.07f * s), at.Y - (0.17f * s), front - 0.005f), new Vector3(0.5f * s, 0.01f, 0.5f * s), Colors.White, new Vector3(90, 0, 0));
        b.Tube(new Vector3(at.X - (0.07f * s), at.Y - (0.17f * s), front), new Vector3(0.36f * s, 0.012f, 0.36f * s), AccessBlue, new Vector3(90, 0, 0));
    }

    /// <summary>A round open-air urinal: four white bowls back to back round a teal post, screens between them, and a sign on top.</summary>
    private static void UrinalStand(MeshBuilder b, Node3D node)
    {
        b.Tube(new Vector3(0, 0.04f, 0), new Vector3(2.3f, 0.08f, 2.3f), Charcoal);
        b.Tube(new Vector3(0, 0.09f, 0), new Vector3(2.1f, 0.03f, 2.1f), SteelDark);
        b.Tube(new Vector3(0, 0.95f, 0), new Vector3(0.78f, 1.8f, 0.78f), Teal);
        b.Tube(new Vector3(0, 1.88f, 0), new Vector3(0.86f, 0.08f, 0.86f), Cream);
        for (var k = 0; k < 4; k++)
        {
            var angle = Mathf.DegToRad(90f * k);
            var out_ = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var turn = new Vector3(0, 90f * k, 0);

            // A white bowl on a backplate, and a short screen between this place and the next.
            b.Box((out_ * 0.42f) + new Vector3(0, 1.0f, 0), new Vector3(0.44f, 0.6f, 0.06f), Porcelain, turn);
            b.Pill((out_ * 0.5f) + new Vector3(0, 0.72f, 0), new Vector3(0.36f, 0.5f, 0.22f), Porcelain, turn);
            b.Box((out_ * 0.6f) + new Vector3(0, 0.5f, 0), new Vector3(0.2f, 0.05f, 0.12f), SteelDark, turn);
            var between = Mathf.DegToRad((90f * k) + 45f);
            var screen = new Vector3(Mathf.Sin(between), 0, Mathf.Cos(between));
            b.Box((screen * 0.68f) + new Vector3(0, 1.0f, 0), new Vector3(0.05f, 1.2f, 0.52f), TealDark, new Vector3(0, (90f * k) + 45f, 0));
        }

        // A sign on a mast above, readable from all round.
        b.Tube(new Vector3(0, 2.3f, 0), new Vector3(0.08f, 0.8f, 0.08f), SteelDark);
        for (var k = 0; k < 4; k++)
        {
            var face = new Node3D { Name = $"Sign{k}", RotationDegrees = new Vector3(0, 90f * k, 0) };
            node.AddChild(face);
            Painted(face, "Urinals", new Vector3(0, 2.72f, 0.52f), 0.85f, 0.2f, Cream);
        }

        b.Box(new Vector3(0, 2.72f, 0), new Vector3(1.0f, 0.26f, 1.0f), TealDark);
    }

    /// <summary>A long screen with a steel trough along its front and six places marked off by short dividers.</summary>
    private static void UrinalTrough(MeshBuilder b, Node3D node)
    {
        const int places = 6;
        const float wide = 5.6f;
        b.Box(new Vector3(0, 0.04f, -0.1f), new Vector3(wide + 0.3f, 0.08f, 1.35f), Charcoal);
        b.Box(new Vector3(0, 1.0f, -0.55f), new Vector3(wide, 1.9f, 0.1f), Teal);
        b.Box(new Vector3(0, 1.98f, -0.55f), new Vector3(wide + 0.1f, 0.08f, 0.16f), Cream);
        foreach (var end in new[] { -wide / 2f, wide / 2f })
        {
            b.Box(new Vector3(end, 1.0f, -0.18f), new Vector3(0.1f, 1.9f, 0.84f), TealDark);
        }

        // The trough, and a pipe along the top.
        b.Box(new Vector3(0, 0.58f, -0.36f), new Vector3(wide - 0.2f, 0.12f, 0.3f), Steel);
        b.Box(new Vector3(0, 0.66f, -0.22f), new Vector3(wide - 0.2f, 0.06f, 0.04f), SteelDark);
        b.Tube(new Vector3(0, 1.55f, -0.48f), new Vector3(0.06f, wide - 0.3f, 0.06f), SteelDark, new Vector3(0, 0, 90));
        for (var i = 1; i < places; i++)
        {
            var x = (i - (places / 2f)) * (wide / places);
            b.Box(new Vector3(x, 1.0f, -0.32f), new Vector3(0.04f, 0.9f, 0.42f), TealDark);
        }

        b.Box(new Vector3(0, 2.36f, -0.5f), new Vector3(2.4f, 0.42f, 0.06f), TealDark);
        b.Tube(new Vector3(-1.0f, 2.1f, -0.5f), new Vector3(0.04f, 0.2f, 0.04f), SteelDark);
        b.Tube(new Vector3(1.0f, 2.1f, -0.5f), new Vector3(0.04f, 0.2f, 0.04f), SteelDark);
        Painted(node, "Urinals", new Vector3(0, 2.36f, -0.46f), 2.1f, 0.3f, Cream);
    }
}
