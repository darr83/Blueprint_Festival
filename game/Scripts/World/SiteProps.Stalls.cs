using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

public static partial class SiteProps
{
    private static readonly Color Teal = new("#2FB7A8");
    private static readonly Color TealDark = new("#1F8E82");
    private static readonly Color Cream = new("#FFF6DD");
    private static readonly Color Glass = new("#9CCBE0");

    /// <summary>A stall built round its own middle, serving towards the south (+Z).</summary>
    public static Node3D Stall(StallKind kind)
    {
        var node = new Node3D { Name = Catalogue.Of(kind).Name };
        var b = new MeshBuilder();
        if (Shop(b, node, kind))
        {
            node.AddChild(b.BuildNode("Shapes"));
            if (kind == StallKind.Burgers)
            {
                // The cab sticks out at one end: slide the van along so the whole of it sits on its ground.
                foreach (var child in node.GetChildren())
                {
                    if (child is Node3D part)
                    {
                        part.Position += new Vector3(-0.55f, 0, -0.15f);
                    }
                }
            }

            return node;
        }

        switch (kind)
        {
            case StallKind.Bar:
                BarTent(b, node);
                break;
            case StallKind.Toilets:
                Toilets(b, node, 3);
                break;
            case StallKind.ToiletBlock:
                Toilets(b, node, 9);
                break;
            case StallKind.FirstAidTent:
                return FirstAidTent();
            case StallKind.MedicalTent:
                return MedicalTent();
            case StallKind.Bin:
                Bin(b, node);
                break;
            default:
                WaterPoint(b, node);
                break;
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>Where the people serving stand, in metres from the stall's middle, the stall facing south.</summary>
    public static Vector3[] StaffSpots(StallKind kind) => ShopStaff(kind) ?? kind switch
    {
        StallKind.Bar => new[] { new Vector3(-1.4f, 0, 0.55f), new Vector3(1.3f, 0, 0.55f) },
        _ => System.Array.Empty<Vector3>(),
    };

    /// <summary>A thought bubble with a simple picture of what someone wants. It sits this high above the ground.</summary>
    public static MeshInstance3D NeedBubble(Need need, float headHeight)
    {
        var b = new MeshBuilder();
        var top = headHeight + 0.75f;
        b.Ball(new Vector3(0, top, 0), new Vector3(0.62f, 0.56f, 0.2f), Colors.White);
        b.Ball(new Vector3(0.14f, top - 0.36f, 0), 0.12f, Colors.White);
        b.Ball(new Vector3(0.2f, top - 0.5f, 0), 0.07f, Colors.White);
        var at = new Vector3(0, top, 0.1f);
        switch (need)
        {
            case Need.Food:
                b.Ball(at + new Vector3(0, 0.08f, 0), new Vector3(0.34f, 0.2f, 0.1f), new Color("#D9963B"));
                b.Box(at, new Vector3(0.36f, 0.06f, 0.1f), new Color("#7A4A2A"));
                b.Box(at + new Vector3(0, 0.04f, 0.01f), new Vector3(0.38f, 0.025f, 0.1f), new Color("#5DAE4B"));
                b.Ball(at + new Vector3(0, -0.07f, 0), new Vector3(0.34f, 0.12f, 0.1f), new Color("#D9963B"));
                break;
            case Need.Drink:
                b.Ball(at + new Vector3(0, -0.05f, 0), new Vector3(0.26f, 0.26f, 0.1f), new Color("#3C8DD8"));
                b.Cone(at + new Vector3(0, 0.11f, 0), new Vector3(0.2f, 0.22f, 0.1f), new Color("#3C8DD8"));
                break;
            default:
                b.Tube(at, new Vector3(0.3f, 0.1f, 0.3f), Teal, new Vector3(90, 0, 0));
                b.Tube(at + new Vector3(0, 0, 0.02f), new Vector3(0.12f, 0.1f, 0.12f), Colors.White, new Vector3(90, 0, 0));
                b.Box(at + new Vector3(0.13f, -0.14f, 0), new Vector3(0.05f, 0.2f, 0.09f), Teal);
                break;
        }

        var node = b.BuildNode("Bubble");
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    /// <summary>Someone serving: an ordinary person in a pink top with a cream apron.</summary>
    public static Node3D Stallholder(int seed, Color? apron = null)
    {
        var node = new Node3D { Name = "Stallholder" };
        var look = LookGenerator.FestivalGoer(seed + 400, AgeGroup.Adult) with
        {
            Poncho = null, Hat = Hat.None, Sunglasses = false, Top = TopStyle.TShirt, TopColour = "#E94F7B", TopDetail = null,
            Legs = LegStyle.Trousers, LegColour = "#26292E", Feet = Footwear.Boots, FeetColour = "#2E2E33",
        };
        var person = PersonView.Build(look);
        node.AddChild(person);
        var h = person.StandingHeight / 1.6f;
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 0.74f * h, 0.125f), new Vector3(0.25f, 0.4f * h, 0.03f), apron ?? Cream);
        b.Box(new Vector3(0, 0.97f * h, 0.12f), new Vector3(0.14f, 0.1f * h, 0.03f), apron ?? Cream);
        node.AddChild(b.BuildNode("Apron"));
        return node;
    }

    private static void FoodVan(MeshBuilder b, Node3D node, string sign)
    {
        // A boxy catering van, side on, with the hatch open under a striped awning.
        const float w = 4.6f, d = 2.1f, floor = 0.55f, high = 2.2f;
        var body = floor + (high / 2);
        b.Box(new Vector3(0, body, 0), new Vector3(w, high, d), Cream);
        b.Box(new Vector3(0, floor + 0.3f, 0), new Vector3(w + 0.02f, 0.6f, d + 0.02f), Teal);
        b.Box(new Vector3(0, floor + high + 0.04f, 0), new Vector3(w + 0.1f, 0.08f, d + 0.1f), TealDark);
        // The cab end.
        b.Box(new Vector3((w / 2) + 0.55f, floor + 0.75f, 0), new Vector3(1.1f, 1.5f, d - 0.1f), Teal);
        b.Box(new Vector3((w / 2) + 0.7f, floor + 1.15f, 0), new Vector3(0.85f, 0.55f, d - 0.06f), Glass);
        foreach (var x in new[] { -1.4f, (w / 2) + 0.5f })
        {
            foreach (var z in new[] { -(d / 2) + 0.1f, (d / 2) - 0.1f })
            {
                b.Tube(new Vector3(x, 0.36f, z), new Vector3(0.72f, 0.24f, 0.72f), Rubber, new Vector3(90, 0, 0));
            }
        }

        // The hatch: a dark opening, a counter and the awning held up on two stays.
        var front = d / 2;
        b.Box(new Vector3(-0.3f, floor + 1.25f, front - 0.02f), new Vector3(3.0f, 1.0f, 0.08f), Charcoal);
        b.Box(new Vector3(-0.3f, floor + 0.74f, front + 0.2f), new Vector3(3.1f, 0.06f, 0.45f), Steel);
        for (var i = 0; i < 8; i++)
        {
            b.Box(new Vector3(-1.61f + (i * 0.375f), floor + 1.98f, front + 0.55f), new Vector3(0.375f, 0.04f, 1.2f), i % 2 == 0 ? Pink : Canvas, new Vector3(18, 0, 0));
        }

        b.Box(new Vector3(-1.8f, floor + 1.45f, front + 0.5f), new Vector3(0.03f, 0.03f, 1.25f), SteelDark, new Vector3(-30, 0, 0));
        b.Box(new Vector3(1.2f, floor + 1.45f, front + 0.5f), new Vector3(0.03f, 0.03f, 1.25f), SteelDark, new Vector3(-30, 0, 0));
        // Sauce bottles and a tray on the counter, a chimney on the roof, a menu board by the side.
        b.Tube(new Vector3(0.6f, floor + 0.87f, front + 0.25f), new Vector3(0.08f, 0.2f, 0.08f), new Color("#D9483B"));
        b.Tube(new Vector3(0.75f, floor + 0.87f, front + 0.25f), new Vector3(0.08f, 0.2f, 0.08f), Sunflower);
        b.Box(new Vector3(-0.9f, floor + 0.8f, front + 0.22f), new Vector3(0.5f, 0.05f, 0.3f), Canvas);
        b.Tube(new Vector3(-1.7f, floor + high + 0.35f, -0.4f), new Vector3(0.22f, 0.6f, 0.22f), Steel);
        b.Cone(new Vector3(-1.7f, floor + high + 0.72f, -0.4f), new Vector3(0.36f, 0.16f, 0.36f), SteelDark);
        b.Box(new Vector3(-2.9f, 0.6f, front + 0.9f), new Vector3(0.7f, 1.1f, 0.05f), Black, new Vector3(-12, 20, 0));
        b.Box(new Vector3(1.75f, floor + 1.25f, front + 0.01f), new Vector3(0.9f, 0.9f, 0.03f), Black);
        Painted(node, sign, new Vector3(-0.3f, floor + 0.3f, front + 0.03f), 2.6f, 0.34f, Cream);
        Painted(node, "Menu", new Vector3(1.75f, floor + 1.55f, front + 0.04f), 0.7f, 0.16f, Sunflower);
    }

    private static void BarTent(MeshBuilder b, Node3D node)
    {
        // A small marquee with a pitched striped roof, open at the front, with a wooden bar across it.
        const float w = 6f, d = 4f, eaves = 2.3f, ridge = 3.4f;
        foreach (var x in new[] { -w / 2, 0f, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.09f, eaves, 0.09f), Canvas);
            }
        }

