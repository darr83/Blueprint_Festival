using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

public static partial class SiteProps
{
    /// <summary>
    /// How each entertainer dresses: their own uniform, the same for everyone hired to do that job.
    /// The guitarist wears a sunflower check shirt, jeans and a straw cowboy hat; the pianist a pink
    /// jacket over a white shirt with black trousers; the drummer a pink and white stripy top with
    /// black shorts and a sunflower cap on backwards; the DJ a purple hoodie, a sunflower beanie,
    /// sunglasses and headphones.
    /// </summary>
    public static PersonLook EntertainerLook(StaffKind kind, int seed)
    {
        var look = LookGenerator.FestivalGoer(seed + 1300, AgeGroup.Adult) with
        {
            Poncho = null, Sunglasses = false, TopDetail = null, Jacket = null, Sparkles = null, Chain = null, Headphones = null, Dungarees = false,
        };
        return kind switch
        {
            StaffKind.Pianist => look with
            {
                Top = TopStyle.LongSleeve, TopColour = "#FFFFFF", Jacket = "#E94F7B", Legs = LegStyle.Trousers, LegColour = "#26292E",
                Feet = Footwear.Boots, FeetColour = "#1B1B1E", Hat = Hat.None,
            },
            StaffKind.Drummer => look with
            {
                Top = TopStyle.Stripes, TopColour = "#E94F7B", TopDetail = "#FFFFFF", Legs = LegStyle.Shorts, LegColour = "#26292E",
                Feet = Footwear.Trainers, FeetColour = "#F2F2F2", Hat = Hat.CapBackwards, HatColour = "#F6B93B",
            },
            StaffKind.Dj => look with
            {
                Top = TopStyle.Hoodie, TopColour = "#6C4BA8", Legs = LegStyle.Trousers, LegColour = "#34495E", Feet = Footwear.Trainers, FeetColour = "#F2F2F2",
                Hat = Hat.Beanie, HatColour = "#F6B93B", Sunglasses = true, Headphones = "#26292E",
            },
            _ => look with
            {
                Top = TopStyle.Check, TopColour = "#F6B93B", TopDetail = "#B9783A", Legs = LegStyle.Trousers, LegColour = "#3F5B8C",
                Feet = Footwear.Boots, FeetColour = "#6F4423", Hat = Hat.Cowboy, HatColour = "#E8C77A",
            },
        };
    }

    /// <summary>
    /// One entertainer at their pitch, facing south (+Z): the performer with their instrument (a
    /// child named "Performer", which <see cref="BandMoves"/> plays), standing on a round festival
    /// mat, with whatever else they bring: an amp and an open guitar case, a potted sunflower by the
    /// piano, or a pair of little speakers either side of the DJ.
    /// </summary>
    public static Node3D Entertainer(StaffKind kind, int seed)
    {
        var node = new Node3D { Name = Catalogue.Of(kind).Name };
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0.012f, 0.3f), new Vector3(2.3f, 0.024f, 1.9f), Pink);
        b.Tube(new Vector3(0, 0.02f, 0.3f), new Vector3(2.05f, 0.024f, 1.65f), new Color("#F6F0E2"));
        switch (kind)
        {
            case StaffKind.Guitarist:
                // A little amp at their side, and the guitar case open on the mat in front.
                b.Box(new Vector3(-0.8f, 0.22f, 0.05f), new Vector3(0.42f, 0.4f, 0.26f), Black);
                b.Box(new Vector3(-0.8f, 0.2f, 0.185f), new Vector3(0.34f, 0.28f, 0.01f), new Color("#3A3F46"));
                b.Box(new Vector3(-0.8f, 0.39f, 0.185f), new Vector3(0.34f, 0.04f, 0.012f), new Color("#F6B93B"));
                b.Box(new Vector3(0.55f, 0.07f, 0.95f), new Vector3(0.42f, 0.1f, 1.0f), Black, new Vector3(0, 70, 0));
                b.Box(new Vector3(0.55f, 0.1f, 0.95f), new Vector3(0.34f, 0.04f, 0.9f), new Color("#B83A60"), new Vector3(0, 70, 0));
                break;

            case StaffKind.Pianist:
                // A potted sunflower beside the piano.
                b.Tube(new Vector3(1.0f, 0.16f, 0.6f), new Vector3(0.3f, 0.32f, 0.3f), new Color("#B9783A"));
                b.Tube(new Vector3(1.0f, 0.65f, 0.6f), new Vector3(0.035f, 0.7f, 0.035f), new Color("#4E8A3A"));
                b.Ball(new Vector3(1.0f, 1.05f, 0.66f), new Vector3(0.32f, 0.32f, 0.06f), Sunflower, new Vector3(-20, 0, 0));
                b.Ball(new Vector3(1.0f, 1.05f, 0.69f), new Vector3(0.13f, 0.13f, 0.05f), new Color("#6F4423"), new Vector3(-20, 0, 0));
                break;

            case StaffKind.Dj:
                // A small speaker on a stand either side of the booth.
                foreach (var x in new[] { -1.15f, 1.15f })
                {
                    b.Tube(new Vector3(x, 0.55f, 0.55f), new Vector3(0.035f, 1.1f, 0.035f), Steel);
                    b.Box(new Vector3(x, 1.28f, 0.55f), new Vector3(0.3f, 0.44f, 0.26f), Black);
                    b.Tube(new Vector3(x, 1.22f, 0.69f), new Vector3(0.2f, 0.01f, 0.2f), new Color("#3A3F46"), new Vector3(90, 0, 0));
                    b.Tube(new Vector3(x, 1.43f, 0.69f), new Vector3(0.08f, 0.01f, 0.08f), new Color("#3A3F46"), new Vector3(90, 0, 0));
                }

                break;
        }

        node.AddChild(b.BuildNode("Pitch"));
        var performer = Performers.Build(Busking.RoleOf(kind), EntertainerLook(kind, seed), seed, busking: true);
        performer.Name = "Performer";
        node.AddChild(performer);
        return node;
    }
}
