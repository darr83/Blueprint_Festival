namespace BlueprintFestival.Simulation.People;

/// <summary>
/// Makes up festival-goers. The same seed always gives the same person, so a saved game only has
/// to remember the seed.
/// </summary>
public static class LookGenerator
{
    public static readonly string[] SkinTones =
    {
        "#F6D3BD", "#EFC3A4", "#E0A983", "#C68A5E", "#A0694A", "#74482F", "#55341F",
    };

    private static readonly string[] NaturalHair =
    {
        "#1E1A17", "#2E2019", "#4A3426", "#6B4A2E", "#8E6A3E", "#C9A25A", "#E3C886", "#B5542A",
    };

    private static readonly string[] GreyHair = { "#9A9A98", "#BDBDBA", "#E4E2DC", "#6F6F6D" };

    private static readonly string[] DyedHair = { "#E94F7B", "#4FB8D8", "#8E6BD1", "#39B38A" };

    /// <summary>
    /// Bright festival colours. None of them is a staff uniform colour (steward orange, first aid
    /// green, welfare purple, hi-vis yellow), so the public never looks like staff.
    /// </summary>
    public static readonly string[] BrightTops =
    {
        "#E94F7B", "#F6B93B", "#4FA8D8", "#36B7B0", "#F2836B", "#C59BE0", "#F7F3EA", "#2F4B7C",
        "#F2A7C3", "#8FD3C8", "#D9483B", "#FFE08A", "#7FB7E6", "#B8336A", "#F5F5F5", "#3C8DAD",
    };

    private static readonly string[] Bottoms =
    {
        "#3F5B8C", "#5B7DB1", "#2C3E57", "#8FA9C9", "#3A3A3F", "#C9B38A", "#7A6A55", "#E8E0D0", "#556B4E",
    };

    private static readonly string[] SkirtColours = { "#E94F7B", "#F6B93B", "#2C3E57", "#F7F3EA", "#36B7B0", "#3F5B8C" };

    private static readonly string[] ShoeColours = { "#F2F2F2", "#2E2E33", "#D9483B", "#3F5B8C", "#F6B93B", "#8A6A45" };

    private static readonly string[] WellyColours = { "#2F6B4A", "#2C3E57", "#D9483B", "#F6B93B", "#E94F7B", "#2E2E33", "#4FA8D8" };

    private static readonly string[] HatColours = { "#F6B93B", "#E94F7B", "#F7F3EA", "#4FA8D8", "#36B7B0", "#C59BE0", "#2C3E57" };

    private static readonly string[] StrawColours = { "#E2C992", "#D4B77A", "#C9A66B" };

    public static readonly string[] PonchoColours = { "#F6D84A", "#6FC3E8", "#F29BBB", "#7FD6B8", "#F5F5F5", "#C59BE0" };

    /// <summary>
    /// A member of the public. On a wet day most people put on wellies and many wear a rain poncho;
    /// on a dry one there are more shorts, sandals and sun hats.
    /// </summary>
    public static PersonLook FestivalGoer(int seed, bool raining = false)
    {
        var random = new Random(unchecked((seed * 7919) + 104729));
        var roll = random.NextDouble();
        var age = roll < 0.10 ? AgeGroup.Child : roll < 0.22 ? AgeGroup.Teenager : roll < 0.86 ? AgeGroup.Adult : AgeGroup.Older;
        return FestivalGoer(seed, age, raining, random);
    }

    /// <summary>A member of the public of a chosen age, for families and for line-ups.</summary>
    public static PersonLook FestivalGoer(int seed, AgeGroup age, bool raining = false) =>
        FestivalGoer(seed, age, raining, new Random(unchecked((seed * 7919) + 104729)));

