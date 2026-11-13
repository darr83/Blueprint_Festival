using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

public static partial class SiteProps
{
    /// <summary>The green of first aid. Medical things are marked with a green cross, never a red one.</summary>
    public static readonly Color FirstAidGreen = new("#1E9E52");

    private static readonly Color MedicGreen = new("#1F6B45");
    private static readonly Color HiVis = new("#E4F03C");
    private static readonly Color Reflective = new("#D9DEE2");

    /// <summary>
    /// One member of staff in uniform, facing south (+Z). Ticket staff wear the festival's pink
    /// with a sunflower cap and lanyard, security wear black under a hi-vis tabard, and first-aiders
    /// wear white and green with a green cross and carry a kit bag.
    /// </summary>
    public static Node3D Staff(StaffKind kind, int seed)
    {
        var node = new Node3D { Name = Catalogue.Of(kind).Name };
        var look = LookGenerator.FestivalGoer(seed + 900, AgeGroup.Adult) with { Poncho = null, Sunglasses = false, TopDetail = null, Legs = LegStyle.Trousers };
        look = kind switch
        {
            StaffKind.TicketStaff => look with
            {
                Top = TopStyle.TShirt, TopColour = "#E94F7B", LegColour = "#34495E", Feet = Footwear.Trainers, FeetColour = "#F2F2F2", Hat = Hat.Cap, HatColour = "#F6B93B",
            },
            StaffKind.Security => look with
            {
                Top = TopStyle.LongSleeve, TopColour = "#26292E", LegColour = "#26292E", Feet = Footwear.Boots, FeetColour = "#1B1B1E", Hat = Hat.Cap, HatColour = "#26292E",
                Build = System.Math.Max(1.1, look.Build), Height = System.Math.Max(1.0, look.Height),
            },
            _ => look with
            {
                Top = TopStyle.TShirt, TopColour = "#FFFFFF", LegColour = "#1F6B45", Feet = Footwear.Boots, FeetColour = "#26292E", Hat = Hat.None,
            },
        };
        var person = PersonView.Build(look);
        node.AddChild(person);

        var h = person.StandingHeight / 1.6f;
        var b = new MeshBuilder();
        switch (kind)
        {
            case StaffKind.TicketStaff:
                // A lanyard with a pass on the chest, and a ticket scanner in a belt pouch.
                b.Box(new Vector3(-0.045f, 1.0f * h, 0.128f), new Vector3(0.022f, 0.2f * h, 0.012f), Sunflower, new Vector3(0, 0, -14));
                b.Box(new Vector3(0.045f, 1.0f * h, 0.128f), new Vector3(0.022f, 0.2f * h, 0.012f), Sunflower, new Vector3(0, 0, 14));
                b.Box(new Vector3(0, 0.87f * h, 0.135f), new Vector3(0.09f, 0.11f * h, 0.015f), Colors.White);
                b.Box(new Vector3(0, 0.89f * h, 0.145f), new Vector3(0.07f, 0.03f * h, 0.01f), Pink);
                b.Box(new Vector3(0.17f, 0.68f * h, 0.07f), new Vector3(0.07f, 0.12f * h, 0.07f), Black);
                break;

            case StaffKind.Security:
                // A hi-vis tabard with two reflective bands, and a radio on the shoulder.
                b.Box(new Vector3(0, 0.9f * h, 0), new Vector3(0.37f, 0.42f * h, 0.275f), HiVis);
                b.Box(new Vector3(0, 0.8f * h, 0), new Vector3(0.378f, 0.045f * h, 0.283f), Reflective);
                b.Box(new Vector3(0, 0.95f * h, 0), new Vector3(0.378f, 0.045f * h, 0.283f), Reflective);
                b.Box(new Vector3(0.12f, 1.05f * h, 0.14f), new Vector3(0.05f, 0.09f * h, 0.03f), Black);
                b.Tube(new Vector3(0.135f, 1.13f * h, 0.14f), new Vector3(0.012f, 0.08f * h, 0.012f), Black);
                break;

            default:
                // A green cross front and back, green shoulders, and a first aid kit bag at the hip.
                foreach (var z in new[] { 0.125f, -0.125f })
                {
                    b.Box(new Vector3(0, 0.92f * h, z), new Vector3(0.065f, 0.19f * h, 0.04f), FirstAidGreen);
                    b.Box(new Vector3(0, 0.92f * h, z), new Vector3(0.19f, 0.065f * h, 0.04f), FirstAidGreen);
                }

                b.Box(new Vector3(0, 1.085f * h, 0), new Vector3(0.4f, 0.05f * h, 0.2f), MedicGreen);
                b.Box(new Vector3(-0.25f, 0.62f * h, 0), new Vector3(0.11f, 0.2f * h, 0.26f), MedicGreen);
                b.Box(new Vector3(-0.31f, 0.62f * h, 0), new Vector3(0.012f, 0.13f * h, 0.045f), Colors.White);
                b.Box(new Vector3(-0.31f, 0.62f * h, 0), new Vector3(0.012f, 0.045f * h, 0.13f), Colors.White);
                b.Box(new Vector3(-0.2f, 0.9f * h, 0), new Vector3(0.025f, 0.4f * h, 0.03f), MedicGreen, new Vector3(0, 0, -12));
                break;
        }

        node.AddChild(b.BuildNode("Uniform"));
        return node;
    }

