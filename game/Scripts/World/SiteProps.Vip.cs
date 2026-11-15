using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The VIP things: the lounge, the viewing platform, the sign that stands outside a stall kept for
/// VIP guests, and the look of VIP ground. Black and gold, so VIP things read as one set.
/// </summary>
public static partial class SiteProps
{
    public static readonly Color VipGold = new("#E2B13C");
    public static readonly Color VipInk = new("#1E1E24");
    private static readonly Color Velvet = new("#4B2766");
    private static readonly Color VelvetDark = new("#3A1D50");
    private static readonly Color Leaf = new("#3F8F4E");

    /// <summary>True for the VIP buildings drawn here.</summary>
    private static bool Vip(MeshBuilder b, Node3D node, StallKind kind)
    {
        switch (kind)
        {
            case StallKind.VipLounge:
                VipLounge(b, node);
                return true;
            case StallKind.VipPlatform:
                VipPlatform(b, node);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The lounge, 8 m by 6 m: a white marquee open at the front, a purple carpet, a bar across the
    /// back, sofas down both sides, and a black and gold sign over the way in.
    /// </summary>
    private static void VipLounge(MeshBuilder b, Node3D node)
    {
        const float w = 8f;
        const float d = 6f;
        const float eaves = 2.5f;

        // Carpet, a step up off the grass.
        b.Box(new Vector3(0, 0.03f, 0), new Vector3(w - 0.2f, 0.06f, d - 0.2f), Velvet);
        b.Box(new Vector3(0, 0.035f, (d / 2) - 0.25f), new Vector3(w - 0.2f, 0.065f, 0.12f), VipGold);

        // Posts, walls at the back and sides, and a peaked roof.
        foreach (var x in new[] { -w / 2, 0, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.09f, eaves, 0.09f), Colors.White);
            }
        }

        b.Box(new Vector3(0, eaves / 2, -d / 2), new Vector3(w, eaves, 0.06f), Canvas);
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            // Open sides above a low white wall with a gold rail, so you can see in.
            b.Box(new Vector3(x, 0.5f, 0), new Vector3(0.08f, 1.0f, d), Canvas);
            b.Box(new Vector3(x, 1.02f, 0), new Vector3(0.12f, 0.06f, d), VipGold);
        }

        var slope = Mathf.RadToDeg(Mathf.Atan2(0.9f, w / 2));
        var half = Mathf.Sqrt((w / 2 * w / 2) + 0.81f);
        foreach (var side in new[] { -1f, 1f })
        {
            b.Box(new Vector3(side * w / 4, eaves + 0.45f, 0), new Vector3(half + 0.1f, 0.06f, d + 0.3f), Colors.White, new Vector3(0, 0, -side * slope));
        }

        b.Box(new Vector3(0, eaves + 0.92f, 0), new Vector3(0.14f, 0.1f, d + 0.32f), VipGold);

        // Gold scallops along the front edge, and the sign in the middle.
        for (var i = 0; i < 16; i++)
        {
            var x = (-w / 2) + ((i + 0.5f) * (w / 16));
            b.Ball(new Vector3(x, eaves - 0.04f, (d / 2) + 0.05f), new Vector3(w / 16, 0.22f, 0.04f), VipGold);
        }

        b.Box(new Vector3(0, eaves + 0.25f, (d / 2) + 0.12f), new Vector3(2.6f, 0.62f, 0.07f), VipInk);
        b.Box(new Vector3(0, eaves + 0.25f, (d / 2) + 0.1f), new Vector3(2.75f, 0.74f, 0.05f), VipGold);
        Painted(node, "VIP Lounge", new Vector3(0, eaves + 0.25f, (d / 2) + 0.16f), 2.3f, 0.42f, VipGold);

        // The bar across the back, with bottles behind it.
        var barZ = (-d / 2) + 1.3f;
        b.Box(new Vector3(0, 1.05f, barZ), new Vector3(4.6f, 0.08f, 0.6f), VipGold);
        b.Box(new Vector3(0, 0.52f, barZ + 0.22f), new Vector3(4.5f, 1.0f, 0.08f), VipInk);
        b.Box(new Vector3(0, 0.75f, barZ + 0.27f), new Vector3(4.5f, 0.05f, 0.02f), VipGold);
        b.Box(new Vector3(0, 1.4f, (-d / 2) + 0.2f), new Vector3(4.4f, 0.05f, 0.3f), WoodDark);
        for (var i = 0; i < 14; i++)
        {
            var x = -2.0f + (i * 0.31f);
            var colour = i % 3 == 0 ? new Color("#3F7F4A") : i % 3 == 1 ? new Color("#B8D8E8") : new Color("#C77A2F");
            b.Tube(new Vector3(x, 1.55f, (-d / 2) + 0.2f), new Vector3(0.09f, 0.26f, 0.09f), colour);
        }