    private static PersonLook FestivalGoer(int seed, AgeGroup age, bool raining, Random random)
    {
        var female = random.NextDouble() < 0.5;
        var height = age switch
        {
            AgeGroup.Child => 0.56 + (random.NextDouble() * 0.16),
            AgeGroup.Teenager => 0.82 + (random.NextDouble() * 0.12),
            _ => (female ? 0.92 : 0.97) + (random.NextDouble() * 0.11),
        };
        var build = age == AgeGroup.Child ? 0.9 + (random.NextDouble() * 0.15) : 0.85 + (random.NextDouble() * 0.4);

        // ---- hair
        var hairRoll = random.NextDouble();
        HairStyle hair;
        if (female)
        {
            hair = hairRoll < 0.26 ? HairStyle.Long : hairRoll < 0.46 ? HairStyle.Bob : hairRoll < 0.62 ? HairStyle.Ponytail
                : hairRoll < 0.74 ? HairStyle.Bun : hairRoll < 0.84 ? HairStyle.Curly : hairRoll < 0.92 ? HairStyle.Afro : HairStyle.Crop;
        }
        else
        {
            hair = hairRoll < 0.40 ? HairStyle.Short : hairRoll < 0.62 ? HairStyle.Crop : hairRoll < 0.74 ? HairStyle.Curly
                : hairRoll < 0.82 ? HairStyle.Afro : hairRoll < 0.88 ? HairStyle.Long : hairRoll < 0.93 ? HairStyle.Bun : HairStyle.Bald;
            if (hair == HairStyle.Bald && age is AgeGroup.Child or AgeGroup.Teenager)
            {
                hair = HairStyle.Short;
            }
        }

        var hairColour = Pick(random, NaturalHair);
        if (age == AgeGroup.Older && random.NextDouble() < 0.8)
        {
            hairColour = Pick(random, GreyHair);
        }
        else if (age is AgeGroup.Teenager or AgeGroup.Adult && random.NextDouble() < 0.07)
        {
            hairColour = Pick(random, DyedHair);
        }

        var beard = !female && age is AgeGroup.Adult or AgeGroup.Older && random.NextDouble() < 0.3;
        beard &= !female && age is AgeGroup.Adult or AgeGroup.Older;

        // ---- clothes
        var topRoll = random.NextDouble();
        var top = topRoll < 0.42 ? TopStyle.TShirt : topRoll < 0.56 ? TopStyle.Stripes : topRoll < 0.68 ? TopStyle.Vest
            : topRoll < 0.80 ? TopStyle.LongSleeve : topRoll < 0.90 ? TopStyle.Hoodie : TopStyle.Dress;
        if (top == TopStyle.Dress && !female)
        {
            top = TopStyle.TShirt;
        }

        if (raining && top == TopStyle.Vest)
        {
            top = TopStyle.Hoodie;
        }

        var topColour = Pick(random, BrightTops);
        string? detail = null;
        if (top == TopStyle.Stripes)
        {
            detail = PickOther(random, BrightTops, topColour);
        }
        else if (top == TopStyle.TShirt && random.NextDouble() < 0.35)
        {
            detail = PickOther(random, BrightTops, topColour);
        }

        var legRoll = random.NextDouble();
        var shortsChance = raining ? 0.25 : 0.55;
        var legs = top == TopStyle.Dress ? LegStyle.Skirt
            : female && legRoll < 0.14 ? LegStyle.Skirt
            : legRoll < 0.14 + shortsChance ? LegStyle.Shorts
            : LegStyle.Trousers;
        var legColour = legs == LegStyle.Skirt ? (top == TopStyle.Dress ? topColour : Pick(random, SkirtColours)) : Pick(random, Bottoms);

        var feetRoll = random.NextDouble();
        var feet = raining
            ? (feetRoll < 0.7 ? Footwear.Wellies : feetRoll < 0.88 ? Footwear.Boots : Footwear.Trainers)
            : (feetRoll < 0.5 ? Footwear.Trainers : feetRoll < 0.68 ? Footwear.Sandals : feetRoll < 0.85 ? Footwear.Boots : Footwear.Wellies);
        var feetColour = feet == Footwear.Wellies ? Pick(random, WellyColours)
            : feet == Footwear.Boots ? (random.NextDouble() < 0.5 ? "#5A4636" : "#2E2E33")
            : feet == Footwear.Sandals ? "#8A6A45"
            : Pick(random, ShoeColours);

        var hatRoll = random.NextDouble();
        var hat = hatRoll < 0.13 ? Hat.Bucket : hatRoll < 0.20 ? Hat.Cowboy : hatRoll < 0.30 ? Hat.Cap : hatRoll < 0.36 ? Hat.Sunhat : Hat.None;
        if (hair == HairStyle.Afro || hair == HairStyle.Bun)
        {
            hat = Hat.None;
        }

        var hatColour = hat is Hat.Cowboy or Hat.Sunhat ? Pick(random, StrawColours) : Pick(random, HatColours);

        var sunglasses = !raining && age != AgeGroup.Child && random.NextDouble() < 0.16;
        var poncho = raining && random.NextDouble() < 0.55 ? Pick(random, PonchoColours) : null;

        return new PersonLook
        {
            Seed = seed,
            Age = age,
            Female = female,
            Height = height,
            Build = build,
            Skin = Pick(random, SkinTones),
            Hair = hair,
            HairColour = hairColour,
            Beard = beard,
            Sunglasses = sunglasses,
            Top = top,
            TopColour = topColour,
            TopDetail = detail,
            Legs = legs,
            LegColour = legColour,
            Feet = feet,
            FeetColour = feetColour,
            Hat = poncho is null ? hat : Hat.None,
            HatColour = hatColour,
            Poncho = poncho,
        };
    }

    private static string Pick(Random random, string[] from) => from[random.Next(from.Length)];

    private static string PickOther(Random random, string[] from, string not)
    {
        var picked = Pick(random, from);
        return picked == not ? from[(Array.IndexOf(from, picked) + 1) % from.Length] : picked;
    }
}
