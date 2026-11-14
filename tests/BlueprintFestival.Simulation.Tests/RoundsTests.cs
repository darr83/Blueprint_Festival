using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class RoundsTests
{
    private static TilePoint GateTile(FestivalGame game) => new(game.Field.Gap.X + 1, game.Field.Gap.Y + 1);

    /// <summary>A planned village festival on its build day, with somebody on the gate.</summary>
    private static FestivalGame BuildDay(string name = "Larkmoor Festival")
    {
        var game = FestivalGame.NewFestival(name);
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, GateTile(game)).Ok);
        return game;
    }

    private static void RunTheDay(FestivalGame game, Action? eachTick = null)
    {
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            eachTick?.Invoke();
            Assert.True(++guard < 70000, "The day never ended.");
        }
    }

    private static double Away(StaffMember s) => Math.Sqrt(((s.X - s.PostX) * (s.X - s.PostX)) + ((s.Y - s.PostY) * (s.Y - s.PostY)));

    /// <summary>Bins along the front of the food and the bar, where people stand about with what they bought.</summary>
    private static void AddBins(FestivalGame game)
    {
        var f = game.Field.Field;
        foreach (var (x, y) in new[] { (f.Right - 30, f.Y + 6), (f.Right - 30, f.Y + 22), (f.Right - 30, f.Y + 38), (f.Right - 50, f.Y + 30), (f.Right - 70, f.Y + 44), (f.X + 40, f.Y + 40), (f.X + 56, f.Y + 24) })
        {
            var built = game.BuildStall(StallKind.Bin, x, y, Facing.South);
            Assert.True(built.Ok, $"No bin at {x - f.X},{y - f.Y}: {built.Problem} (£{game.Money})");
        }
    }

    [Fact]
    public void There_is_a_cleaner_to_hire_and_a_bin_to_put_out()
    {
        var cleaner = Catalogue.Of(StaffKind.Cleaner);
        Assert.Equal("Cleaner", cleaner.Name);
        Assert.True(cleaner.Wage > 0 && cleaner.Wage < Catalogue.Of(StaffKind.Security).Wage);
        var bin = Catalogue.Of(StallKind.Bin);
        Assert.Null(bin.Serves);
        Assert.Equal((2, 2), (bin.Width, bin.Depth));
        Assert.True(bin.Cost > 0);

        var game = BuildDay();
        var before = game.Money;
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Bin, f.X + 40, f.Y + 40, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 44, f.Y + 44)).Ok);
        Assert.Equal(before - bin.Cost - cleaner.Wage, game.Money);
        Assert.Equal(0, game.BinFill(game.Site.Stalls[^1]));
    }

    [Fact]
    public void Security_and_first_aiders_walk_a_beat_round_their_posts_and_ticket_staff_stay_on_the_gate()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 40, f.Y + 50)).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70, f.Y + 50)).Ok);
        var (ticket, security, aider) = (game.Staff[0], game.Staff[1], game.Staff[2]);
        var furthest = new Dictionary<StaffMember, double> { [ticket] = 0, [security] = 0, [aider] = 0 };
        var walked = new HashSet<StaffMember>();
        var stopped = new HashSet<StaffMember>();

        RunTheDay(game, () =>
        {
            foreach (var s in game.Staff)
            {
                if (s.State is StaffState.Patrolling or StaffState.Looking)
                {
                    furthest[s] = Math.Max(furthest[s], Away(s));
                    (s.State == StaffState.Patrolling ? walked : stopped).Add(s);
                    Assert.True(s.IsFree, "Someone on their beat can be sent to whatever happens.");
                    Assert.True(game.Field.IsWalkableForStaff(FieldMap.TileAt(s.X, s.Y)));
                }
            }
        });

        Assert.Equal(0, furthest[ticket]);
        Assert.Contains(security, walked);
        Assert.Contains(security, stopped);
        Assert.Contains(aider, walked);
        Assert.InRange(furthest[security], 5, FestivalGame.BeatOf(StaffKind.Security) + 1);
        Assert.InRange(furthest[aider], 5, FestivalGame.BeatOf(StaffKind.FirstAider) + 1);

        // When the day is done everyone is back where they were posted.
        Assert.All(game.Staff, s => Assert.Equal((StaffState.AtPost, 0.0), (s.State, Away(s))));
    }

    [Fact]
    public void Security_minding_the_front_of_a_stage_stay_there_while_an_act_is_on()
    {
        var game = BuildDay();
        var stage = game.Site.Stages[0];
        var (fx, fy, dx, dy) = Show.RunningOrder.Front(stage);
        Assert.True(game.HireStaff(StaffKind.Security, FieldMap.TileAt(fx + (dx * 3), fy + (dy * 3))).Ok);
        var guard = game.Staff[^1];
        var beat = false;

        RunTheDay(game, () =>
        {
            if (game.MusicOn && game.Clock.TickCount % 60 == 0 && guard.Incident < 0 && guard.State == StaffState.AtPost)
            {
                Assert.Equal(0, Away(guard));
            }

            if (game.MusicOn)
            {
                Assert.NotEqual(StaffState.Looking, guard.State);
            }

            beat |= guard.State == StaffState.Patrolling;
        });

        Assert.True(beat, "Between acts they stretch their legs like anyone else.");
    }

    [Fact]
    public void People_near_a_bin_put_their_rubbish_in_it()
    {
        var bare = BuildDay();
        RunTheDay(bare);
        Assert.True(bare.Litter.Count > 20, $"Only {bare.Litter.Count} pieces were dropped with no bins.");
        Assert.Equal(0, bare.LitterBinned);

        var game = BuildDay();
        AddBins(game);
        RunTheDay(game);

        Assert.True(game.LitterBinned > 10, $"Only {game.LitterBinned} pieces went in the bins.");
        Assert.True(game.Litter.Count < bare.Litter.Count, $"{game.Litter.Count} on the ground with bins, {bare.Litter.Count} without.");
        var bins = game.Site.Stalls.Where(s => s.Kind == StallKind.Bin).ToList();
        Assert.Equal(game.LitterBinned, bins.Sum(game.BinFill));
        Assert.All(bins, b => Assert.InRange(game.BinFill(b), 0, FestivalGame.BinHolds));

        // Not everybody bothers, even with a bin close by.
        Assert.True(game.Litter.Count > 0);
    }

    [Fact]
    public void Cleaners_walk_to_litter_and_pick_it_up()
    {
        var bare = BuildDay();
        RunTheDay(bare);

        var game = BuildDay();
        var f = game.Field.Field;
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 60, f.Y + 40)).Ok);
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 90, f.Y + 30)).Ok);
        var states = new HashSet<StaffState>();
        RunTheDay(game, () => states.Add(game.Staff[1].State));

        Assert.Contains(StaffState.Cleaning, states);
        Assert.Contains(StaffState.Picking, states);
        Assert.Contains(StaffState.Patrolling, states);
        Assert.True(game.LitterPicked > 10, $"The cleaners only picked up {game.LitterPicked}.");
        Assert.True(game.Litter.Count < bare.Litter.Count / 2, $"{game.Litter.Count} left with cleaners, {bare.Litter.Count} without.");
    }

    [Fact]
    public void Cleaners_carry_on_the_morning_after_and_a_full_bin_gets_emptied()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        AddBins(game);

        // One cleaner for the whole field does not get round all of it in the day.
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 6, f.Y + 70)).Ok);
        RunTheDay(game);
        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.True(game.BinsEmptied > 0, "A day's rubbish fills at least one bin.");
        Assert.True(game.LitterPicked > 0);

        // Whatever is lying about the morning after (here, put there by hand) the cleaner goes on picking up.
        var litter = (List<LitterPiece>)typeof(FestivalGame).GetField("_litter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(game)!;
        var (cx, cy) = FieldMap.CentreOf(new TilePoint(f.X + 60, f.Y + 40));
        for (var i = 0; i < 6; i++)
        {
            litter.Add(new LitterPiece(cx + (i * 4), cy + i));
        }

        var left = game.Litter.Count;
        for (var tick = 0; tick < 4000 && game.Litter.Count > 0; tick++)
        {
            game.Clock.Step();
        }

        Assert.Equal(Phase.Tidy, game.Phase);
        Assert.True(game.Litter.Count < left, "A cleaner should keep picking up on the morning after.");
    }

    [Fact]
    public void Rounds_bins_and_litter_counts_come_back_from_a_save()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        AddBins(game);
        Assert.True(game.HireStaff(StaffKind.Cleaner, new TilePoint(f.X + 60, f.Y + 40)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 40, f.Y + 50)).Ok);
        game.OpenGates();
        while (game.Phase == Phase.Day && (game.LitterBinned < 5 || game.LitterPicked < 3 || !game.Staff.Any(s => s.State == StaffState.Patrolling)))
        {
            game.Clock.Step();
        }

        Assert.Equal(Phase.Day, game.Phase);
        var back = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal((game.LitterBinned, game.LitterPicked, game.BinsEmptied), (back.LitterBinned, back.LitterPicked, back.BinsEmptied));
        Assert.Equal(game.Site.Stalls.Select(game.BinFill), back.Site.Stalls.Select(back.BinFill));
        Assert.Equal(game.Staff.Select(s => (s.State, s.X, s.Y, s.Wait)), back.Staff.Select(s => (s.State, s.X, s.Y, s.Wait)));

        // And the two play on the same from there.
        for (var tick = 0; tick < 1800; tick++)
        {
            game.Clock.Step();
            back.Clock.Step();
        }

        Assert.Equal(game.Staff.Select(s => (s.State, s.X, s.Y)), back.Staff.Select(s => (s.State, s.X, s.Y)));
        Assert.Equal(game.Litter.Count, back.Litter.Count);
    }
}
