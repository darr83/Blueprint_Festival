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

    private static readonly string[] Guitars = { "#D9483B", "#F6B93B", "#3C8DAD", "#F5F5F5", "#2FB7A8" };

    public static string Name(BandRole role) => role switch
    {
        BandRole.Keys => "Keyboard player",
        BandRole.Dj => "DJ",
        BandRole.Bassist => "Bass player",
        _ => role.ToString(),
    };

    /// <summary>
    /// One performer facing south, dressed for their music (see <see cref="StarLooks"/>), with
    /// whatever they play. The same seed always gives the same instrument. One of the entertainers
    /// in the fields (busking) plays an acoustic guitar, and a painted street piano for keys.
    /// </summary>
    public static Node3D Build(BandRole role, PersonLook look, int seed, bool busking = false)
    {
        var node = new Node3D { Name = Name(role) };
        seed = System.Math.Abs(seed % 100000);
        var person = PersonView.Build(look);
        person.Name = "Person";
        person.SetPose(Pose.Performing);
        if (role == BandRole.Drummer)
        {
            person.Position = new Vector3(0, -DrummerSits, -0.05f);
        }
        node.AddChild(person);

        var b = new MeshBuilder();
        var h = person.StandingHeight / 1.6f;
        switch (role)
        {
            case BandRole.Singer:
                Stand(b, new Vector3(-0.04f, 0, 0.42f), 1.24f * h);
                break;

            case BandRole.Guitarist:
            case BandRole.Bassist:
            {
                var colour = busking ? Wood : new Color(Guitars[(seed + (int)role) % Guitars.Length]);
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
                // The fiddle under the chin; the bow is held in the right hand (see Bow).
                b.Ball(new Vector3(-0.2f, 1.3f * h, 0.16f), new Vector3(0.16f, 0.07f, 0.28f), Wood, new Vector3(0, 25, 0));
                b.Box(new Vector3(-0.3f, 1.3f * h, 0.38f), new Vector3(0.03f, 0.03f, 0.24f), WoodDark, new Vector3(0, 25, 0));
                break;

            case BandRole.Drummer:
                BuildKit(node);
                break;

            case BandRole.Keys when busking:
                StreetPiano(b);
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

    /// <summary>
    /// An upright street piano for the pianist to stand at, facing south (+Z) with its back to the
    /// people listening: painted festival pink with sunflowers on the back, low enough to see the pianist over, the keys at the height
    /// the keyboard player's hands go to.
    /// </summary>
    private static void StreetPiano(MeshBuilder b)
    {
        var pink = new Color("#E94F7B");
        var deep = new Color("#B83A60");
        var sunflower = new Color("#F6B93B");
        b.Box(new Vector3(0, 0.6f, 0.82f), new Vector3(1.45f, 1.08f, 0.5f), pink);
        b.Box(new Vector3(0, 1.16f, 0.8f), new Vector3(1.52f, 0.05f, 0.58f), deep);
        b.Box(new Vector3(0, 0.93f, 0.49f), new Vector3(1.42f, 0.08f, 0.2f), pink);
        b.Box(new Vector3(0, 0.98f, 0.47f), new Vector3(1.28f, 0.03f, 0.16f), Cream);
        for (var i = 0; i < 18; i++)
        {
            if (i % 7 is 2 or 6)
            {
                continue;
            }

            b.Box(new Vector3(-0.6f + (i * 0.07f), 1.0f, 0.43f), new Vector3(0.03f, 0.025f, 0.08f), Black);
        }

        b.Box(new Vector3(0, 1.06f, 0.58f), new Vector3(1.3f, 0.12f, 0.03f), deep);
        foreach (var x in new[] { -0.64f, 0.64f })
        {
            b.Box(new Vector3(x, 0.32f, 0.5f), new Vector3(0.08f, 0.64f, 0.18f), deep);
        }

        // Sunflowers painted on the back, for the people listening.
        foreach (var (x, y, size) in new[] { (-0.42f, 0.7f, 0.32f), (0.14f, 0.88f, 0.24f), (0.46f, 0.5f, 0.28f), (-0.05f, 0.36f, 0.18f) })
        {
            b.Ball(new Vector3(x, y, 1.08f), new Vector3(size, size, 0.02f), sunflower);
            b.Ball(new Vector3(x, y, 1.09f), new Vector3(size * 0.38f, size * 0.38f, 0.02f), WoodDark);
        }
    }

    /// <summary>How far below standing the drummer sits on the stool, in metres.</summary>
    public const float DrummerSits = 0.15f;

    /// <summary>The middle of the top of each drum and cymbal, where a stick strikes it, the drummer facing south (+Z).</summary>
    public static Vector3 DrumTop(Drum drum) => drum switch
    {
        Drum.Snare => new Vector3(-0.24f, 0.70f, 0.45f),
        Drum.HighTom => new Vector3(-0.17f, 0.91f, 0.64f),
        Drum.LowTom => new Vector3(0.17f, 0.91f, 0.64f),
        Drum.FloorTom => new Vector3(0.42f, 0.66f, 0.6f),
        Drum.HiHat => new Vector3(0.47f, 0.88f, 0.32f),
        _ => new Vector3(0.5f, 1.18f, 0.62f),
    };

    /// <summary>A drum stick, lying along +Z from the hand, one metre long before it is stretched to fit.</summary>
    public static Node3D Stick()
    {
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0, 0.5f), new Vector3(0.04f, 1f, 0.04f), Cream, new Vector3(90, 0, 0));
        return b.BuildNode("Stick");
    }

    /// <summary>A fiddle bow, lying along +Z from the hand, one metre long before it is stretched to fit.</summary>
    public static Node3D Bow()
    {
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0, 0.5f), new Vector3(0.016f, 1f, 0.016f), WoodDark, new Vector3(90, 0, 0));
        b.Box(new Vector3(0, -0.02f, 0.5f), new Vector3(0.008f, 0.02f, 0.95f), Cream);
        return b.BuildNode("Bow");
    }

    /// <summary>
    /// The drum kit, each drum and cymbal a node of its own (named after it) so it can shake when it
    /// is hit: bass drum, snare, two toms, a floor tom, hi-hat, crash and ride, and a stool.
    /// </summary>
    private static void BuildKit(Node3D node)
    {
        var pink = new Color("#E94F7B");
        var brass = new Color("#F6B93B");
        var kit = new Node3D { Name = "Kit" };
        node.AddChild(kit);

        void Piece(string name, Vector3 at, System.Action<MeshBuilder> build)
        {
            var b = new MeshBuilder();
            build(b);
            kit.AddChild(b.BuildNode(name, at));
        }

        // The stool, half hidden behind the kit.
        Piece("Stool", new Vector3(0, 0, -0.05f), b =>
        {
            b.Tube(new Vector3(0, 0.44f, 0), new Vector3(0.34f, 0.07f, 0.34f), Black);
            b.Tube(new Vector3(0, 0.22f, 0), new Vector3(0.04f, 0.44f, 0.04f), Steel);
        });
        Piece("Kick", new Vector3(0, 0.33f, 0.8f), b =>
        {
            b.Tube(Vector3.Zero, new Vector3(0.62f, 0.4f, 0.62f), pink, new Vector3(90, 0, 0));
            b.Tube(new Vector3(0, 0, 0.21f), new Vector3(0.56f, 0.02f, 0.56f), Cream, new Vector3(90, 0, 0));
        });
        Piece(nameof(Drum.Snare), DrumTop(Drum.Snare) - new Vector3(0, 0.07f, 0), b =>
        {
            b.Tube(Vector3.Zero, new Vector3(0.34f, 0.13f, 0.34f), Cream);
            b.Tube(new Vector3(0, -0.36f, 0), new Vector3(0.03f, 0.6f, 0.03f), Steel);
        });
        foreach (var (drum, size) in new[] { (Drum.HighTom, 0.28f), (Drum.LowTom, 0.31f) })
        {
            Piece(drum.ToString(), DrumTop(drum) - new Vector3(0, 0.1f, -0.03f), b => b.Tube(Vector3.Zero, new Vector3(size, 0.2f, size), pink, new Vector3(-18, 0, 0)));
        }

        Piece(nameof(Drum.FloorTom), DrumTop(Drum.FloorTom) - new Vector3(0, 0.17f, 0), b =>
        {
            b.Tube(Vector3.Zero, new Vector3(0.4f, 0.34f, 0.4f), pink);
            b.Tube(new Vector3(0, 0.17f, 0), new Vector3(0.37f, 0.02f, 0.37f), Cream);
        });
        foreach (var (drum, size, tilt) in new[] { (Drum.HiHat, 0.36f, 0f), (Drum.Crash, 0.48f, 10f) })
        {
            var top = DrumTop(drum);
            Piece(drum.ToString() + " stand", new Vector3(top.X, top.Y / 2, top.Z + 0.04f), b => b.Tube(Vector3.Zero, new Vector3(0.03f, top.Y, 0.03f), Steel));
            Piece(drum.ToString(), top - new Vector3(0, 0.02f, 0), b =>
            {
                b.Tube(Vector3.Zero, new Vector3(size, 0.02f, size), brass, new Vector3(tilt, 0, -tilt / 2));
                if (drum == Drum.HiHat)
                {
                    b.Tube(new Vector3(0, -0.04f, 0), new Vector3(size, 0.02f, size), brass);
                }
            });
        }

        // The ride cymbal, on the far side.
        Piece("Ride", new Vector3(-0.6f, 1.08f, 0.62f), b =>
        {
            b.Tube(Vector3.Zero, new Vector3(0.5f, 0.02f, 0.5f), brass, new Vector3(8, 0, 6));
            b.Tube(new Vector3(0, -0.54f, 0), new Vector3(0.03f, 1.08f, 0.03f), Steel);
        });
    }

    private static void Stand(MeshBuilder b, Vector3 foot, float height)
    {
        b.Tube(foot + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f), Black);
        b.Tube(foot + new Vector3(0, height / 2, 0), new Vector3(0.035f, height, 0.035f), Steel);
        b.Ball(foot + new Vector3(0, height + 0.04f, -0.05f), 0.1f, Black);
    }
}