        var slope = Mathf.Sqrt(((d / 2) * (d / 2)) + ((ridge - eaves) * (ridge - eaves)));
        var pitch = Mathf.RadToDeg(Mathf.Atan2(ridge - eaves, d / 2));
        for (var i = 0; i < 8; i++)
        {
            var x = (-w / 2) + ((i + 0.5f) * (w / 8));
            var colour = i % 2 == 0 ? Pink : Canvas;
            b.Box(new Vector3(x, (eaves + ridge) / 2, d / 4), new Vector3(w / 8, 0.05f, slope + 0.2f), colour, new Vector3(pitch, 0, 0));
            b.Box(new Vector3(x, (eaves + ridge) / 2, -d / 4), new Vector3(w / 8, 0.05f, slope + 0.2f), colour, new Vector3(-pitch, 0, 0));
        }

        // Back and side walls.
        b.Box(new Vector3(0, eaves / 2, -d / 2), new Vector3(w, eaves, 0.04f), Canvas);
        b.Box(new Vector3(-w / 2, eaves / 2, -1.2f), new Vector3(0.04f, eaves, 1.6f), Canvas);
        b.Box(new Vector3(w / 2, eaves / 2, -1.2f), new Vector3(0.04f, eaves, 1.6f), Canvas);
        b.Box(new Vector3(0, eaves - 0.1f, (d / 2) + 0.05f), new Vector3(w, 0.42f, 0.05f), Sunflower);
        Painted(node, "Bar", new Vector3(0, eaves - 0.1f, (d / 2) + 0.09f), 1.6f, 0.34f, new Color("#26292E"));
        Bunting(b, new Vector3(-w / 2, eaves - 0.38f, (d / 2) + 0.08f), new Vector3(w / 2, eaves - 0.38f, (d / 2) + 0.08f), 0.2f);

