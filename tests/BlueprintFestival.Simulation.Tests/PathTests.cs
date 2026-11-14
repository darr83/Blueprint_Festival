using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class PathTests
{
    private static readonly DayWeather Wet = DayWeather.Of(WeatherKind.Rain, 3);

    /// <summary>A rainy day with a gravel path from the gate up to the stage, run once.</summary>
    private static readonly Lazy<(FestivalGame Game, int OnPath, int OffPath, int Keepers, int Walkers)> PathDay = new(() => Run(Wet));

    /// <summary>A planned village festival on its build day, with a gate, a bandstand, stalls and staff.</summary>
    private static FestivalGame BuildDay(DayWeather? weather)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        if (weather is not null)
        {
            game.SetWeather(weather);
        }

        game.ChooseHeadliner(game.Headliners.ToList().FindIndex(h => h.Stars == game.Headliners.Min(o => o.Stars)));
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        return game;
    }

    /// <summary>The patch just inside the way in, and the patch in front of the stage.</summary>
    private static ((int Column, int Row) Gate, (int Column, int Row) Stage) Ends(FestivalGame game)
    {
        var gap = game.Field.Gap;
        var gate = game.PatchAt(new TilePoint(gap.X + (gap.Width / 2), game.Field.Field.Bottom - 1))!.Value;
        var rect = game.Site.Stages[0].Rect;
        var stage = game.PatchAt(new TilePoint(rect.X + (rect.Width / 2), rect.Bottom + 6))!.Value;
        return (gate, stage);
    }

    /// <summary>Lays a gravel path from the gate north, then east to the front of the stage.</summary>
    private static void LayGravel(FestivalGame game)
    {
        var (gate, stage) = Ends(game);
        Assert.True(game.LayPath(GroundKind.Gravel, gate, (gate.Column, stage.Row)).Ok);
        Assert.True(game.LayPath(GroundKind.Gravel, (gate.Column, stage.Row), stage).Ok);
    }

    private static (FestivalGame, int, int, int, int) Run(DayWeather weather)
    {
        var game = BuildDay(weather);
        LayGravel(game);
        game.OpenGates();
        int onPath = 0, offPath = 0, keepers = 0, walkers = 0;
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
            if (game.Clock.TickCount % 120 != 0)
            {
                continue;
            }

            foreach (var party in game.Crowd.Parties.Where(p => p.IsOnSite))
            {
                if (game.PatchAt(FieldMap.TileAt(party.X, party.Y)) is var (c, r))
                {
                    var walking = party.State is People.PartyState.WalkingIn or People.PartyState.Strolling;
                    if (walking)
                    {
                        walkers++;
                        keepers += game.Crowd.KeepsToPaths(party) ? 1 : 0;
                        if (game.PathAt(c, r))
                        {
                            onPath++;
                        }
                        else
                        {
                            offPath++;
                        }
                    }
                }
            }
        }

        return (game, onPath, offPath, keepers, walkers);
    }

    [Fact]
    public void Paths_are_cheap_mown_dearer_bark_and_dearest_gravel_and_straw_is_not_a_path()
    {
        Assert.True(FestivalGame.CostOf(GroundKind.MownPath) < FestivalGame.CostOf(GroundKind.BarkChip));
        Assert.True(FestivalGame.CostOf(GroundKind.BarkChip) < FestivalGame.CostOf(GroundKind.Gravel));
        Assert.True(FestivalGame.CostOf(GroundKind.Gravel) < FestivalGame.CostOf(GroundKind.Trackway));
        Assert.False(FestivalGame.IsPath(GroundKind.Straw));
        Assert.True(FestivalGame.IsPath(GroundKind.Trackway));
        Assert.True(FestivalGame.IsPath(GroundKind.MownPath));
        Assert.Equal("Bark chip path", FestivalGame.NameOf(GroundKind.BarkChip));
    }

    [Fact]
    public void A_path_is_laid_in_a_straight_line_one_patch_wide_and_only_new_patches_are_paid_for()
    {
        var game = BuildDay(null);
        var line = game.PathLine((2, 5), (9, 7)).ToList();
        Assert.Equal(8, line.Count);
        Assert.All(line, p => Assert.Equal(5, p.Row));

        var down = game.PathLine((4, 9), (3, 2)).ToList();
        Assert.Equal(8, down.Count);
        Assert.All(down, p => Assert.Equal(4, p.Column));

        // A line running off the edge stops at the hedge.
        Assert.Equal(3, game.PathLine((game.WearColumns - 3, 1), (game.WearColumns + 5, 1)).Count());

        var before = game.Money;
        Assert.True(game.LayGround(GroundKind.Straw, 4, 5).Ok);
        Assert.True(game.LayPath(GroundKind.BarkChip, (2, 5), (6, 5)).Ok);
        Assert.Equal(before - FestivalGame.CostOf(GroundKind.Straw) - (5 * FestivalGame.CostOf(GroundKind.BarkChip)), game.Money);
        Assert.Equal(GroundKind.BarkChip, game.GroundAt(4, 5));
        Assert.Equal(5, game.PathPatches);
        Assert.Contains(game.Spending, l => l.What == "Paths");

        // Running the same path on further only charges for the new stretch.
        var check = game.CheckPath(GroundKind.BarkChip, (2, 5), (8, 5));
        Assert.Equal(2 * FestivalGame.CostOf(GroundKind.BarkChip), check.Cost);
        Assert.False(game.LayPath(GroundKind.BarkChip, (3, 5), (5, 5)).Ok);
        Assert.True(game.Crowd.HasPaths);

        // Lifting the lot takes the path away from the crowd's map too.
        foreach (var (c, r) in game.PathLine((2, 5), (6, 5)).ToList())
        {
            Assert.Equal(FestivalGame.CostOf(GroundKind.BarkChip), game.LiftGround(c, r));
        }

        Assert.Equal(0, game.PathPatches);
        Assert.False(game.Crowd.HasPaths);
    }

    [Fact]
    public void Someone_who_keeps_to_paths_goes_out_of_their_way_to_walk_on_one_and_others_cut_across()
    {
        var map = FieldMap.Village();
        var finder = new PathFinder(map);
        var f = map.Field;

        // An L-shaped path: along the bottom of the field, then up the east side.
        var path = new List<TilePoint>();
        for (var x = f.X + 4; x < f.Right - 4; x++)
        {
            for (var y = f.Bottom - 8; y < f.Bottom - 4; y++)
            {
                path.Add(new TilePoint(x, y));
            }
        }

        for (var y = f.Y + 4; y < f.Bottom - 4; y++)
        {
            for (var x = f.Right - 8; x < f.Right - 4; x++)
            {
                path.Add(new TilePoint(x, y));
            }
        }

        finder.SetPaths(path);
        var onPath = new HashSet<TilePoint>(path);
        var from = new TilePoint(f.X + 6, f.Bottom - 6);
        var to = new TilePoint(f.Right - 6, f.Y + 6);

        var straight = finder.FindPath(from, to)!;
        var kept = finder.FindPath(from, to, keepToPaths: true)!;
        Assert.Equal(2, straight.Count);

        var walked = Walked(kept).ToList();
        Assert.True(walked.Count(onPath.Contains) > walked.Count * 0.9, $"{walked.Count(onPath.Contains)} of {walked.Count} steps on the path.");
        Assert.True(Walked(straight).Count(onPath.Contains) < walked.Count(onPath.Contains) / 3);
        Assert.Equal(to, kept[^1]);
    }

    [Fact]
    public void About_half_the_crowd_keeps_to_paths_on_a_dry_day_and_most_do_once_the_grass_is_wet()
    {
        var game = BuildDay(null);
        var parties = game.Crowd.Parties;
        var keepers = parties.Count(game.Crowd.KeepsToPaths);
        Assert.InRange(keepers, parties.Count * 0.45, parties.Count * 0.65);

        var wet = PathDay.Value.Game;
        Assert.True(wet.Wetness > 0.5, $"Wetness {wet.Wetness}");
        Assert.InRange(wet.Crowd.PathShare, 0.85, 0.95);

        // Those who keep to paths on a dry day still do on a wet one.
        var dry = parties.Where(game.Crowd.KeepsToPaths).Select(p => p.Id).ToHashSet();
        Assert.All(wet.Crowd.Parties.Where(p => dry.Contains(p.Id)), p => Assert.True(wet.Crowd.KeepsToPaths(p)));
    }

    [Fact]
    public void People_walking_about_are_on_the_path_far_more_than_its_share_of_the_field()
    {
        var (game, onPath, offPath, keepers, walkers) = PathDay.Value;
        var share = (double)game.PathPatches / (game.WearColumns * game.WearRows);
        var walkedOnPath = (double)onPath / (onPath + offPath);
        Assert.True(walkedOnPath > share * 3, $"{walkedOnPath:P0} of steps on a path that is {share:P0} of the field.");
        Assert.True(keepers < walkers, "Some people should still cut across.");
    }

    [Fact]
    public void A_gravel_path_stays_clear_of_mud_while_the_grass_beside_it_does_not()
    {
        var game = PathDay.Value.Game;
        var (gate, stage) = Ends(game);
        var column = gate.Column;
        var muddiest = 0.0;
        for (var row = stage.Row; row <= gate.Row; row++)
        {
            Assert.Equal(GroundKind.Gravel, game.GroundAt(column, row));
            Assert.True(game.MudAt(column, row) < 0.1, $"Mud {game.MudAt(column, row)} on the gravel.");
            Assert.True(game.WearAt(column, row) < 0.5);
            muddiest = Math.Max(muddiest, Math.Max(game.MudAt(column - 1, row), game.MudAt(column + 1, row)));
        }

        Assert.True(muddiest > 0.1, $"The grass beside the path only got to {muddiest} mud.");
    }

    [Fact]
    public void Only_patches_with_a_path_on_count_as_path_and_straw_does_not()
    {
        var game = BuildDay(Wet);
        Assert.True(game.LayPath(GroundKind.MownPath, (2, 2), (2, 2)).Ok);
        Assert.True(game.LayPath(GroundKind.BarkChip, (3, 2), (3, 2)).Ok);
        Assert.True(game.LayGround(GroundKind.Straw, 4, 2).Ok);
        Assert.Equal(2, game.PathPatches);
        Assert.Equal(3, game.GroundCovered);
        Assert.True(game.PathAt(2, 2));
        Assert.True(game.PathAt(3, 2));
        Assert.False(game.PathAt(4, 2));
    }

    [Fact]
    public void Paths_come_back_after_loading_and_the_day_carries_on_the_same()
    {
        var game = BuildDay(Wet);
        LayGravel(game);
        game.OpenGates();
        while (game.Now.Hour < 15)
        {
            game.Clock.Step();
        }

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.PathPatches, loaded.PathPatches);
        Assert.True(loaded.Crowd.HasPaths);
        Assert.Equal(game.Crowd.PathShare, loaded.Crowd.PathShare);
        for (var i = 0; i < 1800; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.GrassWorn, loaded.GrassWorn);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
        Assert.Equal(game.Crowd.Parties.Select(p => (p.X, p.Y)), loaded.Crowd.Parties.Select(p => (p.X, p.Y)));
    }

    /// <summary>Every tile walked through along a route, filling in the straight stretches.</summary>
    private static IEnumerable<TilePoint> Walked(List<TilePoint> path)
    {
        yield return path[0];
        for (var i = 1; i < path.Count; i++)
        {
            var (a, b) = (path[i - 1], path[i]);
            var steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            for (var s = 1; s <= steps; s++)
            {
                yield return new TilePoint(a.X + (int)Math.Round((b.X - a.X) * (double)s / steps), a.Y + (int)Math.Round((b.Y - a.Y) * (double)s / steps));
            }
        }
    }
}
