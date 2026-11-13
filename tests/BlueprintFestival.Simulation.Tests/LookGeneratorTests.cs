using System.Text.RegularExpressions;
using BlueprintFestival.Simulation.People;

namespace BlueprintFestival.Simulation.Tests;

public class LookGeneratorTests
{
    private static readonly PersonLook[] Crowd = Enumerable.Range(1, 2000).Select(i => LookGenerator.FestivalGoer(i)).ToArray();
    private static readonly PersonLook[] WetCrowd = Enumerable.Range(1, 2000).Select(i => LookGenerator.FestivalGoer(i, raining: true)).ToArray();

    [Fact]
    public void The_same_seed_always_gives_the_same_person()
    {
        Assert.Equal(LookGenerator.FestivalGoer(42), LookGenerator.FestivalGoer(42));
        Assert.Equal(LookGenerator.FestivalGoer(42, raining: true), LookGenerator.FestivalGoer(42, raining: true));
        Assert.NotEqual(LookGenerator.FestivalGoer(42), LookGenerator.FestivalGoer(43));
    }

    [Fact]
    public void A_crowd_is_a_mix_of_ages_with_adults_the_most_common()
    {
        var ages = Crowd.GroupBy(p => p.Age).ToDictionary(g => g.Key, g => g.Count() / (double)Crowd.Length);
        Assert.InRange(ages[AgeGroup.Child], 0.05, 0.16);
        Assert.InRange(ages[AgeGroup.Teenager], 0.07, 0.18);
        Assert.InRange(ages[AgeGroup.Adult], 0.55, 0.72);
        Assert.InRange(ages[AgeGroup.Older], 0.08, 0.20);
        Assert.InRange(Crowd.Count(p => p.Female) / (double)Crowd.Length, 0.44, 0.56);
    }

    [Fact]
    public void Children_are_small_and_adults_are_adult_sized()
    {
        Assert.All(Crowd.Where(p => p.Age == AgeGroup.Child), p => Assert.InRange(p.Height, 0.55, 0.73));
        Assert.All(Crowd.Where(p => p.Age == AgeGroup.Teenager), p => Assert.InRange(p.Height, 0.8, 0.95));
        Assert.All(Crowd.Where(p => p.Age is AgeGroup.Adult or AgeGroup.Older), p => Assert.InRange(p.Height, 0.9, 1.09));
        Assert.All(Crowd, p => Assert.InRange(p.Build, 0.84, 1.26));
    }

    [Fact]
    public void A_crowd_looks_varied()
    {
        Assert.True(Crowd.Select(p => p.Skin).Distinct().Count() >= 6);
        Assert.True(Crowd.Select(p => p.TopColour).Distinct().Count() >= 14);
        Assert.Equal(Enum.GetValues<HairStyle>().Length, Crowd.Select(p => p.Hair).Distinct().Count());
        Assert.Equal(Enum.GetValues<TopStyle>().Length, Crowd.Select(p => p.Top).Distinct().Count());
        Assert.Equal(Enum.GetValues<Hat>().Length, Crowd.Select(p => p.Hat).Distinct().Count());
        Assert.Equal(Enum.GetValues<Footwear>().Length, Crowd.Select(p => p.Feet).Distinct().Count());

        // In any twenty people standing together, no two should be dressed exactly alike.
        var twenty = Crowd.Take(20).Select(p => (p.Top, p.TopColour, p.Legs, p.LegColour, p.Hat, p.Hair)).Distinct().Count();
        Assert.Equal(20, twenty);
    }

    [Fact]
    public void Every_colour_is_a_proper_web_colour()
    {
        var hex = new Regex("^#[0-9A-F]{6}$");
        foreach (var p in Crowd.Concat(WetCrowd))
        {
            foreach (var colour in new[] { p.Skin, p.HairColour, p.TopColour, p.LegColour, p.FeetColour, p.HatColour, p.Wristband, p.TopDetail, p.Poncho })
            {
                if (colour is not null)
                {
                    Assert.Matches(hex, colour);
                }
            }
        }
    }

    [Fact]
    public void Nobody_in_the_public_wears_a_staff_uniform_colour()
    {
        // Steward orange, first aid green, welfare purple and hi-vis yellow belong to staff.
        var staff = new[] { "#F37021", "#1E9E52", "#7B3FA0", "#D7E91C" };
        Assert.All(Crowd, p => Assert.DoesNotContain(p.TopColour, staff));
        Assert.All(LookGenerator.BrightTops, c => Assert.DoesNotContain(c, staff));
    }

    [Fact]
    public void Sensible_combinations_only()
    {
        foreach (var p in Crowd.Concat(WetCrowd))
        {
            if (p.Top == TopStyle.Dress)
            {
                Assert.True(p.Female);
                Assert.Equal(LegStyle.Skirt, p.Legs);
                Assert.Equal(p.TopColour, p.LegColour);
            }

            if (p.Beard)
            {
                Assert.False(p.Female);
                Assert.True(p.Age is AgeGroup.Adult or AgeGroup.Older);
            }

            if (p.Age == AgeGroup.Child)
            {
                Assert.False(p.Sunglasses);
                Assert.NotEqual(HairStyle.Bald, p.Hair);
            }

            if (p.Top == TopStyle.Stripes)
            {
                Assert.NotNull(p.TopDetail);
                Assert.NotEqual(p.TopColour, p.TopDetail);
            }

            if (p.Poncho is not null)
            {
                Assert.Equal(Hat.None, p.Hat);
            }
        }
    }

    [Fact]
    public void Rain_brings_out_wellies_and_ponchos()
    {
        double Share(PersonLook[] crowd, Func<PersonLook, bool> test) => crowd.Count(test) / (double)crowd.Length;

        Assert.True(Share(WetCrowd, p => p.Feet == Footwear.Wellies) > 0.6);
        Assert.True(Share(Crowd, p => p.Feet == Footwear.Wellies) < 0.25);
        Assert.InRange(Share(WetCrowd, p => p.Poncho is not null), 0.45, 0.65);
        Assert.Equal(0, Share(Crowd, p => p.Poncho is not null));
        Assert.Equal(0, Share(WetCrowd, p => p.Sunglasses));
        Assert.Equal(0, Share(WetCrowd, p => p.Top == TopStyle.Vest));
        Assert.True(Share(WetCrowd, p => p.Legs == LegStyle.Shorts) < Share(Crowd, p => p.Legs == LegStyle.Shorts));
    }

    [Fact]
    public void A_chosen_age_can_be_asked_for()
    {
        Assert.All(Enumerable.Range(1, 50), i => Assert.Equal(AgeGroup.Child, LookGenerator.FestivalGoer(i, AgeGroup.Child).Age));
    }
}