        // The bar: planks on barrels, with pump handles and a stack of kegs behind.
        b.Box(new Vector3(0, 1.05f, 1.2f), new Vector3(w - 0.8f, 0.08f, 0.6f), Wood);
        b.Box(new Vector3(0, 0.5f, 1.45f), new Vector3(w - 0.9f, 1.0f, 0.06f), WoodDark);
        foreach (var x in new[] { -2.3f, 0f, 2.3f })
        {
            b.Tube(new Vector3(x, 0.5f, 1.15f), new Vector3(0.6f, 1.0f, 0.6f), new Color("#8A5A32"));
            b.Tube(new Vector3(x, 0.3f, 1.15f), new Vector3(0.63f, 0.05f, 0.63f), SteelDark);
            b.Tube(new Vector3(x, 0.75f, 1.15f), new Vector3(0.63f, 0.05f, 0.63f), SteelDark);
        }

        foreach (var x in new[] { -1.2f, -0.9f, 0.9f, 1.2f })
        {
            b.Tube(new Vector3(x, 1.27f, 1.1f), new Vector3(0.05f, 0.36f, 0.05f), Black);
            b.Ball(new Vector3(x, 1.47f, 1.1f), 0.09f, x < 0 ? Sunflower : Pink);
        }

        for (var i = 0; i < 4; i++)
        {
            b.Tube(new Vector3(-1.8f + (i * 0.62f), 0.3f, -1.4f), new Vector3(0.5f, 0.6f, 0.5f), Steel);
            if (i < 3)
            {
                b.Tube(new Vector3(-1.5f + (i * 0.62f), 0.9f, -1.4f), new Vector3(0.5f, 0.6f, 0.5f), Steel);
            }
        }

