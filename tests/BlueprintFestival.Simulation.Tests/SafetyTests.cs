using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class SafetyTests
{
    /// <summary>
    /// A planned festival on its build day with a gate with ticket staff, food, drink and toilets,
    /// a first aid tent and two first-aiders. The headliner is the biggest name on offer, playing
    /// on the farm trailer that is all there is money left for, or the smallest, on a bandstand.
    /// </summary>
    private static FestivalGame BuildDay(bool bigName)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        var stars = bigName ? game.Headliners.Max(h => h.Stars) : game.Headliners.Min(h => h.Stars);
        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == stars));
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(bigName ? StageKind.FarmTrailer : StageKind.Bandstand, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 20, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 30, f.Y + 30)).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 30)).Ok);
        return game;
    }

    /// <summary>Puts a front barrier across the stage with one member of security in the strip behind it.</summary>
    private static void BarrierAndPitCrew(FestivalGame game)
    {
        var rect = game.Site.Stages[0].Rect;
        var row = rect.Bottom + 4;
        Assert.True(game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(rect.X - 2, row), new TilePoint(rect.Right + 1, row)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(rect.X + (rect.Width / 2), rect.Bottom + 2)).Ok);
    }

    private static void RunTheDay(FestivalGame game, Action? everyTick = null)
    {
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            everyTick?.Invoke();
            Assert.True(++guard < 70000, "The day never ended.");
        }
    }

    [Fact]
    public void The_map_is_measured_in_two_metre_squares_and_coloured_green_amber_or_red()
    {
        var density = new DensityMap(FieldMap.Village());
        var people = Enumerable.Range(0, 17).Select(_ => (21.0, 31.0)).Concat(Enumerable.Range(0, 8).Select(_ => (40.5, 10.5))).Append((3.0, 3.0));
        density.Measure(people);

        Assert.Equal(17 / 4.0, density.At(20.1, 30.1));
        Assert.Equal(Crowding.CrushRisk, density.StatusAt(21.9, 31.9));
        Assert.Equal(Crowding.Busy, density.StatusAt(40, 10));
        Assert.Equal(Crowding.Comfortable, density.StatusAt(3, 3));
        Assert.Equal(Crowding.Comfortable, density.StatusAt(60, 20));
        Assert.Equal(1, density.RedSquares);
        Assert.Equal(1, density.AmberSquares);
        Assert.Equal(17 / 4.0, density.Worst);
        Assert.Equal((21.0, 31.0), density.WorstAt);
        Assert.Equal(Crowding.Busy, DensityMap.StatusOf(2));
        Assert.Equal(Crowding.CrushRisk, DensityMap.StatusOf(4));
    }

    [Fact]
    public void A_bigger_name_pulls_its_audience_in_closer_and_tighter()
    {
        var game = BuildDay(bigName: false);
        Assert.True(game.Crowd.AudienceReach(0.95) < game.Crowd.AudienceReach(0.7));
        Assert.True(game.Crowd.AudienceReach(0.7) < game.Crowd.AudienceReach(0.3));
        Assert.True(Crowd.Squeeze(0.3) <= DensityMap.Busy);
        Assert.True(Crowd.Squeeze(0.71) < DensityMap.Crush);
        Assert.True(Crowd.Squeeze(0.87) > DensityMap.Crush);
        Assert.True(Crowd.Squeeze(0.95) > Crowd.Squeeze(0.87));
        Assert.True(Crowd.StewardLimit < DensityMap.Crush);
    }

    [Fact]
    public void A_small_headliner_on_the_bandstand_hurts_nobody()
    {
        var game = BuildDay(bigName: false);
        var warnings = 0;
        game.CrushWarning += (_, _) => warnings++;
        RunTheDay(game);

        Assert.Equal(0, warnings);
        Assert.Equal(0, game.CrushInjuries);
        Assert.False(game.SeriousIncident);
    }

    [Fact]
    public void A_big_name_with_nobody_minding_the_front_ends_in_a_crush_and_the_day_is_called_off()
    {
        var game = BuildDay(bigName: true);
        var warnings = 0;
        var hurtAt = new List<(double X, double Y)>();
        game.CrushWarning += (_, _) => warnings++;
        game.CrushInjury += (x, y) => hurtAt.Add((x, y));
        var redAtTheHeadliner = false;
        RunTheDay(game, () => redAtTheHeadliner |= game.Show.Playing(game.Now) is { Headliner: true } && game.CrowdStatus == Crowding.CrushRisk);

        Assert.True(redAtTheHeadliner);
        Assert.True(warnings >= 1);
        Assert.Equal(FestivalGame.SeriousAt, game.CrushInjuries);
        Assert.Equal(game.CrushInjuries, hurtAt.Count);
        Assert.True(game.SeriousIncident);
        Assert.NotNull(game.Show.CalledOff);
        Assert.Null(game.Show.Playing(game.Show.CalledOff!.Value));

        game.PayScouts();
        var result = game.HandBack();
        Assert.True(result.SeriousIncident);
        Assert.Equal(FestivalGame.SeriousAt, result.CrushInjuries);
        Assert.Equal(result.LicenceNow, result.LicenceNext);
        Assert.Contains("crush", result.Verdict, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_front_barrier_is_found_and_security_behind_it_keep_a_big_crowd_safe()
    {
        var game = BuildDay(bigName: true);
        var stage = game.Site.Stages[0];
        Assert.Equal(0, game.BarrierDepth(stage));
        Assert.False(game.HasPitCrew(stage));

        BarrierAndPitCrew(game);
        Assert.Equal(2.5, game.BarrierDepth(stage));
        Assert.True(game.HasPitCrew(stage));
        var front = Show.RunningOrder.Front(stage);
        Assert.True(game.Stewarded(front.X, front.Y + 5));
        Assert.False(game.Stewarded(front.X + 30, front.Y + 5));

        RunTheDay(game);
        Assert.Equal(0, game.CrushInjuries);
        Assert.False(game.SeriousIncident);
    }

    [Fact]
    public void Pausing_the_act_eases_the_crowd_before_anyone_is_hurt()
    {
        var game = BuildDay(bigName: true);
        var warned = false;
        game.CrushWarning += (_, _) => warned = true;
        Assert.False(game.PauseAct());
        var pauses = 0;
        var twice = true;
        RunTheDay(game, () =>
        {
            if (!warned)
            {
                return;
            }

            warned = false;
            if (game.PauseAct())
            {
                pauses++;
                Assert.True(game.ActPaused);
                twice &= !game.PauseAct();
            }
        });

        Assert.True(pauses >= 1);
        Assert.Equal(pauses, game.ActsPaused);
        Assert.True(twice, "An act that is already stopped cannot be stopped again.");
        Assert.Equal(0, game.CrushInjuries);
        Assert.False(game.SeriousIncident);
    }

    [Fact]
    public void People_in_a_queue_are_not_counted_as_a_crush()
    {
        var game = BuildDay(bigName: false);
        game.OpenGates();
        var worstBeforeTheMusic = 0.0;
        while (game.Now.TimeOfDay < TimeSpan.FromHours(11.5))
        {
            game.Clock.Step();
            worstBeforeTheMusic = Math.Max(worstBeforeTheMusic, game.Density.Worst);
        }

        Assert.True(game.Crowd.ThroughGates > 50);
        Assert.True(worstBeforeTheMusic < DensityMap.Crush, $"Worst was {worstBeforeTheMusic}.");
    }

    [Fact]
    public void A_crush_in_progress_is_saved_and_carries_on_the_same_after_loading()
    {
        var game = BuildDay(bigName: true);
        game.OpenGates();
        while (game.CrushInjuries < 2)
        {
            game.Clock.Step();
            Assert.True(game.Phase == Phase.Day, "Nobody was hurt all day.");
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.CrushInjuries, loaded.CrushInjuries);
        Assert.Contains(loaded.Incidents, i => i.Crush);

        foreach (var g in new[] { game, loaded })
        {
            var guard = 0;
            while (g.Phase == Phase.Day)
            {
                g.Clock.Step();
                Assert.True(++guard < 70000);
            }
        }

        Assert.Equal(game.CrushInjuries, loaded.CrushInjuries);
        Assert.Equal(game.SeriousIncident, loaded.SeriousIncident);
        Assert.Equal(game.Show.CalledOff, loaded.Show.CalledOff);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
    }
}