        // A sofa and a low table down each side, facing in.
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * ((w / 2) - 0.65f);
            b.Box(new Vector3(x, 0.3f, 0.6f), new Vector3(0.8f, 0.36f, 2.4f), Velvet);
            b.Box(new Vector3(x + (side * 0.32f), 0.62f, 0.6f), new Vector3(0.2f, 0.6f, 2.4f), VelvetDark);
            foreach (var z in new[] { -0.55f, 1.75f })
            {
                b.Box(new Vector3(x, 0.45f, z), new Vector3(0.82f, 0.3f, 0.18f), VelvetDark);
            }

            var tx = side * ((w / 2) - 1.65f);
            b.Tube(new Vector3(tx, 0.22f, 0.6f), new Vector3(0.7f, 0.04f, 0.7f), VipGold);
            b.Tube(new Vector3(tx, 0.11f, 0.6f), new Vector3(0.1f, 0.22f, 0.1f), VipInk);
            b.Tube(new Vector3(tx + 0.1f, 0.3f, 0.5f), new Vector3(0.07f, 0.12f, 0.07f), Glass);
        }

        // Plants in gold pots, and a gold rope each side of the way in.
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * ((w / 2) - 0.45f);
            b.Tube(new Vector3(x, 0.25f, (d / 2) - 0.45f), new Vector3(0.45f, 0.5f, 0.45f), VipGold);
            b.Ball(new Vector3(x, 0.95f, (d / 2) - 0.45f), new Vector3(0.7f, 1.0f, 0.7f), Leaf);
            Stanchions(b, new Vector3(side * 2.85f, 0, (d / 2) - 0.25f), new Vector3(side * 1.15f, 0, (d / 2) - 0.25f));
        }
    }

    /// <summary>
    /// The viewing platform, 6 m by 4 m of ground: a wooden deck 6 m by 3 m on scaffold legs 1.2 m
    /// up, glass rails with a gold top rail round it, and steps up the middle of the back.
    /// </summary>
    private static void VipPlatform(MeshBuilder b, Node3D node)
    {
        const float w = 6f;
        const float back = -1f;
        const float front = 2f;
        const float d = front - back;
        const float mid = (front + back) / 2;
        var h = PlatformHeight;

        foreach (var x in new[] { (-w / 2) + 0.15f, -1f, 1f, (w / 2) - 0.15f })
        {
            foreach (var z in new[] { back + 0.15f, front - 0.15f })
            {
                b.Tube(new Vector3(x, h / 2, z), new Vector3(0.08f, h, 0.08f), Steel);
            }
        }

        b.Box(new Vector3(0, h * 0.45f, back + 0.15f), new Vector3(w - 0.3f, 0.05f, 0.05f), SteelDark, new Vector3(0, 0, 12));
        b.Box(new Vector3(0, h - 0.06f, mid), new Vector3(w, 0.12f, d), Deck);
        for (var i = 0; i < 12; i++)
        {
            b.Box(new Vector3((-w / 2) + ((i + 0.5f) * (w / 12)), h + 0.002f, mid), new Vector3(0.02f, 0.005f, d - 0.02f), WoodDark);
        }

        // A black skirt round the front and sides with the gold VIP name on the front.
        b.Box(new Vector3(0, (h / 2) - 0.05f, front + 0.01f), new Vector3(w, h - 0.1f, 0.04f), VipInk);
        b.Box(new Vector3(0, h - 0.16f, front + 0.035f), new Vector3(w, 0.06f, 0.02f), VipGold);
        Painted(node, "VIP", new Vector3(0, (h / 2) - 0.05f, front + 0.04f), 1.6f, 0.55f, VipGold);
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            b.Box(new Vector3(x, (h / 2) - 0.05f, mid + 0.4f), new Vector3(0.04f, h - 0.1f, d - 0.8f), VipInk);
        }

        // Glass rails on the front and sides with a gold rail along the top.
        const float rail = 1.05f;
        b.Box(new Vector3(0, h + (rail / 2), front - 0.04f), new Vector3(w - 0.1f, rail - 0.1f, 0.03f), Glass);
        b.Box(new Vector3(0, h + rail, front - 0.04f), new Vector3(w, 0.07f, 0.07f), VipGold);
        foreach (var x in new[] { (-w / 2) + 0.04f, (w / 2) - 0.04f })
        {
            b.Box(new Vector3(x, h + (rail / 2), mid), new Vector3(0.03f, rail - 0.1f, d - 0.1f), Glass);
            b.Box(new Vector3(x, h + rail, mid), new Vector3(0.07f, 0.07f, d), VipGold);
        }

        // The back rail, open in the middle for the steps.
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * ((w / 4) + 0.25f);
            b.Box(new Vector3(x, h + (rail / 2), back + 0.04f), new Vector3((w / 2) - 0.5f, rail - 0.1f, 0.03f), Glass);
            b.Box(new Vector3(x, h + rail, back + 0.04f), new Vector3((w / 2) - 0.5f, 0.07f, 0.07f), VipGold);
        }

        // Steps up the back, 1 m wide, with a gold handrail each side.
        const int steps = 5;
        for (var i = 0; i < steps; i++)
        {
            var y = h * (i + 1) / (steps + 1);
            var z = back - 1f + ((i + 0.5f) * (1f / steps));
            b.Box(new Vector3(0, y - 0.1f, z), new Vector3(1.0f, 0.06f, 0.22f), Deck);
            b.Box(new Vector3(0, (y - 0.1f) / 2, z + 0.1f), new Vector3(0.96f, y - 0.1f, 0.03f), WoodDark);
        }

        foreach (var x in new[] { -0.52f, 0.52f })
        {
            b.Box(new Vector3(x, (h / 2) + 0.75f, back - 0.5f), new Vector3(0.05f, 0.05f, 1.56f), VipGold, new Vector3(-50, 0, 0));
        }
    }

    /// <summary>How high the deck of the VIP viewing platform is, in metres.</summary>
    public const float PlatformHeight = 1.2f;

    /// <summary>Two gold posts with a velvet rope between them, the VIP way of saying "this side only".</summary>
    private static void Stanchions(MeshBuilder b, Vector3 from, Vector3 to)
    {
        foreach (var at in new[] { from, to })
        {
            b.Tube(at + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f), VipGold);
            b.Tube(at + new Vector3(0, 0.45f, 0), new Vector3(0.06f, 0.9f, 0.06f), VipGold);
            b.Ball(at + new Vector3(0, 0.93f, 0), 0.1f, VipGold);
        }

        var span = to - from;
        var segments = 8;
        var last = from + new Vector3(0, 0.85f, 0);
        for (var i = 1; i <= segments; i++)
        {
            var t = i / (float)segments;
            var at = from + (span * t) + new Vector3(0, 0.85f - (Mathf.Sin(t * Mathf.Pi) * 0.18f), 0);
            var piece = at - last;
            var turn = Mathf.RadToDeg(Mathf.Atan2(-piece.Z, piece.X));
            var lean = Mathf.RadToDeg(Mathf.Asin(piece.Y / piece.Length()));
            b.Box((last + at) / 2, new Vector3(piece.Length() + 0.02f, 0.045f, 0.045f), new Color("#B0263E"), new Vector3(0, turn, lean));
            last = at;
        }
    }

    /// <summary>
    /// The sign that stands outside a stall kept for VIP guests: a black board in a gold frame on a
    /// post, saying "VIP only", at the front corner of the stall. Built round the stall's middle,
    /// the stall facing south, for a stall a given size in metres.
    /// </summary>
    public static Node3D VipSign(float width, float depth)
    {
        var node = new Node3D { Name = "VipSign" };
        var b = new MeshBuilder();
        var at = new Vector3((width / 2) + 0.15f, 0, (depth / 2) + 0.3f);
        b.Tube(at + new Vector3(0, 0.03f, 0), new Vector3(0.36f, 0.06f, 0.36f), VipInk);
        b.Tube(at + new Vector3(0, 0.75f, 0), new Vector3(0.06f, 1.5f, 0.06f), VipGold);
        b.Box(at + new Vector3(0, 1.62f, 0), new Vector3(0.9f, 0.5f, 0.05f), VipGold);
        b.Box(at + new Vector3(0, 1.62f, 0.006f), new Vector3(0.82f, 0.42f, 0.05f), VipInk);
        b.Ball(at + new Vector3(0, 1.93f, 0), new Vector3(0.18f, 0.16f, 0.05f), VipGold);
        node.AddChild(b.BuildNode("Shapes"));
        Painted(node, "VIP", at + new Vector3(0, 1.68f, 0.04f), 0.7f, 0.22f, VipGold);
        Painted(node, "only", at + new Vector3(0, 1.5f, 0.04f), 0.5f, 0.1f, Colors.White);
        return node;
    }
}