        b.Box(new Vector3(1.9f, 0.5f, -1.4f), new Vector3(1.2f, 1.0f, 0.7f), new Color("#F5F5F5"));
    }

    private static void Toilets(MeshBuilder b, Node3D node, int cabins)
    {
        // Portable cabins in a row: three for a village, nine in a block.
        for (var i = 0; i < cabins; i++)
        {
            var x = (i - ((cabins - 1) / 2f)) * 1.25f;
            b.Box(new Vector3(x, 0.06f, 0), new Vector3(1.2f, 0.12f, 1.25f), Charcoal);
            b.Box(new Vector3(x, 1.2f, 0), new Vector3(1.14f, 2.16f, 1.18f), Teal);
            b.Box(new Vector3(x, 2.32f, 0), new Vector3(1.2f, 0.1f, 1.24f), Cream, new Vector3(4, 0, 0));
            // The doorway is dark inside. The door is a part of its own, hinged on the left, so it can swing open.
            b.Box(new Vector3(x, 1.12f, 0.585f), new Vector3(0.8f, 1.86f, 0.02f), new Color("#22302E"));
            var door = new Node3D { Name = $"Door{i}", Position = new Vector3(x - 0.4f, 0, 0.6f) };
            var leaf = new MeshBuilder();
            leaf.Box(new Vector3(0.4f, 1.12f, 0.005f), new Vector3(0.8f, 1.86f, 0.03f), Cream);
            leaf.Box(new Vector3(0.7f, 1.1f, 0.035f), new Vector3(0.06f, 0.16f, 0.04f), Black);
            leaf.Tube(new Vector3(0.4f, 1.75f, 0.03f), new Vector3(0.2f, 0.02f, 0.2f), Cream.Darkened(0.25f), new Vector3(90, 0, 0));
            door.AddChild(leaf.BuildNode("Leaf"));
            foreach (var (sign, colour) in new[] { ("Vacant", new Color("#1E9E52")), ("Engaged", new Color("#D9483B")) })
            {
                var disc = new MeshBuilder();
                disc.Tube(new Vector3(0.4f, 1.75f, 0.036f), new Vector3(0.16f, 0.02f, 0.16f), colour, new Vector3(90, 0, 0));
                var node3 = disc.BuildNode(sign);
                node3.Visible = sign == "Vacant";
                door.AddChild(node3);
            }

            node.AddChild(door);
            b.Tube(new Vector3(x - 0.42f, 2.5f, -0.45f), new Vector3(0.09f, 0.5f, 0.09f), Black);
        }

        Painted(node, "Toilets", new Vector3(0, 2.62f, 0.3f), 2.2f, 0.3f, Cream);
        b.Box(new Vector3(0, 2.62f, 0.27f), new Vector3(2.5f, 0.42f, 0.04f), TealDark);
        b.Tube(new Vector3(-1.2f, 2.5f, 0.27f), new Vector3(0.04f, 0.3f, 0.04f), SteelDark);
        b.Tube(new Vector3(1.2f, 2.5f, 0.27f), new Vector3(0.04f, 0.3f, 0.04f), SteelDark);
    }

    /// <summary>
    /// A litter bin: a sunflower-yellow drum with a pink band and a dark rim, on a 1 m square of
    /// ground. A child called "Rubbish" (bags and cups heaped over the rim) is shown once it is nearly full.
    /// </summary>
    private static void Bin(MeshBuilder b, Node3D node)
    {
        b.Tube(new Vector3(0, 0.45f, 0), new Vector3(0.62f, 0.9f, 0.62f), Sunflower);
        b.Tube(new Vector3(0, 0.62f, 0), new Vector3(0.64f, 0.16f, 0.64f), Pink);
        b.Tube(new Vector3(0, 0.9f, 0), new Vector3(0.68f, 0.06f, 0.68f), Charcoal);
        b.Tube(new Vector3(0, 0.03f, 0), new Vector3(0.66f, 0.06f, 0.66f), Charcoal);
        b.Tube(new Vector3(0, 0.915f, 0), new Vector3(0.54f, 0.04f, 0.54f), Black);

        var full = new MeshBuilder();
        full.Ball(new Vector3(-0.08f, 0.98f, 0.02f), new Vector3(0.4f, 0.3f, 0.38f), Black);
        full.Ball(new Vector3(0.16f, 0.96f, -0.1f), new Vector3(0.26f, 0.22f, 0.26f), Charcoal);
        full.Tube(new Vector3(0.14f, 1.05f, 0.14f), new Vector3(0.08f, 0.13f, 0.08f), Cream, new Vector3(24, 0, 18));
        full.Box(new Vector3(-0.2f, 1.04f, -0.14f), new Vector3(0.16f, 0.02f, 0.12f), Colors.White, new Vector3(20, 30, 10));
        full.Ball(new Vector3(0.42f, 0.12f, 0.2f), new Vector3(0.34f, 0.26f, 0.3f), Black);
        var rubbish = full.BuildNode("Rubbish");
        rubbish.Visible = false;
        node.AddChild(rubbish);
    }

    private static void WaterPoint(MeshBuilder b, Node3D node)
    {
        // A standpipe with two taps over a trough, and a sign.
        b.Box(new Vector3(0, 0.4f, 0), new Vector3(1.5f, 0.12f, 0.6f), Steel);
        b.Box(new Vector3(0, 0.46f, 0), new Vector3(1.4f, 0.02f, 0.5f), new Color("#6FC3E8"));
        foreach (var x in new[] { -0.65f, 0.65f })
        {
            b.Box(new Vector3(x, 0.2f, 0), new Vector3(0.06f, 0.4f, 0.5f), SteelDark);
        }

        b.Tube(new Vector3(0, 0.85f, -0.22f), new Vector3(0.09f, 1.7f, 0.09f), new Color("#4FA8D8"));
        b.Box(new Vector3(0, 1.0f, -0.22f), new Vector3(1.0f, 0.07f, 0.07f), new Color("#4FA8D8"));
        foreach (var x in new[] { -0.45f, 0.45f })
        {
            b.Tube(new Vector3(x, 0.94f, -0.1f), new Vector3(0.06f, 0.06f, 0.26f), Steel, new Vector3(90, 0, 0));
            b.Tube(new Vector3(x, 0.86f, 0.02f), new Vector3(0.05f, 0.14f, 0.05f), Steel);
            b.Box(new Vector3(x, 1.07f, -0.1f), new Vector3(0.16f, 0.03f, 0.05f), new Color("#D9483B"));
        }

        b.Box(new Vector3(0, 1.85f, -0.22f), new Vector3(0.9f, 0.4f, 0.04f), new Color("#4FA8D8"));
        Painted(node, "Water", new Vector3(0, 1.85f, -0.19f), 0.8f, 0.24f, Cream);
    }
}
