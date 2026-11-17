using BlueprintFestival.Simulation.People;

namespace BlueprintFestival.Simulation.Show;

/// <summary>
/// How the stars dress on stage. Every act wears the look of its music: country stars in cowboy
/// hats, checked shirts and dungarees; rock stars with long hair and leather jackets; metal bands
/// with big manes, black and chains; pop stars in sparkly jackets; DJs with headphones and shades;
/// hip hop acts in backwards caps, hoodies and gold chains. Bigger names wear more shine. All the
/// acts are made up and none is dressed as any real artist.
/// </summary>
public static class StarLooks
{
    private const string Black = "#26292E";
    private const string White = "#F5F5F5";
    private const string Gold = "#F2C14E";
    private const string Silver = "#C9CFD6";
    private const string Denim = "#4A6FA5";

    private static readonly string[] CountryHats = { "#D4B77A", "#F3EBDD", "#7A5232", "#2E2E33" };
    private static readonly string[] Checks = { "#D9483B", "#3C6FB0", "#2F8F5B", "#E94F7B" };
    private static readonly string[] DenimShades = { Denim, "#3A5683", "#6E8FBF" };
    private static readonly string[] Leather = { Black, "#3B2A22", "#5A2E2E" };
    private static readonly string[] RockTees = { White, "#D9483B", "#F6B93B", "#26292E" };
    private static readonly string[] PopJackets = { "#E94F7B", "#B565D9", "#F6B93B", "#2FB7A8", "#F5F5F5" };
    private static readonly string[] PopHair = { "#F3D27A", "#E94F7B", "#B565D9", "#F5F5F5", "#4FB8D8" };
    private static readonly string[] Neon = { "#39E07A", "#FF4FA3", "#4FD8FF", "#FFE14F", "#B565D9" };
    private static readonly string[] Bright = { "#D9483B", "#3C8DAD", "#F6B93B", White, "#2FB7A8", Black };
    private static readonly string[] DarkHair = { "#1E1A17", "#2E2019", "#4A3426" };
    private static readonly string[] IndieTops = { "#2E4057", "#8C2F39", "#F3EBDD", "#3D5A40", "#D9A441" };
    private static readonly string[] Parkas = { "#5C6B3A", "#2E4057", "#7A6A55" };
    private static readonly string[] PunkHair = { "#39E07A", "#FF4FA3", "#D9483B", "#4FD8FF", "#FFE14F" };
    private const string Tartan = "#B0303A";
    private static readonly string[] Tweeds = { "#7A6A55", "#5C5446", "#8A7A5A", "#4E5A4A" };
    private static readonly string[] FolkDresses = { "#6E8F5A", "#B5542A", "#3C6FB0", "#8C2F39" };
    private static readonly string[] ReggaeColours = { "#D9483B", "#F2C14E", "#2F8F5B", White };
    private static readonly string[] Suits = { Black, "#5A1F2E", "#2E4057", "#F5F5F5", "#B5542A" };
    private static readonly string[] DiscoShine = { Gold, Silver, "#E94F7B", "#B565D9", "#2FB7A8" };
    private static readonly string[] DiscoFlares = { White, "#F2C14E", "#E94F7B", "#B565D9", Black };
    private static readonly string[] AnyHair = { "#1E1A17", "#2E2019", "#4A3426", "#6B4A2E", "#B5542A", "#C9A25A", "#E3C779" };

    /// <summary>How member number <paramref name="member"/> of an act looks on stage. The same act always looks the same.</summary>
    public static PersonLook For(Act act, int member)
    {
        ArgumentNullException.ThrowIfNull(act);
        var role = member >= 0 && member < act.Members.Count ? act.Members[member] : BandRole.Singer;
        return For(act.Genre, role, act.IsFemale(member), act.Rating, act.LookSeed + (member * 13));
    }

