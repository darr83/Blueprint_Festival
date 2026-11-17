using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>The dev panel's tools, used while the festival is running.</summary>
[Collection(SingingCollection.Name)]
public class DevPanelTests
{
    /// <summary>A village festival with a gate, a stage, food, a bar, toilets, first aid and staff, gates open.</summary>
    private static FestivalGame Day(bool launchPad = false)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 14)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 56, f.Y + 20)).Ok);
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 60, f.Y + 34)).Ok);
        if (launchPad)
        {
            var built = false;
            for (var x = f.X + 14; x < f.Right - 16 && !built; x += 4)
            {
                for (var py = f.Bottom - 16; py > f.Y + 14 && !built; py -= 4)
                {
                    built = game.BuildLaunchPad(LaunchPadKind.Small, x, py).Ok;
                }
            }

            Assert.True(built, "Nowhere for the launch pad.");
        }

        game.StartDay();
        game.OpenGates();
        return game;
    }

    private static void RunUntil(FestivalGame game, double hour)
    {
        var when = game.Day.AddHours(hour);
        while (game.Now < when && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    private static void RunMinutes(FestivalGame game, double minutes)
    {
        var until = game.Now + TimeSpan.FromMinutes(minutes);
        while (game.Now < until && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    /// <summary>The middle of whoever is out and about in the field.</summary>
    private static (double X, double Y) Busy(FestivalGame game)
    {
        var about = game.Crowd.Parties.Where(game.Crowd.OutAndAbout).ToList();
        Assert.True(about.Count >= 2, "Nobody about.");
        return (about.Average(p => p.X), about.Average(p => p.Y));
    }

    [Fact]
    public void Money_can_be_added_and_the_bank_emptied()
    {
        var game = Day();
        var before = game.Money;
        game.DevAddMoney(10_000);
        Assert.Equal(before + 10_000, game.Money);
        game.DevAddMoney(-1_000_000);
        Assert.Equal(0, game.Money);
        game.DevAddMoney(100_000);
        game.DevEmptyBank();
        Assert.Equal(0, game.Money);
        game.DevAddMoney(int.MaxValue);
        Assert.Equal(FestivalGame.DevMostMoney, game.Money);
    }

    [Fact]
    public void Any_artist_can_be_put_on_in_the_set_playing_and_keeps_its_times()
    {
        var game = Day();
        var show = game.Shows[0];
        var first = show.Slots[0];
        RunUntil(game, first.Start.TimeOfDay.TotalHours + 0.25);
        var playing = show.Playing(game.Now)!;
        var artist = Roster.Artists.First(a => a.Name != playing.Act.Name);

        Assert.True(game.DevSwapAct(0, artist));
        var now = show.Playing(game.Now)!;
        Assert.Equal(artist.Name, now.Act.Name);
        Assert.Equal((playing.Index, playing.Start, playing.End), (now.Index, now.Start, now.End));

        // Ended early: the stage stands empty until the next set.
        Assert.True(game.DevEndSet(0));
        Assert.Null(show.Playing(game.Now));
        Assert.False(game.DevEndSet(0));

        // Between sets, the next act is the one swapped.
        var next = show.Next(game.Now)!;
        var other = Roster.Artists.First(a => a.Name != next.Act.Name && a.Name != artist.Name);
        Assert.True(game.DevSwapAct(0, other));
        Assert.Equal(other.Name, show.Next(game.Now)!.Act.Name);
        Assert.False(game.DevSwapAct(3, other));
    }

    [Fact]
    public void The_clock_can_be_hurried_on_to_a_time_of_day()
    {
        var game = Day();
        RunUntil(game, 11);
        Assert.False(game.DevJumpTo(TimeSpan.FromHours(10)));
        Assert.True(game.DevJumpTo(TimeSpan.FromHours(13)));
        var guard = 0;
        while (game.SkippingTo is not null)
        {
            game.Advance(0.016);
            Assert.True(++guard < 100_000, "The jump never got there.");
        }

        Assert.True(game.Now >= game.Day.AddHours(13) || game.CrushBuilding, $"Stopped at {game.Now:HH:mm}.");
    }

    [Fact]
    public void More_people_can_be_brought_in_and_everyone_sent_home()
    {
        var game = Day();
        RunUntil(game, 12);
        var expected = game.Crowd.Expected;
        var onSite = game.Crowd.OnSite;
        Assert.Equal(100, game.DevAddPeople(100));
        Assert.Equal(expected + 100, game.Crowd.Expected);
        RunMinutes(game, 15);
        Assert.True(game.Crowd.OnSite >= onSite + 80, $"{game.Crowd.OnSite} on site, {onSite} before.");
        Assert.Equal(game.Crowd.Parties.Count, game.Crowd.Parties.Select(p => p.Id).Distinct().Count());
        Assert.Equal(game.Crowd.Parties.Sum(p => p.Members.Count), game.Crowd.Parties.SelectMany(p => p.Members).Select(m => m.Id).Distinct().Count());

        var licence = game.Licence;
        var more = game.DevFillToLicence();
        Assert.Equal(Math.Max(0, licence - expected - 100), more);

        Assert.True(game.DevSendAllHome() > 0);
        RunMinutes(game, 60);
        Assert.Equal(0, game.Crowd.OnSite);
    }

    [Fact]
    public void Happiness_needs_and_drink_can_be_set_for_everyone()
    {
        var game = Day();
        RunUntil(game, 13);
        Assert.True(game.Crowd.SetMood(20) > 0);
        Assert.InRange(game.Crowd.Happiness, 15, 25);
        game.Crowd.SetMood(95);
        Assert.InRange(game.Crowd.Happiness, 85, 100);

        game.Crowd.MakeEveryoneNeed(Need.Drink);
        Assert.True(game.Crowd.Content(Need.Drink) < 20);
        Assert.True(game.Crowd.MakeEveryoneDrunk() > 0);
        Assert.True(game.Drinkers.Drunk > 0);
        game.Crowd.ResetNeeds();
        Assert.Equal(100, game.Crowd.Content(Need.Drink));
        Assert.Equal(0, game.Drinkers.Drunk);
    }

    [Fact]
    public void Weather_set_from_the_panel_starts_now_and_the_ground_can_be_dried()
    {
        var game = Day();
        RunUntil(game, 11.5);
        game.DevSetWeather(WeatherKind.Rain);
        Assert.Equal(WeatherKind.Rain, game.Weather.Kind);
        Assert.True(game.RainNow > 0.5);
        RunMinutes(game, 30);
        Assert.True(game.Wetness > 0);
        game.DevSetWeather(WeatherKind.Fine);
        Assert.Equal(0, game.RainNow);
        game.DevSetWetness(0);
        Assert.Equal(0, game.Wetness);
        game.DevSetWeather(WeatherKind.Showers);
        Assert.True(game.RainNow > 0);
    }

    [Fact]
    public void A_crush_can_be_made_where_the_camera_is_and_cleared_again()
    {
        var game = Day();
        RunUntil(game, 14.5);
        var (x, y) = Busy(game);
        var packed = game.DevCrushAt(x, y);
        Assert.True(packed >= 30, $"Only {packed} packed in.");
        RunMinutes(game, 3);
        Assert.True(game.CrushBuilding || game.Density.RedSquares > 0, $"Worst {game.Density.Worst:F1} a square metre.");

        Assert.True(game.DevLitterAt(x, y) > 0);
        Assert.NotEmpty(game.Litter);
        Assert.True(game.DevClearTrouble());
        Assert.False(game.CrushBuilding);
        Assert.Empty(game.Litter);
        Assert.Empty(game.Sick);
        Assert.DoesNotContain(game.Crowd.Parties, p => p.State == PartyState.Standing && p.WaitUntil > game.Now && !p.Fighting);
    }

    [Fact]
    public void Someone_can_be_hurt_or_sick_and_a_fight_started()
    {
        var game = Day();
        RunUntil(game, 15);
        var (x, y) = Busy(game);
        Assert.True(game.DevHurtAt(x, y));
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Hurt && n.What.StartsWith("Someone hurt"));
        Assert.Contains(game.Incidents, i => i.Kind == IncidentKind.Ill);
        Assert.True(game.BeSickAt(x, y));
        Assert.NotEmpty(game.Sick);
    }

    [Fact]
    public void Fireworks_can_be_let_off_now_or_held()
    {
        var none = Day();
        RunUntil(none, 12);
        Assert.False(none.DevLaunchFireworks());

        var game = Day(launchPad: true);
        RunUntil(game, 12);
        Assert.True(game.DevLaunchFireworks());
        Assert.Single(game.FireworkResults);
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Fireworks);

        var minute = game.NextShowMinute()!.Value;
        Assert.True(game.AddFireworkShow(minute) >= 0);
        Assert.Equal(TimeSpan.FromMinutes(minute), game.NextFireworksAt);
        game.FireworksHeld = true;
        RunUntil(game, (minute + 10) / 60.0);
        Assert.Single(game.FireworkResults);
    }

    [Fact]
    public void Crowd_singing_can_be_turned_off_or_left_to_the_crowd_every_chorus()
    {
        var chorus = new Chorus(40, 60);
        try
        {
            SingAlong.Setting = SingingSetting.Off;
            Assert.Equal(0, SingAlong.Share(100, 1_000_000));
            Assert.Null(SingAlong.LeftToTheCrowd(1, chorus));

            SingAlong.Setting = SingingSetting.JustTheCrowd;
            Assert.True(SingAlong.Share(10, 10) >= SingAlong.JustTheCrowdShare);
            for (var roll = 0; roll < 50; roll++)
            {
                Assert.Equal(chorus, SingAlong.LeftToTheCrowd(roll, chorus));
            }

            Assert.Equal(1, SingAlong.LeftLevel(new[] { chorus }, 50));

            SingAlong.Setting = SingingSetting.Normal;
            Assert.Equal(0, SingAlong.Share(10, 10));
            var left = Enumerable.Range(0, 400).Count(r => SingAlong.LeftToTheCrowd(r, chorus) is not null);
            Assert.InRange(left, 20, 110);
        }
        finally
        {
            SingAlong.Setting = SingingSetting.Normal;
        }
    }
}

/// <summary>Tests that read or change how the crowd sings along, which is shared, so they do not run at the same time.</summary>
[CollectionDefinition(Name)]
public class SingingCollection
{
    public const string Name = "Singing";
}
