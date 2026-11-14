using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class SecondStageTests
{
    /// <summary>
    /// A festival of 1,500 on Home Farm meadow on its build day: turnstiles, stalls down both
    /// hedges, first aid, and a hired stage in the north-west corner put up before the main stage.
    /// </summary>
    private static FestivalGame Built(bool book, bool secondStage = true)
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        game.ChooseHeadliner(0);
        game.BookSecondStage(book);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.TurnstileRow, gap.X, y, false).Ok);
        for (var lane = 0; lane < 4; lane++)
        {
            Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + (lane * 6) + 1, y)).Ok);
        }

        if (secondStage)
        {
            Assert.True(game.BuildStage(StageKind.HiredStage, f.X + 30, f.Y + 6, Facing.South).Ok);
        }

        Assert.True(game.BuildStage(StageKind.MainStage, f.Right - 90, f.Y + 6, Facing.South).Ok);
        var main = game.Site.Stages[^1].Rect;
        Assert.True(game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(main.X - 2, main.Bottom + 4), new TilePoint(main.Right + 1, main.Bottom + 4)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(main.X + 10, main.Bottom + 2)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(main.Right - 10, main.Bottom + 2)).Ok);
        var kinds = new[] { StallKind.FoodVan, StallKind.Bar, StallKind.ToiletBlock, StallKind.WaterPoint };
        for (var i = 0; i < 12; i++)
        {
            var east = i % 2 == 0;
            var x = east ? f.Right - 16 - (i / 6 * 40) : f.X + 4 + (i / 6 * 40);
            Assert.True(game.BuildStall(kinds[i % 4], x, f.Y + 50 + (i / 2 % 3 * 30), east ? Facing.West : Facing.East).Ok, $"Stall {i} would not fit.");
        }

        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 100, f.Y + 6, Facing.South).Ok);
        for (var i = 0; i < 4; i++)
        {
            Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 70 + (i * 23), f.Y + 80)).Ok);
            Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 60 + (i * 29), f.Y + 100)).Ok);
        }

        return game;
    }

    private static int Watching(FestivalGame game, bool second) =>
        game.Crowd.Parties.Where(p => p.State == PartyState.Watching && p.Watch >= 0 && p.Watch >= RunningOrder.SecondBase == second).Sum(p => p.Members.Count);

    [Fact]
    public void A_second_line_up_is_six_smaller_acts_that_are_a_choice_at_planning()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 60000, false, 80));
        Assert.False(game.SecondStageBooked);
        Assert.Equal(6, game.SecondShow.Slots.Count);
        Assert.All(game.SecondShow.Slots, s => Assert.True(s.Index >= RunningOrder.SecondBase && !s.Headliner && s.Act.Draw < 0.5));
        var names = game.Show.Slots.Concat(game.SecondShow.Slots).Select(s => s.Act.Name).Concat(game.Headliners.Select(h => h.Act.Name)).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());

        // Their sets overlap the main stage's, so there is a choice to make.
        Assert.Contains(game.SecondShow.Slots, s => game.Show.Slots.Any(m => s.Start < m.End && m.Start < s.End));

        var without = game.ActFees;
        game.BookSecondStage(true);
        Assert.True(game.SecondStageBooked);
        Assert.Equal(without + game.SecondStageFee, game.ActFees);
        Assert.True(game.SecondStageFee > 0);
        game.BookSecondStage(false);
        Assert.Equal(without, game.ActFees);

        game.BookSecondStage(true);
        var bank = game.Money;
        Assert.True(game.ConfirmPlan());
        Assert.Equal(bank - game.ActFees - game.FieldRent, game.Money);
        Assert.Throws<InvalidOperationException>(() => game.BookSecondStage(false));
    }

    [Fact]
    public void The_biggest_stage_is_the_main_stage_whichever_was_put_up_first()
    {
        var game = Built(book: true);
        Assert.Equal(StageKind.HiredStage, game.Site.Stages[0].Kind);
        Assert.Equal(StageKind.MainStage, game.Show.Stage!.Kind);
        Assert.Equal(StageKind.HiredStage, game.SecondShow.Stage!.Kind);
        Assert.Equal(new[] { StageKind.MainStage, StageKind.HiredStage }, RunningOrder.InOrder(game.Site).Select(s => s.Kind));

        // With one stage it is the main stage, and booked acts with no second stage do not play.
        var one = Built(book: true, secondStage: false);
        Assert.Equal(StageKind.MainStage, one.Show.Stage!.Kind);
        Assert.Null(one.SecondShow.Stage);
        Assert.Null(one.SecondShow.Playing(one.Day.AddHours(13.2)));

        // And a second stage with nobody booked for it stands empty.
        var unbooked = Built(book: false);
        Assert.Null(unbooked.SecondShow.Stage);
        Assert.Null(unbooked.SecondShow.Playing(unbooked.Day.AddHours(13.2)));
        Assert.NotNull(unbooked.Show.Playing(unbooked.Day.AddHours(13.5)));
    }

    [Fact]
    public void A_second_stage_splits_the_crowd_between_two_fronts()
    {
        var game = Built(book: true);
        var alone = Built(book: false);
        game.OpenGates();
        alone.OpenGates();
        int mostAtSecond = 0, mostAtMain = 0, mostAtMainAlone = 0, bothAtOnce = 0;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            if (alone.Phase == Phase.Day)
            {
                alone.Clock.Step();
            }

            if (game.Clock.TickCount % 120 == 0)
            {
                var second = Watching(game, second: true);
                var main = Watching(game, second: false);
                mostAtSecond = Math.Max(mostAtSecond, second);
                mostAtMain = Math.Max(mostAtMain, main);
                bothAtOnce = Math.Max(bothAtOnce, Math.Min(second, main));
                mostAtMainAlone = Math.Max(mostAtMainAlone, Watching(alone, second: false));
                Assert.Equal(0, Watching(alone, second: true));

                // Everyone watching the second stage is stood in front of it, not the main stage.
                if (second > 0)
                {
                    var (fx, fy, _, _) = RunningOrder.Front(game.SecondShow.Stage!);
                    Assert.All(game.Crowd.Parties.Where(p => p.State == PartyState.Watching && p.Watch >= RunningOrder.SecondBase),
                        p => Assert.True(Math.Abs(p.X - fx) < 45 && p.Y - fy is > 0 and < 60, $"Watching the second stage from {p.X:0},{p.Y:0}."));
                }
            }

            Assert.True(++guard < 70000, "The day never ended.");
        }

        Assert.True(mostAtSecond > 100, $"Most at the second stage {mostAtSecond}.");
        Assert.True(mostAtMain > 300, $"Most at the main stage {mostAtMain}.");
        Assert.True(bothAtOnce > 60, $"Most watching both at once {bothAtOnce}.");
        Assert.False(game.SeriousIncident);
        Assert.Equal(0, game.CrushInjuries);
        Assert.True(game.Crowd.Happiness >= alone.Crowd.Happiness - 3, $"{game.Crowd.Happiness}% with a second stage, {alone.Crowd.Happiness}% without.");
        Assert.Contains(game.Spending, l => l.What == "Act fees" && l.Amount == alone.Spending.Single(a => a.What == "Act fees").Amount + game.SecondStageFee);
    }

    [Fact]
    public void A_set_on_the_second_stage_saves_and_loads_and_plays_out_the_same()
    {
        var game = Built(book: true);
        game.OpenGates();
        while (game.Now.TimeOfDay < TimeSpan.FromHours(14.6))
        {
            game.Clock.Step();
        }

        Assert.NotNull(game.SecondShow.Playing(game.Now));
        Assert.True(Watching(game, second: true) > 0);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.True(loaded.SecondStageBooked);
        Assert.Equal(game.SecondShow.Playing(game.Now)!.Act.Name, loaded.SecondShow.Playing(loaded.Now)!.Act.Name);
        Assert.Equal(Watching(game, second: true), Watching(loaded, second: true));
        for (var i = 0; i < 3600; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Crowd.Watching, loaded.Crowd.Watching);
        Assert.Equal(Watching(game, second: true), Watching(loaded, second: true));
        Assert.Equal(game.Money, loaded.Money);
    }
}