    /// <summary>
    /// A performer of this genre and role. Stars (four or five) always wear shades on stage, and
    /// pop and hip hop stars add gold.
    /// </summary>
    public static PersonLook For(Genre genre, BandRole role, bool female, int rating, int seed)
    {
        seed = Math.Abs(seed % 100000);
        var random = new Random(unchecked((seed * 6007) + 7919 + ((int)genre * 131) + ((int)role * 17)));
        var star = rating >= 4;
        var basic = LookGenerator.FestivalGoer(seed * 31 + 900, AgeGroup.Adult) with
        {
            Female = female,
            Height = (female ? 0.95 : 1.0) + (random.NextDouble() * 0.08),
            Build = 0.9 + (random.NextDouble() * 0.25),
            Beard = false,
            Poncho = null,
            Sunglasses = false,
            Hat = Hat.None,
            TopDetail = null,
            Legs = LegStyle.Trousers,
            Feet = Footwear.Boots,
            FeetColour = "#2E2E33",
        };

        var look = genre switch
        {
            Genre.Country => Country(basic, random, female),
            Genre.Rock => Rock(basic, random, female),
            Genre.Metal => Metal(basic, random, female),
            Genre.Pop => Pop(basic, random, female, star),
            Genre.Electronic => Electronic(basic, random, female),
            Genre.Indie => Indie(basic, random, female),
            Genre.Punk => Punk(basic, random, female),
            Genre.Folk => Folk(basic, random, female),
            Genre.Reggae => Reggae(basic, random, female),
            Genre.Soul => Soul(basic, random, female, star),
            Genre.Disco => Disco(basic, random, female, star),
            _ => HipHop(basic, random, female, star),
        };

        if (star)
        {
            look = look with { Sunglasses = true };
        }

        // A drummer in a heavy jacket, or in a hat that hides behind the cymbals, would not last a set.
        if (role == BandRole.Drummer)
        {
            look = look with { Jacket = null, Top = look.Top is TopStyle.Check or TopStyle.Dress ? TopStyle.Vest : look.Top, Legs = LegStyle.Trousers };
        }

        // DJs wear headphones whatever they play.
        if (role == BandRole.Dj && look.Headphones is null)
        {
            look = look with { Headphones = Pick(random, Neon), Hat = look.Hat is Hat.Cowboy or Hat.Beanie or Hat.Tam or Hat.FlatCap ? Hat.None : look.Hat, Hair = look.Hair == HairStyle.Mohawk ? HairStyle.Crop : look.Hair };
        }

        return look;
    }

    private static PersonLook Country(PersonLook l, Random r, bool female) => l with
    {
        Hair = female ? (r.NextDouble() < 0.6 ? HairStyle.Long : HairStyle.Ponytail) : (r.NextDouble() < 0.7 ? HairStyle.Short : HairStyle.Long),
        HairColour = Pick(r, AnyHair),
        Beard = !female && r.NextDouble() < 0.5,
        Hat = Hat.Cowboy,
        HatColour = Pick(r, CountryHats),
        Top = TopStyle.Check,
        TopColour = White,
        TopDetail = Pick(r, Checks),
        Dungarees = true,
        Legs = LegStyle.Trousers,
        LegColour = Pick(r, DenimShades),
        Feet = Footwear.Boots,
        FeetColour = r.NextDouble() < 0.6 ? "#7A5232" : "#3B2A22",
    };

    private static PersonLook Rock(PersonLook l, Random r, bool female) => l with
    {
        Hair = r.NextDouble() < 0.4 ? HairStyle.Long : HairStyle.Mane,
        HairColour = female && r.NextDouble() < 0.35 ? Pick(r, PopHair) : Pick(r, AnyHair),
        Beard = !female && r.NextDouble() < 0.3,
        Sunglasses = r.NextDouble() < 0.5,
        Top = TopStyle.TShirt,
        TopColour = Pick(r, RockTees),
        TopDetail = r.NextDouble() < 0.6 ? "#D9483B" : null,
        Jacket = Pick(r, Leather),
        Legs = LegStyle.Trousers,
        LegColour = r.NextDouble() < 0.6 ? Black : "#3A5683",
        Feet = Footwear.Boots,
        FeetColour = Black,
        Chain = r.NextDouble() < 0.4 ? Silver : null,
    };

    private static PersonLook Metal(PersonLook l, Random r, bool female) => l with
    {
        Hair = HairStyle.Mane,
        HairColour = r.NextDouble() < 0.8 ? Pick(r, DarkHair) : "#B5542A",
        Beard = !female && r.NextDouble() < 0.7,
        Sunglasses = r.NextDouble() < 0.3,
        Top = r.NextDouble() < 0.5 ? TopStyle.Vest : TopStyle.TShirt,
        TopColour = Black,
        TopDetail = r.NextDouble() < 0.5 ? "#D9483B" : White,
        Jacket = r.NextDouble() < 0.35 ? Black : null,
        Legs = LegStyle.Trousers,
        LegColour = Black,
        Feet = Footwear.Boots,
        FeetColour = Black,
        Chain = Silver,
    };

