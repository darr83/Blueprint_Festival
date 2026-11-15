using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The food, drink and merchandise stalls. Each is built round its own middle, serving towards the
/// south (+Z), and each has something big on top that says what it sells from across the field.
/// </summary>
public static partial class SiteProps
{
    private static readonly Color Tomato = new("#D9483B");
    private static readonly Color Bun = new("#D9963B");
    private static readonly Color Lettuce = new("#5DAE4B");
    private static readonly Color Brick = new("#B5573A");
    private static readonly Color Sky = new("#6FC3E8");
    private static readonly Color Lilac = new("#C59BE0");
    private static readonly Color Mint = new("#7FD6B8");
    private static readonly Color Bamboo = new("#C9A45C");
    private static readonly Color Coffee = new("#6B4226");

    /// <summary>True for the stalls drawn here.</summary>
    private static bool Shop(MeshBuilder b, Node3D node, StallKind kind)
    {
        switch (kind)
        {
            case StallKind.HotDogs:
                HotDogCart(b, node);
                return true;
            case StallKind.Burgers:
                FoodVan(b, node, "Burgers");
                GiantBurger(b, new Vector3(1.2f, 2.75f, -0.1f), 1.0f);
                return true;
            case StallKind.Fries:
                FriesHut(b, node);
                return true;
            case StallKind.Pizza:
                PizzaOven(b, node);
                return true;
            case StallKind.Noodles:
                NoodleBar(b, node);
                return true;
            case StallKind.IceCream:
                IceCreamVan(b, node);
                return true;
            case StallKind.Coffee:
                CoffeeCart(b, node);
                return true;
            case StallKind.Slush:
                SlushStand(b, node);
                return true;
            case StallKind.Cocktails:
                CocktailBar(b, node);
                return true;
            case StallKind.Ponchos:
                PonchoStall(b, node);
                return true;
            case StallKind.BucketHats:
                HatStall(b, node);
                return true;
            case StallKind.Merchandise:
                MerchTent(b, node);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Where the people serving stand at the new stalls.</summary>
    private static Vector3[]? ShopStaff(StallKind kind) => kind switch
    {
        StallKind.HotDogs => new[] { new Vector3(0.1f, 0, -0.35f) },
        StallKind.Burgers => new[] { new Vector3(-1.45f, 0.55f, 0.4f), new Vector3(-0.15f, 0.55f, 0.4f) },
        StallKind.Fries => new[] { new Vector3(0, 0.12f, 0.1f) },
        StallKind.Pizza => new[] { new Vector3(-0.9f, 0, 0.5f), new Vector3(0.9f, 0, 0.5f) },
        StallKind.Noodles => new[] { new Vector3(-1.1f, 0, 0.1f), new Vector3(1.1f, 0, 0.1f) },
        StallKind.IceCream => new[] { new Vector3(-0.5f, 0.5f, 0.3f) },
        StallKind.Coffee => new[] { new Vector3(0.2f, 0, -0.4f) },
        StallKind.Slush => new[] { new Vector3(0, 0, -0.1f) },
        StallKind.Cocktails => new[] { new Vector3(-1.0f, 0, 0.2f), new Vector3(1.0f, 0, 0.2f) },
        StallKind.Ponchos => new[] { new Vector3(-0.6f, 0, 0.1f) },
        StallKind.BucketHats => new[] { new Vector3(0.6f, 0, 0.2f) },
        StallKind.Merchandise => new[] { new Vector3(-1.2f, 0, 0.5f), new Vector3(1.2f, 0, 0.5f) },
        _ => null,
    };

    /// <summary>Four posts and a flat striped canopy sloping down to the front, with a painted board along its front edge.</summary>
    private static void Canopy(MeshBuilder b, Node3D node, float w, float d, float eaves, Color one, Color two, string sign, Color board, Color letters)
    {
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.07f, eaves, 0.07f), Canvas);
            }
        }

        var stripes = Mathf.Max(4, Mathf.RoundToInt(w / 0.5f));
        for (var i = 0; i < stripes; i++)
        {
            var x = (-w / 2) + ((i + 0.5f) * (w / stripes));
            b.Box(new Vector3(x, eaves + 0.14f, 0), new Vector3((w / stripes) + 0.01f, 0.05f, d + 0.5f), i % 2 == 0 ? one : two, new Vector3(7, 0, 0));
        }

        b.Box(new Vector3(0, eaves - 0.14f, (d / 2) + 0.2f), new Vector3(w + 0.1f, 0.34f, 0.05f), board);
        Painted(node, sign, new Vector3(0, eaves - 0.14f, (d / 2) + 0.235f), w - 0.3f, 0.26f, letters);
    }

    /// <summary>A serving counter across the front: a top, a painted front and two ends.</summary>
    private static void Counter(MeshBuilder b, float w, float z, Color top, Color front, float height = 1.0f)
    {
        b.Box(new Vector3(0, height, z), new Vector3(w, 0.07f, 0.6f), top);
        b.Box(new Vector3(0, height / 2, z + 0.27f), new Vector3(w - 0.06f, height, 0.05f), front);
        foreach (var x in new[] { (-w / 2) + 0.04f, (w / 2) - 0.04f })
        {
            b.Box(new Vector3(x, height / 2, z), new Vector3(0.05f, height, 0.55f), front.Darkened(0.15f));
        }
    }

    private static void GiantBurger(MeshBuilder b, Vector3 at, float size)
    {
        b.Tube(at + new Vector3(0, 0.1f * size, 0), new Vector3(size, 0.2f * size, size), Bun);
        b.Tube(at + new Vector3(0, 0.26f * size, 0), new Vector3(size * 1.06f, 0.14f * size, size * 1.06f), new Color("#6B3A22"));
        b.Tube(at + new Vector3(0, 0.35f * size, 0), new Vector3(size * 1.1f, 0.05f * size, size * 1.1f), Sunflower);
        b.Tube(at + new Vector3(0, 0.4f * size, 0), new Vector3(size * 1.12f, 0.05f * size, size * 1.12f), Lettuce);
        b.Ball(at + new Vector3(0, 0.45f * size, 0), new Vector3(size, 0.62f * size, size), Bun);
    }

    private static void HotDogCart(MeshBuilder b, Node3D node)
    {
        // A red cart on two big wheels, a steel top with a hot plate, and a big umbrella over it.
        b.Box(new Vector3(0, 0.7f, 0.35f), new Vector3(1.9f, 0.75f, 0.9f), Tomato);
        b.Box(new Vector3(0, 1.1f, 0.35f), new Vector3(2.0f, 0.06f, 1.0f), Steel);
        b.Box(new Vector3(0, 0.72f, 0.81f), new Vector3(1.7f, 0.5f, 0.02f), Sunflower);
        Painted(node, "Hot Dogs", new Vector3(0, 0.72f, 0.825f), 1.6f, 0.3f, Tomato);
        foreach (var x in new[] { -0.7f, 0.7f })
        {
            b.Tube(new Vector3(x, 0.36f, 0.86f), new Vector3(0.72f, 0.08f, 0.72f), Rubber, new Vector3(90, 0, 0));
            b.Tube(new Vector3(x, 0.36f, 0.9f), new Vector3(0.3f, 0.06f, 0.3f), Steel, new Vector3(90, 0, 0));
        }

        b.Box(new Vector3(1.15f, 0.9f, 0.35f), new Vector3(0.4f, 0.05f, 0.05f), SteelDark);
        b.Box(new Vector3(-0.5f, 1.16f, 0.3f), new Vector3(0.7f, 0.06f, 0.5f), Charcoal);
        for (var i = 0; i < 4; i++)
        {
            b.Pill(new Vector3(-0.72f + (i * 0.15f), 1.22f, 0.3f), new Vector3(0.08f, 0.4f, 0.08f), new Color("#B5482E"), new Vector3(90, 0, 0));
        }

        b.Tube(new Vector3(0.45f, 1.24f, 0.45f), new Vector3(0.1f, 0.24f, 0.1f), Tomato);
        b.Tube(new Vector3(0.62f, 1.24f, 0.45f), new Vector3(0.1f, 0.24f, 0.1f), Sunflower);
        b.Box(new Vector3(0.55f, 1.16f, 0.15f), new Vector3(0.5f, 0.08f, 0.3f), Canvas);

        // The umbrella, with a giant hot dog on its tip.
        b.Tube(new Vector3(0, 1.5f, -0.05f), new Vector3(0.07f, 3.0f, 0.07f), SteelDark);
        b.Cone(new Vector3(0, 2.75f, -0.05f), new Vector3(2.9f, 0.6f, 2.9f), Tomato);
        b.Tube(new Vector3(0, 2.47f, -0.05f), new Vector3(2.92f, 0.07f, 2.92f), Sunflower);
        b.Pill(new Vector3(0, 3.35f, -0.05f), new Vector3(0.5f, 1.5f, 0.42f), Bun, new Vector3(0, 0, 90));
        b.Pill(new Vector3(0, 3.5f, -0.05f), new Vector3(0.28f, 1.75f, 0.28f), new Color("#B5482E"), new Vector3(0, 0, 90));
        for (var i = 0; i < 7; i++)
        {
            b.Box(new Vector3(-0.6f + (i * 0.2f), 3.64f, -0.05f), new Vector3(0.24f, 0.04f, 0.06f), Sunflower, new Vector3(0, i % 2 == 0 ? 35 : -35, 0));
        }
    }

    private static void FriesHut(MeshBuilder b, Node3D node)
    {
        // A little wooden hut with a pitched red and yellow roof, a hatch and a giant cone of fries on top.
        const float w = 2.6f, d = 2.3f, wall = 2.1f;
        b.Box(new Vector3(0, 0.06f, 0), new Vector3(w + 0.1f, 0.12f, d + 0.1f), WoodDark);
        b.Box(new Vector3(0, wall / 2, -d / 2), new Vector3(w, wall, 0.06f), Wood);
        b.Box(new Vector3(-w / 2, wall / 2, 0), new Vector3(0.06f, wall, d), Wood);
        b.Box(new Vector3(w / 2, wall / 2, 0), new Vector3(0.06f, wall, d), Wood);
        b.Box(new Vector3(0, 0.55f, d / 2), new Vector3(w, 1.0f, 0.06f), Tomato);
        b.Box(new Vector3(0, 1.08f, (d / 2) + 0.12f), new Vector3(w + 0.1f, 0.06f, 0.4f), Canvas);
        b.Box(new Vector3(0, 1.95f, d / 2), new Vector3(w, 0.32f, 0.06f), Sunflower);
        Painted(node, "Fries", new Vector3(0, 1.95f, (d / 2) + 0.04f), 1.8f, 0.26f, Tomato);
        for (var i = 0; i < 6; i++)
        {
            b.Box(new Vector3(-1.08f + (i * 0.43f), 0.55f, (d / 2) + 0.035f), new Vector3(0.2f, 1.0f, 0.01f), Sunflower);
        }

        var slope = Mathf.Sqrt(((w / 2) * (w / 2)) + 0.49f);
        var pitch = Mathf.RadToDeg(Mathf.Atan2(0.7f, w / 2));
        b.Box(new Vector3(-w / 4, wall + 0.35f, 0), new Vector3(slope + 0.2f, 0.06f, d + 0.4f), Tomato, new Vector3(0, 0, pitch));
        b.Box(new Vector3(w / 4, wall + 0.35f, 0), new Vector3(slope + 0.2f, 0.06f, d + 0.4f), Tomato, new Vector3(0, 0, -pitch));
        b.Box(new Vector3(0, wall + 0.2f, d / 2), new Vector3(w * 0.7f, 0.4f, 0.05f), Wood);

        // The fryer inside, and paper cones on the counter.
        b.Box(new Vector3(-0.7f, 0.6f, -0.7f), new Vector3(0.9f, 1.0f, 0.6f), Steel);
        b.Box(new Vector3(-0.7f, 1.12f, -0.7f), new Vector3(0.8f, 0.04f, 0.5f), new Color("#E3B23C"));
        for (var i = 0; i < 3; i++)
        {
            FriesCone(b, new Vector3(0.4f + (i * 0.32f), 1.11f, (d / 2) + 0.12f), 0.2f);
        }

        FriesCone(b, new Vector3(0, wall + 0.72f, 0), 1.0f);
    }

    /// <summary>A red paper cone of fries, standing on its point.</summary>
    private static void FriesCone(MeshBuilder b, Vector3 foot, float size)
    {
        b.Cone(foot + new Vector3(0, 0.5f * size, 0), new Vector3(0.6f * size, size, 0.45f * size), Tomato, new Vector3(180, 0, 0));
        for (var i = 0; i < 6; i++)
        {
            var x = (-0.2f + (i * 0.08f)) * size;
            b.Box(foot + new Vector3(x, (1.05f + (i % 3 * 0.07f)) * size, (i % 2 == 0 ? 0.05f : -0.05f) * size), new Vector3(0.07f * size, 0.5f * size, 0.07f * size), new Color("#F2CB4A"), new Vector3(0, 0, (i - 2.5f) * -7f));
        }
    }

    private static void PizzaOven(MeshBuilder b, Node3D node)
    {
        // A gazebo in red, white and green, a domed brick oven at the back and pizzas on the counter.
        const float w = 3.8f, d = 3.6f;
        Canopy(b, node, w, d, 2.3f, Tomato, Canvas, "Pizza", new Color("#2E8B57"), Canvas);
        b.Box(new Vector3(0, 0.45f, -1.1f), new Vector3(2.0f, 0.9f, 1.3f), new Color("#8E8A80"));
        b.Ball(new Vector3(0, 0.9f, -1.1f), new Vector3(1.9f, 1.7f, 1.3f), Brick);
        b.Ball(new Vector3(0, 1.05f, -0.52f), new Vector3(0.8f, 0.55f, 0.2f), Black);
        b.Ball(new Vector3(0, 1.0f, -0.5f), new Vector3(0.5f, 0.3f, 0.2f), new Color("#FF8A2B"));
        b.Tube(new Vector3(0.5f, 2.0f, -1.3f), new Vector3(0.26f, 1.4f, 0.26f), Charcoal);
        b.Cone(new Vector3(0.5f, 2.8f, -1.3f), new Vector3(0.44f, 0.2f, 0.44f), Black);
        // Logs for the fire.
        for (var i = 0; i < 3; i++)
        {
            b.Tube(new Vector3(-1.5f, 0.12f + (i * 0.2f), -1.2f), new Vector3(0.2f, 0.7f, 0.2f), WoodDark, new Vector3(90, 0, 0));
        }

        Counter(b, w - 0.5f, 1.25f, Wood, Tomato);
        for (var i = 0; i < 3; i++)
        {
            var x = -1.1f + (i * 1.1f);
            b.Tube(new Vector3(x, 1.06f, 1.25f), new Vector3(0.62f, 0.03f, 0.62f), Wood.Lightened(0.2f));
            b.Tube(new Vector3(x, 1.085f, 1.25f), new Vector3(0.54f, 0.03f, 0.54f), new Color("#E9C46A"));
            b.Tube(new Vector3(x, 1.1f, 1.25f), new Vector3(0.44f, 0.02f, 0.44f), Tomato);
            for (var k = 0; k < 4; k++)
            {
                b.Tube(new Vector3(x + (Mathf.Cos(k * 1.6f) * 0.12f), 1.115f, 1.25f + (Mathf.Sin(k * 1.6f) * 0.12f)), new Vector3(0.09f, 0.015f, 0.09f), i == 1 ? new Color("#8C2F23") : Lettuce);
            }
        }

        // A giant slice on the board at the top.
        b.Cone(new Vector3(0, 2.95f, 0.6f), new Vector3(0.9f, 1.0f, 0.12f), new Color("#E9C46A"), new Vector3(180, 0, 0));
        b.Cone(new Vector3(0, 2.99f, 0.63f), new Vector3(0.7f, 0.8f, 0.12f), Tomato, new Vector3(180, 0, 0));
        b.Pill(new Vector3(0, 3.46f, 0.6f), new Vector3(0.16f, 1.0f, 0.16f), Bun, new Vector3(0, 0, 90));
        foreach (var (x, y) in new[] { (-0.15f, 3.25f), (0.12f, 3.15f), (0, 2.9f) })
        {
            b.Tube(new Vector3(x, y, 0.7f), new Vector3(0.14f, 0.03f, 0.14f), new Color("#8C2F23"), new Vector3(90, 0, 0));
        }
    }

    private static void NoodleBar(MeshBuilder b, Node3D node)
    {
        // A long counter under a red roof with turned-up ends, hung with paper lanterns.
        const float w = 4.8f, d = 2.8f, eaves = 2.3f;
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.1f, eaves, 0.1f), Tomato);
            }
        }

        b.Box(new Vector3(0, eaves + 0.08f, 0), new Vector3(w + 0.5f, 0.12f, d + 0.6f), new Color("#B5302A"));
        b.Box(new Vector3(0, eaves + 0.3f, 0), new Vector3(w * 0.7f, 0.34f, d * 0.6f), new Color("#B5302A"));
        b.Box(new Vector3(0, eaves + 0.5f, 0), new Vector3(w * 0.76f, 0.07f, d * 0.7f), Charcoal);
        foreach (var x in new[] { -(w / 2) - 0.3f, (w / 2) + 0.3f })
        {
            b.Box(new Vector3(x, eaves + 0.2f, 0), new Vector3(0.3f, 0.1f, d + 0.6f), new Color("#B5302A"), new Vector3(0, 0, x < 0 ? -35 : 35));
        }

        b.Box(new Vector3(0, eaves / 2, -d / 2), new Vector3(w, eaves, 0.05f), Canvas);
        b.Box(new Vector3(0, eaves - 0.2f, (d / 2) + 0.3f), new Vector3(2.4f, 0.36f, 0.05f), Black);
        Painted(node, "Noodles", new Vector3(0, eaves - 0.2f, (d / 2) + 0.335f), 2.2f, 0.26f, Sunflower);
        for (var i = 0; i < 6; i++)
        {
            if (i is 2 or 3)
            {
                continue;
            }

            var x = -2.0f + (i * 0.8f);
            b.Tube(new Vector3(x, eaves - 0.1f, (d / 2) + 0.25f), new Vector3(0.02f, 0.2f, 0.02f), Black);
            b.Ball(new Vector3(x, eaves - 0.4f, (d / 2) + 0.25f), new Vector3(0.36f, 0.42f, 0.36f), i % 2 == 0 ? Tomato : Sunflower);
            b.Tube(new Vector3(x, eaves - 0.62f, (d / 2) + 0.25f), new Vector3(0.14f, 0.05f, 0.14f), Black);
        }

        Counter(b, w - 0.4f, 0.95f, Wood, new Color("#B5302A"));
        // Two woks on burners, and a stack of noodle boxes.
        foreach (var x in new[] { -1.5f, 0.3f })
        {
            b.Tube(new Vector3(x, 1.1f, 0.9f), new Vector3(0.4f, 0.12f, 0.4f), Charcoal);
            b.Ball(new Vector3(x, 1.2f, 0.9f), new Vector3(0.7f, 0.22f, 0.7f), Black);
            b.Ball(new Vector3(x, 1.24f, 0.9f), new Vector3(0.56f, 0.14f, 0.56f), new Color("#E9C46A"));
            b.Box(new Vector3(x + 0.5f, 1.22f, 0.9f), new Vector3(0.4f, 0.04f, 0.05f), WoodDark);
        }

        for (var i = 0; i < 3; i++)
        {
            NoodleBox(b, new Vector3(1.5f + (i % 2 * 0.3f), 1.04f + (i / 2 * 0.24f), 0.95f), 0.24f);
        }

        NoodleBox(b, new Vector3(0, eaves + 0.54f, 0.3f), 0.9f);
    }

    /// <summary>A white noodle box with a red band and chopsticks sticking out.</summary>
    private static void NoodleBox(MeshBuilder b, Vector3 foot, float size)
    {
        b.Box(foot + new Vector3(0, 0.45f * size, 0), new Vector3(0.8f * size, 0.9f * size, 0.7f * size), Canvas);
        b.Box(foot + new Vector3(0, 0.45f * size, 0), new Vector3(0.82f * size, 0.3f * size, 0.72f * size), Tomato);
        b.Ball(foot + new Vector3(0, 0.92f * size, 0), new Vector3(0.7f * size, 0.3f * size, 0.6f * size), new Color("#E9C46A"));
        b.Box(foot + new Vector3(0.12f * size, 1.15f * size, 0), new Vector3(0.05f * size, 0.9f * size, 0.05f * size), WoodDark, new Vector3(0, 0, -18));
        b.Box(foot + new Vector3(0.24f * size, 1.12f * size, 0), new Vector3(0.05f * size, 0.9f * size, 0.05f * size), WoodDark, new Vector3(0, 0, -28));
    }

    private static void IceCreamVan(MeshBuilder b, Node3D node)
    {
        // A pastel van, side on, with a serving window and a giant cone on the roof.
        const float w = 3.6f, d = 2.0f, floor = 0.5f, high = 2.0f;
        var pinkPastel = new Color("#F7B9CF");
        b.Box(new Vector3(-0.4f, floor + (high / 2), 0), new Vector3(w, high, d), Cream);
        b.Box(new Vector3(-0.4f, floor + 0.35f, 0), new Vector3(w + 0.02f, 0.7f, d + 0.02f), pinkPastel);
        b.Box(new Vector3(-0.4f, floor + high + 0.04f, 0), new Vector3(w + 0.1f, 0.08f, d + 0.1f), Mint);
        b.Box(new Vector3(1.85f, floor + 0.7f, 0), new Vector3(0.9f, 1.4f, d - 0.1f), pinkPastel);
        b.Box(new Vector3(1.95f, floor + 1.1f, 0), new Vector3(0.72f, 0.5f, d - 0.06f), Glass);
        foreach (var x in new[] { -1.5f, 1.7f })
        {
            foreach (var z in new[] { -(d / 2) + 0.1f, (d / 2) - 0.1f })
            {
                b.Tube(new Vector3(x, 0.33f, z), new Vector3(0.66f, 0.22f, 0.66f), Rubber, new Vector3(90, 0, 0));
            }
        }

        var front = d / 2;
        b.Box(new Vector3(-0.5f, floor + 1.25f, front - 0.02f), new Vector3(2.2f, 0.9f, 0.08f), Charcoal);
        b.Box(new Vector3(-0.5f, floor + 0.78f, front + 0.15f), new Vector3(2.3f, 0.06f, 0.36f), Steel);
        for (var i = 0; i < 6; i++)
        {
            b.Box(new Vector3(-1.42f + (i * 0.37f), floor + 1.85f, front + 0.4f), new Vector3(0.37f, 0.04f, 0.9f), i % 2 == 0 ? pinkPastel : Canvas, new Vector3(16, 0, 0));
        }

        Painted(node, "Ice Cream", new Vector3(-0.5f, floor + 0.35f, front + 0.03f), 2.2f, 0.3f, PinkDark);
        // Pictures of lollies beside the window.
        foreach (var (x, colour) in new[] { (0.85f, Tomato), (1.1f, Sunflower) })
        {
            b.Pill(new Vector3(x, floor + 1.35f, front + 0.02f), new Vector3(0.16f, 0.4f, 0.04f), colour);
            b.Box(new Vector3(x, floor + 1.08f, front + 0.02f), new Vector3(0.04f, 0.2f, 0.03f), Wood);
        }

        // The giant cone: a wafer on its point with two scoops and a flake.
        var top = floor + high + 0.08f;
        b.Cone(new Vector3(-0.6f, top + 0.6f, 0), new Vector3(0.8f, 1.2f, 0.8f), new Color("#D9A65A"), new Vector3(180, 0, 0));
        b.Ball(new Vector3(-0.6f, top + 1.3f, 0), 0.86f, pinkPastel);
        b.Ball(new Vector3(-0.6f, top + 1.8f, 0), 0.66f, Cream);
        b.Tube(new Vector3(-0.4f, top + 1.95f, 0), new Vector3(0.1f, 0.6f, 0.1f), new Color("#5A3A22"), new Vector3(0, 0, -25));
    }

    private static void CoffeeCart(MeshBuilder b, Node3D node)
    {
        // A brown cart with a cream awning, a coffee machine and a giant cup on the roof.
        const float w = 2.4f, eaves = 2.2f;
        b.Box(new Vector3(0, 0.6f, 0.35f), new Vector3(w, 0.9f, 0.9f), Coffee);
        b.Box(new Vector3(0, 1.08f, 0.35f), new Vector3(w + 0.1f, 0.06f, 1.0f), Wood.Lightened(0.15f));
        b.Box(new Vector3(0, 0.62f, 0.81f), new Vector3(w - 0.4f, 0.5f, 0.02f), Cream);
        Painted(node, "Coffee", new Vector3(0, 0.62f, 0.825f), w - 0.6f, 0.3f, Coffee);
        foreach (var x in new[] { -0.85f, 0.85f })
        {
            b.Tube(new Vector3(x, 0.3f, 0.84f), new Vector3(0.6f, 0.08f, 0.6f), Rubber, new Vector3(90, 0, 0));
            b.Tube(new Vector3(x, eaves / 2, -0.15f), new Vector3(0.06f, eaves, 0.06f), Coffee.Darkened(0.2f));
        }

        b.Box(new Vector3(0, eaves, 0.3f), new Vector3(w + 0.3f, 0.06f, 1.5f), Cream, new Vector3(8, 0, 0));
        for (var i = 0; i < 6; i++)
        {
            b.Ball(new Vector3(-1.1f + (i * 0.44f), eaves - 0.18f, 1.03f), new Vector3(0.44f, 0.22f, 0.04f), i % 2 == 0 ? Coffee : Cream);
        }

        // The machine, a grinder and a stack of cups.
        b.Box(new Vector3(-0.5f, 1.36f, 0.2f), new Vector3(0.8f, 0.5f, 0.5f), Steel);
        b.Box(new Vector3(-0.5f, 1.64f, 0.2f), new Vector3(0.84f, 0.06f, 0.54f), Tomato);
        b.Tube(new Vector3(-0.7f, 1.2f, 0.5f), new Vector3(0.05f, 0.14f, 0.05f), Black);
        b.Tube(new Vector3(-0.3f, 1.2f, 0.5f), new Vector3(0.05f, 0.14f, 0.05f), Black);
        b.Tube(new Vector3(0.3f, 1.3f, 0.15f), new Vector3(0.22f, 0.4f, 0.22f), Black);
        b.Cone(new Vector3(0.3f, 1.62f, 0.15f), new Vector3(0.26f, 0.24f, 0.26f), Glass, new Vector3(180, 0, 0));
        b.Tube(new Vector3(0.8f, 1.26f, 0.5f), new Vector3(0.12f, 0.32f, 0.12f), Cream);

        // The giant cup, with steam.
        b.Tube(new Vector3(0, eaves + 0.5f, 0), new Vector3(0.9f, 0.8f, 0.9f), Cream);
        b.Tube(new Vector3(0, eaves + 0.5f, 0), new Vector3(0.92f, 0.3f, 0.92f), Tomato);
        b.Tube(new Vector3(0, eaves + 0.9f, 0), new Vector3(0.8f, 0.03f, 0.8f), Coffee);
        b.Tube(new Vector3(0.55f, eaves + 0.5f, 0), new Vector3(0.4f, 0.1f, 0.4f), Cream, new Vector3(90, 0, 0));
        b.Tube(new Vector3(0, eaves + 0.08f, 0), new Vector3(1.2f, 0.06f, 1.2f), Cream);
        foreach (var x in new[] { -0.15f, 0.15f })
        {
            b.Ball(new Vector3(x, eaves + 1.15f, 0), new Vector3(0.14f, 0.3f, 0.14f), Canvas);
            b.Ball(new Vector3(x + 0.08f, eaves + 1.42f, 0), new Vector3(0.11f, 0.24f, 0.11f), Canvas);
        }
    }

    private static void SlushStand(MeshBuilder b, Node3D node)
    {
        // A bright blue stand with two churning tanks of slush.
        const float w = 2.5f, d = 2.0f;
        Canopy(b, node, w, d, 2.25f, Sky, Canvas, "Slush", new Color("#2E7FC2"), Canvas);
        Counter(b, w - 0.2f, 0.55f, Canvas, new Color("#2E7FC2"));
        foreach (var (x, colour) in new[] { (-0.55f, new Color("#3C8DD8")), (0.25f, Tomato) })
        {
            b.Box(new Vector3(x, 1.14f, 0.5f), new Vector3(0.6f, 0.22f, 0.5f), Charcoal);
            b.Tube(new Vector3(x, 1.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), colour.Lightened(0.25f));
            b.Tube(new Vector3(x, 1.78f, 0.5f), new Vector3(0.54f, 0.07f, 0.54f), Charcoal);
            b.Box(new Vector3(x, 1.2f, 0.78f), new Vector3(0.08f, 0.1f, 0.08f), Steel);
        }

        for (var i = 0; i < 3; i++)
        {
            b.Cone(new Vector3(0.75f + (i % 2 * 0.2f), 1.16f + (i / 2 * 0.1f), 0.55f), new Vector3(0.16f, 0.26f, 0.16f), Canvas, new Vector3(180, 0, 0));
        }

        // A giant cup of slush with a straw on the canopy.
        b.Cone(new Vector3(0, 3.0f, -0.2f), new Vector3(0.9f, 1.3f, 0.9f), Canvas, new Vector3(180, 0, 0));
        b.Tube(new Vector3(0, 3.0f, -0.2f), new Vector3(0.56f, 0.2f, 0.56f), new Color("#2E7FC2"));
        b.Ball(new Vector3(0, 3.68f, -0.2f), new Vector3(0.9f, 0.6f, 0.9f), new Color("#3C8DD8"));
        b.Tube(new Vector3(0.2f, 4.05f, -0.2f), new Vector3(0.07f, 0.8f, 0.07f), Tomato, new Vector3(0, 0, -15));
    }

    private static void CocktailBar(MeshBuilder b, Node3D node)
    {
        // A beach bar: bamboo posts, a thatched roof, a bamboo counter and a row of bright bottles.
        const float w = 4.6f, d = 3.0f, eaves = 2.3f;
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.14f, eaves, 0.14f), Bamboo);
            }
        }

        // The thatch: two shaggy layers over the back half, so the bar itself can be seen from above.
        b.Box(new Vector3(0, eaves + 0.05f, 0), new Vector3(w + 0.3f, 0.1f, d + 0.3f), StrawDark);
        b.Cone(new Vector3(0, eaves + 0.5f, -0.1f), new Vector3(w + 0.9f, 0.9f, d + 0.5f), Straw);
        b.Cone(new Vector3(0, eaves + 0.22f, -0.1f), new Vector3(w + 1.3f, 0.45f, d + 0.9f), StrawDark);
        b.Box(new Vector3(0, eaves - 0.3f, (d / 2) + 0.5f), new Vector3(2.8f, 0.4f, 0.05f), new Color("#1F8E82"));
        Painted(node, "Cocktails", new Vector3(0, eaves - 0.3f, (d / 2) + 0.535f), 2.6f, 0.3f, Sunflower);
        foreach (var x in new[] { -1.3f, 1.3f })
        {
            b.Tube(new Vector3(x, eaves - 0.05f, (d / 2) + 0.5f), new Vector3(0.03f, 0.3f, 0.03f), Bamboo);
        }

        b.Box(new Vector3(0, 1.08f, 1.0f), new Vector3(w - 0.4f, 0.08f, 0.65f), Wood);
        for (var i = 0; i < 20; i++)
        {
            b.Tube(new Vector3(-2.0f + (i * 0.21f), 0.52f, 1.3f), new Vector3(0.19f, 1.04f, 0.19f), i % 2 == 0 ? Bamboo : Bamboo.Darkened(0.12f));
        }

        // The back bar, with bottles.
        b.Box(new Vector3(0, 0.8f, -1.2f), new Vector3(w - 1.0f, 1.6f, 0.4f), WoodDark);
        var bottles = new[] { Tomato, Sunflower, Lettuce, Sky, Lilac, Pink, new Color("#FF7A45"), Mint };
        for (var i = 0; i < 8; i++)
        {
            var x = -1.4f + (i * 0.4f);
            b.Tube(new Vector3(x, 1.76f, -1.2f), new Vector3(0.13f, 0.32f, 0.13f), bottles[i]);
            b.Tube(new Vector3(x, 1.98f, -1.2f), new Vector3(0.05f, 0.14f, 0.05f), bottles[i].Darkened(0.2f));
        }

        // Glasses with umbrellas on the bar, and two stools in front.
        foreach (var (x, colour) in new[] { (-1.2f, new Color("#FF7A45")), (0.2f, Pink), (1.4f, Lettuce) })
        {
            Cocktail(b, new Vector3(x, 1.12f, 1.0f), 0.24f, colour);
        }

        // A giant cocktail on the roof.
        Cocktail(b, new Vector3(0, eaves + 0.9f, -0.1f), 1.0f, new Color("#FF7A45"));
    }

    /// <summary>A cocktail glass with a paper umbrella and a slice of fruit.</summary>
    private static void Cocktail(MeshBuilder b, Vector3 foot, float size, Color drink)
    {
        b.Tube(foot + new Vector3(0, 0.02f * size, 0), new Vector3(0.5f * size, 0.04f * size, 0.5f * size), Glass);
        b.Tube(foot + new Vector3(0, 0.3f * size, 0), new Vector3(0.06f * size, 0.56f * size, 0.06f * size), Glass);
        b.Cone(foot + new Vector3(0, 0.85f * size, 0), new Vector3(0.9f * size, 0.6f * size, 0.9f * size), drink, new Vector3(180, 0, 0));
        b.Tube(foot + new Vector3(0.2f * size, 1.2f * size, 0), new Vector3(0.03f * size, 0.6f * size, 0.03f * size), WoodDark, new Vector3(0, 0, -20));
        b.Cone(foot + new Vector3(0.3f * size, 1.5f * size, 0), new Vector3(0.6f * size, 0.2f * size, 0.6f * size), Pink, new Vector3(0, 0, -20));
        b.Tube(foot + new Vector3(-0.42f * size, 1.14f * size, 0), new Vector3(0.3f * size, 0.04f * size, 0.3f * size), Sunflower, new Vector3(90, 0, 0));
    }

    private static void PonchoStall(MeshBuilder b, Node3D node)
    {
        // A small stall under a sky-blue canopy, with ponchos in every colour hanging from a rail.
        const float w = 2.6f, d = 2.0f;
        Canopy(b, node, w, d, 2.25f, Sky, Canvas, "Ponchos", new Color("#2E7FC2"), Canvas);
        b.Box(new Vector3(0, 1.1f, -d / 2), new Vector3(w, 2.2f, 0.04f), Canvas);
        b.Tube(new Vector3(0, 1.95f, 0.5f), new Vector3(0.04f, w - 0.2f, 0.04f), Steel, new Vector3(0, 0, 90));
        for (var i = 0; i < 5; i++)
        {
            var colour = new Color(LookGenerator.PonchoColours[i % LookGenerator.PonchoColours.Length]);
            var x = -0.95f + (i * 0.475f);
            if (i == 1)
            {
                // A gap in the rail where the stallholder stands.
                continue;
            }

            b.Cone(new Vector3(x, 1.5f, 0.5f), new Vector3(0.5f, 0.8f, 0.3f), colour);
            b.Ball(new Vector3(x, 1.92f, 0.5f), new Vector3(0.22f, 0.22f, 0.2f), colour);
        }

        b.Box(new Vector3(0.45f, 0.75f, 0.7f), new Vector3(1.4f, 0.06f, 0.6f), Wood);
        b.Box(new Vector3(0.45f, 0.37f, 0.98f), new Vector3(1.36f, 0.74f, 0.04f), new Color("#2E7FC2"));
        for (var i = 0; i < 3; i++)
        {
            b.Box(new Vector3(0.05f + (i * 0.4f), 0.84f, 0.7f), new Vector3(0.3f, 0.12f, 0.36f), new Color(LookGenerator.PonchoColours[(i * 2) % LookGenerator.PonchoColours.Length]));
        }

        // A big umbrella on the roof, so nobody is in any doubt.
        b.Tube(new Vector3(0, 2.9f, -0.2f), new Vector3(0.05f, 0.9f, 0.05f), SteelDark);
        b.Ball(new Vector3(0, 3.3f, -0.2f), new Vector3(1.5f, 0.7f, 1.5f), Sunflower);
        b.Tube(new Vector3(0, 3.22f, -0.2f), new Vector3(1.46f, 0.12f, 1.46f), Canvas);
        b.Ball(new Vector3(0, 3.68f, -0.2f), 0.1f, Sunflower);
    }

    private static void HatStall(MeshBuilder b, Node3D node)
    {
        // A sunflower-yellow stall with bucket hats on a pegboard and in stacks on the table.
        const float w = 2.6f, d = 2.4f;
        Canopy(b, node, w, d, 2.25f, Sunflower, Canvas, "Hats", Pink, Canvas);
        b.Box(new Vector3(0, 1.2f, -d / 2), new Vector3(w, 2.0f, 0.05f), Wood.Lightened(0.25f));
        for (var row = 0; row < 2; row++)
        {
            for (var i = 0; i < 4; i++)
            {
                var colour = new Color(Simulation.People.Crowd.HatStallColours[((row * 4) + i) % Simulation.People.Crowd.HatStallColours.Length]);
                BucketHat(b, new Vector3(-0.9f + (i * 0.6f), 1.1f + (row * 0.6f), (-d / 2) + 0.2f), 0.4f, colour);
            }
        }

        b.Box(new Vector3(-0.3f, 0.8f, 0.8f), new Vector3(1.7f, 0.06f, 0.7f), Wood);
        b.Box(new Vector3(-0.3f, 0.4f, 1.13f), new Vector3(1.66f, 0.8f, 0.04f), Pink);
        for (var i = 0; i < 3; i++)
        {
            for (var k = 0; k < 3 - i; k++)
            {
                BucketHat(b, new Vector3(-0.85f + (i * 0.5f), 0.84f + (k * 0.09f), 0.8f), 0.36f, new Color(Simulation.People.Crowd.HatStallColours[(i + k) % 8]));
            }
        }

        BucketHat(b, new Vector3(0, 2.75f, 0), 1.6f, Pink);
    }

    /// <summary>A bucket hat: a soft crown with a brim that slopes down all round.</summary>
    private static void BucketHat(MeshBuilder b, Vector3 foot, float size, Color colour)
    {
        b.Cone(foot + new Vector3(0, 0.12f * size, 0), new Vector3(size, 0.5f * size, size), colour.Darkened(0.08f));
        b.Tube(foot + new Vector3(0, 0.3f * size, 0), new Vector3(0.62f * size, 0.4f * size, 0.62f * size), colour);
        b.Ball(foot + new Vector3(0, 0.5f * size, 0), new Vector3(0.62f * size, 0.16f * size, 0.62f * size), colour);
    }

    private static void MerchTent(MeshBuilder b, Node3D node)
    {
        // A pink and white marquee with T-shirts on a rail, posters on the back wall and bags on the counter.
        const float w = 4.6f, d = 3.6f, eaves = 2.4f, ridge = 3.3f;
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

        b.Box(new Vector3(0, eaves / 2, -d / 2), new Vector3(w, eaves, 0.04f), Canvas);
        b.Box(new Vector3(-w / 2, eaves / 2, -0.9f), new Vector3(0.04f, eaves, 1.8f), Canvas);
        b.Box(new Vector3(w / 2, eaves / 2, -0.9f), new Vector3(0.04f, eaves, 1.8f), Canvas);
        b.Box(new Vector3(0, eaves - 0.1f, (d / 2) + 0.05f), new Vector3(w, 0.42f, 0.05f), Black);
        Painted(node, "Merchandise", new Vector3(0, eaves - 0.1f, (d / 2) + 0.09f), w - 0.6f, 0.3f, Sunflower);

        // T-shirts on a rail across the back, and three posters.
        b.Tube(new Vector3(0, 2.05f, -1.3f), new Vector3(0.04f, w - 0.4f, 0.04f), Steel, new Vector3(0, 0, 90));
        var shirts = new[] { Pink, Black, Sunflower, Canvas, Teal, Lilac };
        for (var i = 0; i < 6; i++)
        {
            var x = -1.75f + (i * 0.7f);
            b.Box(new Vector3(x, 1.6f, -1.3f), new Vector3(0.46f, 0.6f, 0.06f), shirts[i]);
            b.Box(new Vector3(x, 1.82f, -1.3f), new Vector3(0.74f, 0.2f, 0.06f), shirts[i]);
            b.Ball(new Vector3(x, 1.62f, -1.26f), new Vector3(0.2f, 0.2f, 0.03f), i % 2 == 0 ? Sunflower : Pink);
        }

        foreach (var (x, colour) in new[] { (-1.5f, Sunflower), (0f, Pink), (1.5f, Teal) })
        {
            b.Box(new Vector3(x, 0.85f, (-d / 2) + 0.04f), new Vector3(0.6f, 0.85f, 0.02f), colour);
            b.Ball(new Vector3(x, 0.95f, (-d / 2) + 0.06f), new Vector3(0.34f, 0.34f, 0.02f), Canvas);
        }

        Counter(b, w - 0.6f, 1.2f, Wood, Pink);
        foreach (var (x, colour) in new[] { (-1.6f, Canvas), (-0.2f, Sunflower), (0.3f, Canvas), (1.7f, Pink) })
        {
            CarrierBag(b, new Vector3(x, 1.04f, 1.2f), 0.4f, colour);
        }

        b.Box(new Vector3(0.9f, 1.1f, 1.2f), new Vector3(0.4f, 0.12f, 0.34f), Black);
        b.Box(new Vector3(-0.9f, 1.1f, 1.2f), new Vector3(0.4f, 0.1f, 0.34f), Teal);
    }

    /// <summary>
    /// Something bought at a stall, made to sit in a hand: its bottom is at the hand, and it stands
    /// upright. A bag hangs below the hand instead.
    /// </summary>
    public static MeshInstance3D CarriedItem(Carried item, int seed)
    {
        var b = new MeshBuilder();
        var up = new Vector3(0, 0.05f, 0.03f);
        switch (item)
        {
            case Carried.Pint:
                b.Tube(up + new Vector3(0, 0.1f, 0), new Vector3(0.11f, 0.2f, 0.11f), new Color("#E3A02C"));
                b.Tube(up + new Vector3(0, 0.215f, 0), new Vector3(0.115f, 0.04f, 0.115f), Canvas);
                break;
            case Carried.Cup:
                b.Cone(up + new Vector3(0, 0.07f, 0), new Vector3(0.13f, 0.26f, 0.13f), seed % 2 == 0 ? Tomato : Sky, new Vector3(180, 0, 0));
                b.Tube(up + new Vector3(0, 0.19f, 0), new Vector3(0.13f, 0.02f, 0.13f), Canvas);
                b.Tube(up + new Vector3(0.02f, 0.26f, 0), new Vector3(0.018f, 0.16f, 0.018f), Canvas, new Vector3(0, 0, -12));
                break;
            case Carried.Coffee:
                b.Cone(up + new Vector3(0, 0.06f, 0), new Vector3(0.13f, 0.24f, 0.13f), Cream, new Vector3(180, 0, 0));
                b.Tube(up + new Vector3(0, 0.1f, 0), new Vector3(0.125f, 0.07f, 0.125f), Coffee);
                b.Tube(up + new Vector3(0, 0.185f, 0), new Vector3(0.135f, 0.025f, 0.135f), Black);
                break;
            case Carried.Slush:
                b.Cone(up + new Vector3(0, 0.07f, 0), new Vector3(0.14f, 0.26f, 0.14f), Canvas, new Vector3(180, 0, 0));
                b.Ball(up + new Vector3(0, 0.21f, 0), new Vector3(0.15f, 0.12f, 0.15f), seed % 2 == 0 ? new Color("#3C8DD8") : Tomato);
                b.Tube(up + new Vector3(0.02f, 0.3f, 0), new Vector3(0.018f, 0.16f, 0.018f), Sunflower, new Vector3(0, 0, -12));
                break;
            case Carried.Cocktail:
                b.Tube(up + new Vector3(0, 0.06f, 0), new Vector3(0.025f, 0.12f, 0.025f), Glass);
                b.Cone(up + new Vector3(0, 0.18f, 0), new Vector3(0.17f, 0.14f, 0.17f), new Color("#FF7A45"), new Vector3(180, 0, 0));
                b.Cone(up + new Vector3(0.05f, 0.31f, 0), new Vector3(0.13f, 0.05f, 0.13f), Pink, new Vector3(0, 0, -20));
                b.Tube(up + new Vector3(0.04f, 0.26f, 0), new Vector3(0.012f, 0.1f, 0.012f), WoodDark, new Vector3(0, 0, -20));
                break;
            case Carried.HotDog:
                b.Pill(up + new Vector3(0, 0.06f, 0.02f), new Vector3(0.1f, 0.26f, 0.09f), Bun, new Vector3(90, 0, 0));
                b.Pill(up + new Vector3(0, 0.1f, 0.02f), new Vector3(0.06f, 0.3f, 0.06f), new Color("#B5482E"), new Vector3(90, 0, 0));
                b.Box(up + new Vector3(0, 0.135f, 0.02f), new Vector3(0.02f, 0.01f, 0.2f), Sunflower);
                break;
            case Carried.Burger:
                b.Tube(up + new Vector3(0, 0.035f, 0), new Vector3(0.19f, 0.04f, 0.19f), Bun);
                b.Tube(up + new Vector3(0, 0.07f, 0), new Vector3(0.2f, 0.035f, 0.2f), new Color("#6B3A22"));
                b.Tube(up + new Vector3(0, 0.092f, 0), new Vector3(0.21f, 0.012f, 0.21f), Lettuce);
                b.Ball(up + new Vector3(0, 0.1f, 0), new Vector3(0.19f, 0.12f, 0.19f), Bun);
                break;
            case Carried.Fries:
                b.Cone(up + new Vector3(0, 0.07f, 0), new Vector3(0.15f, 0.22f, 0.1f), Tomato, new Vector3(180, 0, 0));
                for (var i = 0; i < 4; i++)
                {
                    b.Box(up + new Vector3(-0.045f + (i * 0.03f), 0.2f + (i % 2 * 0.02f), 0), new Vector3(0.022f, 0.12f, 0.022f), new Color("#F2CB4A"), new Vector3(0, 0, (i - 1.5f) * -8f));
                }

                break;
            case Carried.Pizza:
                b.Box(up + new Vector3(0, 0.02f, 0.04f), new Vector3(0.22f, 0.012f, 0.26f), Canvas);
                b.Cone(up + new Vector3(0, 0.035f, 0.04f), new Vector3(0.2f, 0.24f, 0.03f), new Color("#E9C46A"), new Vector3(90, 0, 0));
                b.Cone(up + new Vector3(0, 0.045f, 0.05f), new Vector3(0.15f, 0.18f, 0.02f), Tomato, new Vector3(90, 0, 0));
                break;
            case Carried.Noodles:
                b.Box(up + new Vector3(0, 0.075f, 0), new Vector3(0.13f, 0.15f, 0.11f), Canvas);
                b.Box(up + new Vector3(0, 0.075f, 0), new Vector3(0.135f, 0.05f, 0.115f), Tomato);
                b.Ball(up + new Vector3(0, 0.155f, 0), new Vector3(0.11f, 0.05f, 0.09f), new Color("#E9C46A"));
                b.Box(up + new Vector3(0.03f, 0.2f, 0), new Vector3(0.012f, 0.16f, 0.012f), WoodDark, new Vector3(0, 0, -20));
                break;
            case Carried.IceCream:
                b.Cone(up + new Vector3(0, 0.07f, 0), new Vector3(0.1f, 0.2f, 0.1f), new Color("#D9A65A"), new Vector3(180, 0, 0));
                b.Ball(up + new Vector3(0, 0.2f, 0), 0.13f, seed % 3 == 0 ? Cream : seed % 3 == 1 ? new Color("#F7B9CF") : new Color("#8A5A32"));
                break;
            default:
                CarrierBag(b, new Vector3(0, -0.4f, 0), 0.42f, seed % 3 == 0 ? Pink : seed % 3 == 1 ? Canvas : Sunflower);
                break;
        }

        var node = b.BuildNode("Held");
        // A little bigger than life, so it can be made out from across the field.
        node.Scale = Vector3.One * (item == Carried.Bag ? 1.15f : 1.5f);
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    /// <summary>A paper carrier bag with two string handles and the festival's sunflower on the side.</summary>
    public static void CarrierBag(MeshBuilder b, Vector3 foot, float size, Color colour)
    {
        b.Box(foot + new Vector3(0, 0.4f * size, 0), new Vector3(0.7f * size, 0.8f * size, 0.3f * size), colour);
        b.Ball(foot + new Vector3(0, 0.42f * size, 0.16f * size), new Vector3(0.3f * size, 0.3f * size, 0.03f * size), colour == Pink ? Sunflower : Pink);
        foreach (var z in new[] { -0.1f, 0.1f })
        {
            b.Tube(foot + new Vector3(0, 0.84f * size, z * size), new Vector3(0.36f * size, 0.03f * size, 0.36f * size), Rope, new Vector3(90, 0, 0));
        }
    }
}