    /// <summary>A green cross on white, drawn flat facing south. Size is the length of an arm of the cross.</summary>
    private static void GreenCross(MeshBuilder b, Vector3 at, float size, bool board = true)
    {
        if (board)
        {
            b.Box(at, new Vector3(size * 1.5f, size * 1.5f, 0.03f), Colors.White);
        }

        b.Box(at + new Vector3(0, 0, 0.02f), new Vector3(size * 0.34f, size, 0.02f), FirstAidGreen);
        b.Box(at + new Vector3(0, 0, 0.02f), new Vector3(size, size * 0.34f, 0.02f), FirstAidGreen);
    }

    /// <summary>
    /// The first aid tent, open to the south (+Z): a white frame tent 4 m wide and 3 m deep with a
    /// green cross on the gable and on a flag, and two camp beds inside.
    /// </summary>
    public static Node3D FirstAidTent()
    {
        var node = new Node3D { Name = "First aid tent" };
        var b = new MeshBuilder();
        const float w = 4f, d = 3f, eaves = 2.0f, ridge = 2.9f;
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            foreach (var z in new[] { -d / 2, d / 2 })
            {
                b.Tube(new Vector3(x, eaves / 2, z), new Vector3(0.07f, eaves, 0.07f), Steel);
            }
        }

        // Back and side walls, and a pitched roof in two slopes.
        b.Box(new Vector3(0, eaves / 2, -d / 2), new Vector3(w, eaves, 0.04f), Colors.White);
        b.Box(new Vector3(-w / 2, eaves / 2, -1.0f), new Vector3(0.04f, eaves, 1.0f), Colors.White);
        b.Box(new Vector3(w / 2, eaves / 2, -1.0f), new Vector3(0.04f, eaves, 1.0f), Colors.White);
        var slope = Mathf.RadToDeg(Mathf.Atan2(ridge - eaves, w / 2));
        var length = Mathf.Sqrt(((ridge - eaves) * (ridge - eaves)) + (w * w / 4)) + 0.2f;
        b.Box(new Vector3(-w / 4, (eaves + ridge) / 2, 0), new Vector3(length, 0.05f, d + 0.3f), Colors.White, new Vector3(0, 0, slope));
        b.Box(new Vector3(w / 4, (eaves + ridge) / 2, 0), new Vector3(length, 0.05f, d + 0.3f), Colors.White, new Vector3(0, 0, -slope));
        b.Box(new Vector3(0, eaves - 0.14f, d / 2), new Vector3(w, 0.28f, 0.04f), FirstAidGreen);
        Painted(node, "FIRST AID", new Vector3(0, eaves - 0.14f, (d / 2) + 0.03f), w - 1.2f, 0.2f, Colors.White);
        GreenCross(b, new Vector3(0, eaves + 0.42f, (d / 2) + 0.16f), 0.5f);

        // Two camp beds with green blankets, and a kit box.
        foreach (var x in new[] { -1.15f, 1.15f })
        {
            b.Box(new Vector3(x, 0.42f, -0.35f), new Vector3(0.8f, 0.08f, 1.9f), Canvas);
            b.Box(new Vector3(x, 0.47f, -0.05f), new Vector3(0.82f, 0.05f, 1.2f), MedicGreen);
            b.Ball(new Vector3(x, 0.5f, -1.1f), new Vector3(0.5f, 0.14f, 0.3f), Colors.White);
            foreach (var z in new[] { -1.2f, 0.5f })
            {
                b.Box(new Vector3(x - 0.35f, 0.2f, z), new Vector3(0.05f, 0.4f, 0.05f), SteelDark);
                b.Box(new Vector3(x + 0.35f, 0.2f, z), new Vector3(0.05f, 0.4f, 0.05f), SteelDark);
            }
        }

        b.Box(new Vector3(0, 0.25f, -1.1f), new Vector3(0.6f, 0.5f, 0.4f), MedicGreen);
        b.Box(new Vector3(0, 0.3f, -0.89f), new Vector3(0.07f, 0.22f, 0.01f), Colors.White);
        b.Box(new Vector3(0, 0.3f, -0.89f), new Vector3(0.22f, 0.07f, 0.01f), Colors.White);

        // A flag on a pole so the tent can be found from across the field.
        b.Tube(new Vector3((w / 2) + 0.25f, 2.2f, d / 2), new Vector3(0.06f, 4.4f, 0.06f), Steel);
        GreenCross(b, new Vector3((w / 2) + 0.82f, 3.95f, d / 2), 0.55f);
        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>The marker over someone who needs first aid: a green cross on a white board. It sits this high above the ground.</summary>
    public static MeshInstance3D HelpMarker(float headHeight)
    {
        var b = new MeshBuilder();
        var top = headHeight + 0.7f;
        b.Ball(new Vector3(0, top, 0), new Vector3(0.62f, 0.62f, 0.16f), Colors.White);
        b.Cone(new Vector3(0, top - 0.38f, 0), new Vector3(0.2f, 0.22f, 0.1f), Colors.White, new Vector3(180, 0, 0));
        GreenCross(b, new Vector3(0, top, 0.07f), 0.36f, board: false);
        var node = b.BuildNode("Help");
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }
}