    private static PersonLook Pop(PersonLook l, Random r, bool female, bool star)
    {
        var jacket = Pick(r, PopJackets);
        var skirt = female && r.NextDouble() < 0.5;
        return l with
        {
            Hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Long : r.NextDouble() < 0.5 ? HairStyle.Bob : HairStyle.Ponytail) : HairStyle.Quiff,
            HairColour = Pick(r, PopHair),
            Top = TopStyle.TShirt,
            TopColour = jacket == White ? "#E94F7B" : White,
            Jacket = jacket,
            Sparkles = star || r.NextDouble() < 0.6 ? (jacket == "#F6B93B" ? White : Gold) : null,
            Legs = skirt ? LegStyle.Skirt : LegStyle.Trousers,
            LegColour = skirt ? jacket : (r.NextDouble() < 0.5 ? White : Black),
            Feet = skirt ? Footwear.Boots : Footwear.Trainers,
            FeetColour = skirt ? White : "#F2F2F2",
            Chain = star ? Gold : null,
        };
    }

    private static PersonLook Electronic(PersonLook l, Random r, bool female)
    {
        var neon = Pick(r, Neon);
        return l with
        {
            Hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Long : HairStyle.Ponytail) : (r.NextDouble() < 0.5 ? HairStyle.Crop : HairStyle.Quiff),
            HairColour = r.NextDouble() < 0.4 ? Pick(r, Neon) : Pick(r, AnyHair),
            Sunglasses = true,
            Top = r.NextDouble() < 0.5 ? TopStyle.Hoodie : TopStyle.Vest,
            TopColour = r.NextDouble() < 0.5 ? Black : neon,
            TopDetail = null,
            Legs = LegStyle.Trousers,
            LegColour = Black,
            Feet = Footwear.Trainers,
            FeetColour = neon,
            Headphones = neon,
        };
    }

    private static PersonLook HipHop(PersonLook l, Random r, bool female, bool star)
    {
        var colour = Pick(r, Bright);
        var hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Afro : HairStyle.Long) : (r.NextDouble() < 0.6 ? HairStyle.Crop : HairStyle.Short);
        var hat = r.NextDouble() < 0.6 ? Hat.CapBackwards : Hat.Beanie;
        return l with
        {
            Hair = hair,
            HairColour = Pick(r, DarkHair),
            Beard = !female && r.NextDouble() < 0.4,
            Hat = hair == HairStyle.Afro ? Hat.None : hat,
            HatColour = Pick(r, Bright),
            Top = r.NextDouble() < 0.6 ? TopStyle.Hoodie : TopStyle.TShirt,
            TopColour = colour,
            TopDetail = null,
            Legs = LegStyle.Trousers,
            LegColour = r.NextDouble() < 0.5 ? Black : "#7A6A55",
            Feet = Footwear.Trainers,
            FeetColour = White,
            Chain = Gold,
            Sparkles = star && r.NextDouble() < 0.4 ? Gold : null,
        };
    }

    private static PersonLook Indie(PersonLook l, Random r, bool female)
    {
        var parka = r.NextDouble() < 0.6;
        return l with
        {
            Hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Bob : HairStyle.Mop) : HairStyle.Mop,
            HairColour = Pick(r, AnyHair),
            Top = r.NextDouble() < 0.5 ? TopStyle.Stripes : TopStyle.TShirt,
            TopColour = Pick(r, IndieTops),
            TopDetail = r.NextDouble() < 0.5 ? White : null,
            Jacket = parka ? Pick(r, Parkas) : null,
            Legs = LegStyle.Trousers,
            LegColour = r.NextDouble() < 0.6 ? Black : Pick(r, DenimShades),
            Feet = Footwear.Trainers,
            FeetColour = r.NextDouble() < 0.5 ? White : Black,
        };
    }

    private static PersonLook Punk(PersonLook l, Random r, bool female) => l with
    {
        Hair = r.NextDouble() < 0.7 ? HairStyle.Mohawk : HairStyle.Crop,
        HairColour = r.NextDouble() < 0.8 ? Pick(r, PunkHair) : Pick(r, DarkHair),
        Top = r.NextDouble() < 0.5 ? TopStyle.Vest : TopStyle.TShirt,
        TopColour = r.NextDouble() < 0.6 ? White : Black,
        TopDetail = r.NextDouble() < 0.7 ? Pick(r, PunkHair) : null,
        Jacket = r.NextDouble() < 0.7 ? Black : null,
        Sparkles = r.NextDouble() < 0.5 ? Silver : null,
        Legs = female && r.NextDouble() < 0.5 ? LegStyle.Skirt : LegStyle.Trousers,
        LegColour = r.NextDouble() < 0.6 ? Tartan : Black,
        Feet = Footwear.Boots,
        FeetColour = Black,
        Chain = Silver,
    };

    private static PersonLook Folk(PersonLook l, Random r, bool female)
    {
        var tweed = Pick(r, Tweeds);
        var dress = female && r.NextDouble() < 0.4;
        return l with
        {
            Hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Long : HairStyle.Ponytail) : HairStyle.Short,
            HairColour = Pick(r, AnyHair),
            Beard = !female && r.NextDouble() < 0.6,
            Hat = !female || r.NextDouble() < 0.3 ? Hat.FlatCap : Hat.None,
            HatColour = tweed,
            Top = dress ? TopStyle.Dress : r.NextDouble() < 0.5 ? TopStyle.Check : TopStyle.LongSleeve,
            TopColour = dress ? Pick(r, FolkDresses) : "#F3EBDD",
            TopDetail = dress ? null : r.NextDouble() < 0.5 ? tweed : null,
            Jacket = dress ? null : r.NextDouble() < 0.5 ? tweed : null,
            Legs = dress ? LegStyle.Skirt : LegStyle.Trousers,
            LegColour = dress ? Pick(r, FolkDresses) : "#6B5A45",
            Feet = Footwear.Boots,
            FeetColour = "#5A3A22",
        };
    }

    private static PersonLook Reggae(PersonLook l, Random r, bool female) => l with
    {
        Hair = r.NextDouble() < 0.5 ? HairStyle.Long : female ? HairStyle.Afro : HairStyle.Short,
        HairColour = Pick(r, DarkHair),
        Beard = !female && r.NextDouble() < 0.5,
        Hat = r.NextDouble() < 0.7 ? Hat.Tam : Hat.None,
        HatColour = "#2F8F5B",
        Top = r.NextDouble() < 0.5 ? TopStyle.Stripes : TopStyle.TShirt,
        TopColour = Pick(r, ReggaeColours),
        TopDetail = Pick(r, ReggaeColours),
        Legs = LegStyle.Trousers,
        LegColour = r.NextDouble() < 0.5 ? "#D8CBB0" : Pick(r, DenimShades),
        Feet = r.NextDouble() < 0.4 ? Footwear.Sandals : Footwear.Trainers,
        FeetColour = "#7A5232",
    };

    private static PersonLook Soul(PersonLook l, Random r, bool female, bool star)
    {
        var suit = Pick(r, Suits);
        var dress = female && r.NextDouble() < 0.6;
        return l with
        {
            Hair = female ? (r.NextDouble() < 0.5 ? HairStyle.Afro : HairStyle.Bun) : (r.NextDouble() < 0.5 ? HairStyle.Short : HairStyle.Crop),
            HairColour = Pick(r, DarkHair),
            Top = dress ? TopStyle.Dress : TopStyle.LongSleeve,
            TopColour = dress ? suit : White,
            TopDetail = null,
            Jacket = dress ? null : suit,
            Sparkles = dress && (star || r.NextDouble() < 0.5) ? Gold : null,
            Legs = dress ? LegStyle.Skirt : LegStyle.Trousers,
            LegColour = suit,
            Feet = Footwear.Boots,
            FeetColour = Black,
            Chain = star || r.NextDouble() < 0.4 ? Gold : null,
        };
    }

    private static PersonLook Disco(PersonLook l, Random r, bool female, bool star)
    {
        var shine = Pick(r, DiscoShine);
        var flares = Pick(r, DiscoFlares);
        return l with
        {
            Hair = r.NextDouble() < 0.6 ? HairStyle.Afro : female ? HairStyle.Long : HairStyle.Curly,
            HairColour = r.NextDouble() < 0.7 ? Pick(r, AnyHair) : "#F3D27A",
            Top = female && r.NextDouble() < 0.5 ? TopStyle.Dress : TopStyle.LongSleeve,
            TopColour = shine,
            Sparkles = star || r.NextDouble() < 0.8 ? (shine == Silver ? White : Silver) : null,
            Legs = LegStyle.Trousers,
            LegColour = flares,
            Feet = Footwear.Boots,
            FeetColour = r.NextDouble() < 0.5 ? White : Gold,
            Chain = star ? Gold : null,
        };
    }

    private static string Pick(Random random, string[] from) => from[random.Next(from.Length)];
}
