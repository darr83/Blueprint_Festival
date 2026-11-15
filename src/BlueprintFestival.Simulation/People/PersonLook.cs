namespace BlueprintFestival.Simulation.People;

public enum AgeGroup
{
    Child,
    Teenager,
    Adult,
    Older,
}

public enum HairStyle
{
    Bald,
    Short,
    Crop,
    Bob,
    Long,
    Ponytail,
    Bun,
    Afro,
    Curly,

    /// <summary>A big mane past the shoulders, for metal and rock stars.</summary>
    Mane,

    /// <summary>Short at the sides with a swept-up front.</summary>
    Quiff,
}

public enum TopStyle
{
    TShirt,
    Vest,
    LongSleeve,
    Hoodie,
    Stripes,
    Dress,

    /// <summary>A long-sleeved checked shirt; the check is <see cref="PersonLook.TopDetail"/>.</summary>
    Check,
}

public enum LegStyle
{
    Trousers,
    Shorts,
    Skirt,
}

public enum Footwear
{
    Trainers,
    Boots,
    Sandals,
    Wellies,
}

public enum Hat
{
    None,
    Bucket,
    Cowboy,
    Cap,
    Sunhat,

    /// <summary>A woolly hat pulled down.</summary>
    Beanie,

    /// <summary>A cap worn with the peak at the back.</summary>
    CapBackwards,
}

/// <summary>
/// Everything about how one person looks, worked out once from a seed. Colours are web colours
/// such as "#E94F7B". The game draws this as a "style A" figure: big round head, dot eyes, a smile
/// and a rounded body.
/// </summary>
public sealed record PersonLook
{
    public int Seed { get; init; }

    public AgeGroup Age { get; init; } = AgeGroup.Adult;

    public bool Female { get; init; }

    /// <summary>Size against a typical adult: about 0.6 for a small child, up to 1.08 for a tall adult.</summary>
    public double Height { get; init; } = 1;

    /// <summary>How broad the body is: 0.85 slight, 1 typical, 1.25 broad.</summary>
    public double Build { get; init; } = 1;

    public string Skin { get; init; } = "#E8B894";

    public HairStyle Hair { get; init; } = HairStyle.Short;

    public string HairColour { get; init; } = "#4A3426";

    public bool Beard { get; init; }

    public bool Sunglasses { get; init; }

    public TopStyle Top { get; init; } = TopStyle.TShirt;

    public string TopColour { get; init; } = "#4FA8D8";

    /// <summary>The second colour of a striped top, or the print on a T-shirt. Null for a plain top.</summary>
    public string? TopDetail { get; init; }

    public LegStyle Legs { get; init; } = LegStyle.Trousers;

    public string LegColour { get; init; } = "#3F5B8C";

    public Footwear Feet { get; init; } = Footwear.Trainers;

    public string FeetColour { get; init; } = "#F2F2F2";

    public Hat Hat { get; init; } = Hat.None;

    public string HatColour { get; init; } = "#F6B93B";

    /// <summary>The colour of an open jacket worn over the top, or null for none.</summary>
    public string? Jacket { get; init; }

    /// <summary>Little shining dots over the top or jacket (a stage outfit), or null for none.</summary>
    public string? Sparkles { get; init; }

    /// <summary>Dungarees: a bib and straps over the top, the colour of the trousers.</summary>
    public bool Dungarees { get; init; }

    /// <summary>The colour of a chain worn round the neck, or null for none.</summary>
    public string? Chain { get; init; }

    /// <summary>The colour of headphones worn over the head (a DJ's), or null for none.</summary>
    public string? Headphones { get; init; }

    /// <summary>The colour of a rain poncho worn over everything, or null when not wearing one.</summary>
    public string? Poncho { get; init; }

    /// <summary>The colour of the VIP lanyard round the neck, with a pass hanging on the chest, or null for an ordinary ticket.</summary>
    public string? Lanyard { get; init; }

    /// <summary>Everyone who has come through the gate wears a festival wristband.</summary>
    public string Wristband { get; init; } = "#E94F7B";
}
