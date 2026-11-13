using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Builds the acts: a style A person in a stage outfit with their instrument. Acts are made up;
/// no real artist is ever shown or named.
/// </summary>
public static class Performers
{
    private static readonly Color Black = new("#26292E");
    private static readonly Color Steel = new("#B9C0C6");
    private static readonly Color Wood = new("#B9783A");
    private static readonly Color WoodDark = new("#6F4423");
    private static readonly Color Cream = new("#F6F0E2");

    private static readonly string[] Outfits = { "#E94F7B", "#F6B93B", "#26292E", "#F5F5F5", "#7B4FE9", "#2FB7A8", "#D9483B" };
    private static readonly string[] Guitars = { "#D9483B", "#F6B93B", "#3C8DAD", "#F5F5F5", "#2FB7A8" };

    public static string Name(BandRole role) => role switch
    {
        BandRole.Keys => "Keyboard player",
        BandRole.Dj => "DJ",
        BandRole.Bassist => "Bass player",
        _ => role.ToString(),
    };

    /// <summary>One performer facing south, with whatever they play. The same seed always gives the same person.</summary>
    public static Node3D Build(BandRole role, int seed)
    {
        var node = new Node3D { Name = Name(role) };
        seed = System.Math.Abs(seed % 100000);
        var outfit = Outfits[(seed * 7 + (int)role) % Outfits.Length];
        var look = LookGenerator.FestivalGoer(seed * 31 + 900, AgeGroup.Adult) with
        {
            Poncho = null,
            Hat = role == BandRole.Fiddler ? Hat.Cowboy : role == BandRole.Dj ? Hat.Cap : Hat.None,
            HatColour = role == BandRole.Dj ? "#26292E" : "#D4B77A",
            Sunglasses = role is BandRole.Dj or BandRole.Bassist,
            Top = role == BandRole.Singer ? TopStyle.Vest : role == BandRole.Drummer ? TopStyle.Vest : TopStyle.TShirt,
            TopColour = outfit,
            TopDetail = role == BandRole.Singer ? "#F6B93B" : null,
            Legs = LegStyle.Trousers,
            LegColour = outfit == "#26292E" ? "#3F5B8C" : "#26292E",
            Feet = Footwear.Boots,
            FeetColour = "#2E2E33",
        };
        var person = PersonView.Build(look);
        person.Name = "Person";
        person.SetPose(role == BandRole.Singer ? Pose.Cheering : Pose.Standing);
        node.AddChild(person);

        var b = new MeshBuilder();
        var h = person.StandingHeight / 1.6f;
        switch (role)
        {
            case BandRole.Singer:
                Stand(b, new Vector3(0.05f, 0, 0.42f), 1.42f * h);
                break;

            case BandRole.Guitarist:
            case BandRole.Bassist:
            {
                var colour = new Color(Guitars[(seed + (int)role) % Guitars.Length]);
                var body = new Vector3(0.1f, 0.9f * h, 0.2f);
                b.Ball(body, new Vector3(0.36f, 0.42f, 0.1f), colour, new Vector3(0, 0, -35));
                b.Ball(body + new Vector3(-0.12f, 0.12f, 0), new Vector3(0.26f, 0.3f, 0.1f), colour, new Vector3(0, 0, -35));
                b.Tube(body + new Vector3(0, 0, 0.05f), new Vector3(0.1f, 0.02f, 0.1f), Black, new Vector3(90, 0, 0));
                var length = role == BandRole.Bassist ? 0.78f : 0.62f;
                b.Box(body + new Vector3(-0.2f - (length * 0.4f), 0.14f + (length * 0.29f), 0), new Vector3(length, 0.05f, 0.04f), WoodDark, new Vector3(0, 0, -35));
                b.Box(body + new Vector3(-0.2f - (length * 0.84f), 0.14f + (length * 0.6f), 0), new Vector3(0.14f, 0.08f, 0.04f), colour, new Vector3(0, 0, -35));
                // The strap over the shoulder.
                b.Box(new Vector3(0.02f, 1.12f * h, 0.13f), new Vector3(0.05f, 0.5f, 0.02f), Black, new Vector3(0, 0, 38));
                break;
            }

            case BandRole.Fiddler:
                b.Ball(new Vector3(-0.2f, 1.3f * h, 0.16f), new Vector3(0.16f, 0.07f, 0.28f), Wood, new Vector3(0, 25, 0));
                b.Box(new Vector3(-0.3f, 1.3f * h, 0.38f), new Vector3(0.03f, 0.03f, 0.24f), WoodDark, new Vector3(0, 25, 0));
                b.Box(new Vector3(-0.08f, 1.34f * h, 0.32f), new Vector3(0.5f, 0.015f, 0.015f), Cream, new Vector3(0, 20, 20));
                break;

            case BandRole.Drummer:
                // A stool to sit on is hidden by the kit: bass drum, snare, two toms, hi-hat and cymbals.
                b.Tube(new Vector3(0, 0.36f, 0.75f), new Vector3(0.66f, 0.42f, 0.66f), new Color("#E94F7B"), new Vector3(90, 0, 0));
                b.Tube(new Vector3(0, 0.36f, 0.97f), new Vector3(0.6f, 0.02f, 0.6f), Cream, new Vector3(90, 0, 0));
                b.Tube(new Vector3(-0.42f, 0.62f, 0.5f), new Vector3(0.36f, 0.16f, 0.36f), Cream);
                b.Tube(new Vector3(-0.2f, 0.86f, 0.78f), new Vector3(0.3f, 0.2f, 0.3f), new Color("#E94F7B"), new Vector3(-20, 0, 0));
                b.Tube(new Vector3(0.2f, 0.86f, 0.78f), new Vector3(0.32f, 0.22f, 0.32f), new Color("#E94F7B"), new Vector3(-20, 0, 0));
                b.Tube(new Vector3(0.52f, 0.5f, 0.45f), new Vector3(0.42f, 0.36f, 0.42f), new Color("#E94F7B"));
                foreach (var (x, z, y, size) in new[] { (-0.72f, 0.55f, 0.95f, 0.36f), (-0.5f, 0.95f, 1.25f, 0.46f), (0.6f, 0.9f, 1.2f, 0.5f) })
                {
                    b.Tube(new Vector3(x, y / 2, z), new Vector3(0.03f, y, 0.03f), Steel);
                    b.Tube(new Vector3(x, y, z), new Vector3(size, 0.02f, size), new Color("#F6B93B"), new Vector3(8, 0, 6));
                }

                break;

            case BandRole.Keys:
                b.Box(new Vector3(0, 0.95f, 0.42f), new Vector3(1.15f, 0.09f, 0.34f), Black);
                b.Box(new Vector3(0, 1.0f, 0.47f), new Vector3(1.05f, 0.02f, 0.2f), Cream);
                b.Box(new Vector3(0, 0.47f, 0.42f), new Vector3(1.1f, 0.04f, 0.04f), Steel, new Vector3(0, 0, 40));
                b.Box(new Vector3(0, 0.47f, 0.42f), new Vector3(1.1f, 0.04f, 0.04f), Steel, new Vector3(0, 0, -40));
                break;

            case BandRole.Dj:
                b.Box(new Vector3(0, 0.5f, 0.5f), new Vector3(1.7f, 1.0f, 0.6f), Black);
                b.Box(new Vector3(0, 0.6f, 0.81f), new Vector3(1.5f, 0.6f, 0.02f), new Color("#E94F7B"));
                foreach (var x in new[] { -0.5f, 0.5f })
                {
                    b.Tube(new Vector3(x, 1.02f, 0.5f), new Vector3(0.42f, 0.03f, 0.42f), new Color("#3A3F46"));
                    b.Tube(new Vector3(x, 1.04f, 0.5f), new Vector3(0.14f, 0.02f, 0.14f), new Color("#F6B93B"));
                }

                b.Box(new Vector3(0, 1.03f, 0.5f), new Vector3(0.3f, 0.05f, 0.4f), Steel);
                break;
        }

        if (!b.IsEmpty)
        {
            node.AddChild(b.BuildNode("Instrument"));
        }

        return node;
    }

    private static void Stand(MeshBuilder b, Vector3 foot, float height)
    {
        b.Tube(foot + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f), Black);
        b.Tube(foot + new Vector3(0, height / 2, 0), new Vector3(0.035f, height, 0.035f), Steel);
        b.Ball(foot + new Vector3(0, height + 0.04f, -0.05f), 0.1f, Black);
    }
}
