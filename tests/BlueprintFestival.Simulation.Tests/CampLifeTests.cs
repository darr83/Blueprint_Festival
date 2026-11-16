using System.Text.Json;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class CampLifeTests
{
    /// <summary>The estate festival with camping, a way in and fire lanes, ready to open.</summary>
    private static FestivalGame ReadyToOpen()
    {
        var game = CampLayoutTests.BuildDay();
        CampLayoutTests.OpenAWayIn(game);
        CampLayoutTests.LayFireLanes(game);
        return game;
    }

    /// <summary>Runs the day until the music is over and the camp has settled for the night.</summary>
    private static void RunToNight(FestivalGame game)
    {
        game.OpenGates();
        while (game.Phase == Phase.Day && game.CampTime == CampTime.None)
        {
            game.Clock.Step();
        }

        Assert.Equal(CampTime.Night, game.CampTime);
    }

    /// <summary>Runs on, a tick at a time, until the tidy-up starts.</summary>
    private static void RunToTidy(FestivalGame game)
    {
        for (var i = 0; i < 2000 && game.Phase == Phase.Day; i++)
        {
            game.Clock.Step();
        }

        Assert.Equal(Phase.Tidy, game.Phase);
    }

    private static SaveData Reload(FestivalGame game) => JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(game.Save()))!;

    [Fact]
    public void Camp_stewards_only_work_in_a_camping_field()
    {
        var game = CampLayoutTests.BuildDay();
        var camp = CampLayoutTests.CampOf(game);
        var music = game.Field.Fields.First(f => f.Use == FieldUse.Music).Grass;
        Assert.False(game.HireStaff(StaffKind.CampSteward, new TilePoint(music.X + 20, music.Y + 20)).Ok);
        Assert.True(game.HireStaff(StaffKind.CampSteward, new TilePoint(camp.X + 30, camp.Y + 30)).Ok);
        var pit = CampLayoutTests.Free(game, StallKind.FirePit, camp.X + 40, camp.Y + 40);
        Assert.True(game.BuildStall(StallKind.FirePit, pit.X, pit.Y, Facing.South).Ok);
        Assert.False(game.Site.CheckStall(StallKind.FirePit, music.X + 30, music.Y + 30, Facing.South).Ok);
    }

    [Fact]
    public void A_camp_nobody_watches_has_a_bad_night_then_packs_up_in_the_morning()
    {
        var game = ReadyToOpen();
        RunToNight(game);
        Assert.True(game.NightCampers > 0);
        Assert.True(game.CampersUp > 0, "Some campers should sit up round fires as the night begins.");
        Assert.NotEmpty(game.CampFires);
        Assert.All(game.CampFires, f => Assert.False(f.InPit));

        // Half way through the night it saves and loads as it was.
        var start = game.Clock.TickCount;
        while (game.Now.TimeOfDay is { TotalHours: > 4 } || game.Now.TimeOfDay.TotalHours < 2)
        {
            game.Clock.Step();
        }

        Assert.True(game.Clock.TickCount - start < 300, "The night should run a minute a tick.");
        var loaded = FestivalGame.Load(Reload(game));
        Assert.Equal(CampTime.Night, loaded.CampTime);
        Assert.Equal(game.CampLog.Count, loaded.CampLog.Count);
        Assert.Equal(game.CampSafe, loaded.CampSafe, 6);

        game.SkipCampNight();
        Assert.Equal(CampTime.Morning, game.CampTime);
        Assert.Contains(game.CampLog, l => l.Tone == DayTone.Bad);
        Assert.DoesNotContain(game.CampLog, l => l.Tone == DayTone.Good && l.Outcome.Contains("steward"));
        Assert.Contains(game.CampLog, l => l.Text == "No showers in the camp");
        Assert.Contains(game.CampLog, l => l.Text == "No breakfast in the camp");

        var tents = game.Crowd.Tents.Count();
        for (var i = 0; i < 120; i++)
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.Tents.Count() < tents, "Tents should come down as campers pack up.");
        RunToTidy(game);
        Assert.Equal(CampTime.None, game.CampTime);
        Assert.True(game.Now >= game.Day + FestivalGame.MorningStarts);
        Assert.Equal(game.TentsLeft, game.Crowd.Tents.Count());
        Assert.True(game.TentsLeft > 0, "With nothing in the camp, a few should leave their tent.");
        Assert.True(game.ScoutsFee >= game.TentsLeft * FestivalGame.TentLeftCost);
        Assert.True(game.PayScouts());
        Assert.Equal(0, game.TentsLeft);
        Assert.Empty(game.Crowd.Tents);
    }

    [Fact]
    public void Stewards_security_fire_pits_and_a_shop_make_for_a_good_night_and_morning()
    {
        var game = ReadyToOpen();
        var camp = CampLayoutTests.CampOf(game);
        // Stewards and security on a grid across the whole camp, with fire pits among them.
        for (var x = camp.X + 30; x < camp.Right - 10; x += 60)
        {
            for (var y = camp.Y + 30; y < camp.Bottom - 10; y += 60)
            {
                var steward = CampLayoutTests.Free(game, StallKind.Bin, x, y);
                Assert.True(game.HireStaff(StaffKind.CampSteward, steward).Ok);
                Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(steward.X + 4, steward.Y)).Ok);
                var pit = CampLayoutTests.Free(game, StallKind.FirePit, x + 8, y + 8);
                Assert.True(game.BuildStall(StallKind.FirePit, pit.X, pit.Y, Facing.South).Ok);
            }
        }

        foreach (var (kind, x, y) in new[] { (StallKind.ShowerBlock, 20, 20), (StallKind.ShowerBlock, camp.Width - 60, camp.Height - 40), (StallKind.ToiletBlock, 60, 50), (StallKind.ToiletBlock, camp.Width - 80, 60), (StallKind.CampShop, camp.Width / 2, camp.Height - 40) })
        {
            var at = CampLayoutTests.Free(game, kind, camp.X + x, camp.Y + y);
            Assert.True(game.BuildStall(kind, at.X, at.Y, Facing.South).Ok);
        }

        RunToNight(game);
        Assert.Contains(game.CampFires, f => f.InPit);
        game.AskForQuiet();
        Assert.NotNull(game.QuietUntil);
        game.SkipCampNight();
        var bad = game.CampLog.Count(l => l.Tone == DayTone.Bad);
        Assert.True(bad <= 1, $"With the camp watched, little should go wrong: {string.Join("; ", game.CampLog.Select(l => $"{l.Text} ({l.Outcome})"))}");
        Assert.True(game.CampSafe > 0.8 && game.CampQuiet > 0.8);

        game.SkipCampMorning();
        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.True(game.BreakfastsSold > 0);
        Assert.Contains(game.Income, l => l.What == "Breakfast for campers" && l.Amount == game.BreakfastTakings);
        Assert.Contains(game.DayLog, n => n.Kind == DayNoteKind.Camp && n.What.Contains("good night"));
    }

    [Fact]
    public void Security_sent_to_the_camp_walks_the_tents()
    {
        var game = ReadyToOpen();
        var music = game.Field.Fields.First(f => f.Use == FieldUse.Music).Grass;
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(music.X + 20, music.Y + 20)).Ok);
        RunToNight(game);
        var guard = game.Staff.First(s => s.Kind == StaffKind.Security);
        Assert.False(game.InCamp(FieldMap.TileAt(guard.X, guard.Y)));
        game.SendSecurityToCamp();
        game.Clock.Step();
        Assert.Contains(game.Crowd.Pitched, t => Math.Abs(FieldMap.CentreOf(t.Pitch).X - guard.X) < 12 && Math.Abs(FieldMap.CentreOf(t.Pitch).Y - guard.Y) < 12);
        Assert.True(game.SecuritySentToCamp);
    }
}
