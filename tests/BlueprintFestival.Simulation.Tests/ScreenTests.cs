using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Big screens with speakers: putting them up, the ground they cover, and the people at the back they help.</summary>
[Collection(BigDays.Name)]
public class ScreenTests
{
    [Fact]
    public void There_are_three_sizes_each_bigger_and_dearer_and_reaching_further()
    {
        Assert.Equal(3, BigScreens.All.Count);
        for (var i = 1; i < BigScreens.All.Count; i++)
        {
            var (small, big) = (BigScreens.All[i - 1], BigScreens.All[i]);
            Assert.True(big.Cost > small.Cost && big.Reach > small.Reach && big.PictureWidth > small.PictureWidth && big.Width > small.Width);
            Assert.True(big.Speakers >= small.Speakers && small.Speakers >= 1);
        }

        Assert.Equal(new[] { 250, 600, 1200 }, BigScreens.All.Select(s => s.Cost));
        Assert.Equal(new[] { 25.0, 40.0, 60.0 }, BigScreens.All.Select(s => s.Reach));
    }

    [Fact]
    public void A_screen_goes_up_on_the_grass_is_paid_for_blocks_the_ground_and_comes_down_again()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        var money = game.Money;
        var (x, y) = (f.X + 20, f.Y + 30);

        Assert.Equal(250, game.CheckScreen(ScreenKind.Small, x, y, Facing.South).Cost);
        Assert.True(game.BuildScreen(ScreenKind.Small, x, y, Facing.South).Ok);
        Assert.Equal(money - 250, game.Money);
        Assert.Equal(250, game.Spending.Single(l => l.What == BigScreens.Line).Amount);
        var screen = Assert.Single(game.Site.Screens);
        Assert.Equal(new TileRect(x, y, 14, 6), screen.Rect);
        Assert.Equal(new TileRect(x, y, 6, 14), new PlacedScreen(ScreenKind.Small, x, y, Facing.East).Rect);

        // People walk round it, and nothing else goes on top of it.
        var inside = new TilePoint(x + 3, y + 2);
        Assert.False(game.Field.IsWalkable(inside));
        Assert.False(game.CheckScreen(ScreenKind.Small, x + 2, y + 1, Facing.South).Ok);
        Assert.False(game.CheckStall(StallKind.Bar, x + 2, y, Facing.South).Ok);
        Assert.False(game.CheckFence(FenceKind.Rope, new TilePoint(x - 2, y + 3), new TilePoint(x + 4, y + 3)).Ok);
        Assert.False(game.CheckLight(LightKind.PathLight, inside).Ok);
        Assert.False(game.CheckScreen(ScreenKind.Large, 0, 0, Facing.South).Ok);

        // Taking it down gives the hire back.
        Assert.Equal(250, game.Remove(inside));
        Assert.Equal(money, game.Money);
        Assert.Empty(game.Site.Screens);
        Assert.True(game.Field.IsWalkable(inside));
    }

    [Fact]
    public void A_screen_covers_a_fan_of_ground_in_front_of_it_out_to_its_reach()
    {
        // A medium screen facing south: its front edge is at y = 13 m, across the middle at x = 14.5 m.
        var screen = new PlacedScreen(ScreenKind.Medium, 20, 20, Facing.South);
        var (fx, fy, dx, dy) = SiteLayout.Front(screen.Rect, screen.Facing);
        Assert.Equal((14.5, 13.0, 0, 1), (fx, fy, dx, dy));

        Assert.True(BigScreens.Covers(screen, fx, fy + 10));
        Assert.True(BigScreens.Covers(screen, fx, fy + 39));
        Assert.False(BigScreens.Covers(screen, fx, fy + 41));
        Assert.False(BigScreens.Covers(screen, fx, fy - 5));

        // Up to 60 degrees off to the side, not beyond.
        Assert.True(BigScreens.Covers(screen, fx + 15, fy + 10));
        Assert.False(BigScreens.Covers(screen, fx + 20, fy + 5));

        // Turned to face east, it covers the ground to the east instead.
        var east = screen with { Facing = Facing.East };
        var (ex, ey, _, _) = SiteLayout.Front(east.Rect, east.Facing);
        Assert.True(BigScreens.Covers(east, ex + 20, ey));
        Assert.False(BigScreens.Covers(east, ex, ey + 20));
    }

    [Fact]
    public void Far_from_the_stage_an_act_is_enjoyed_less_without_a_screen()
    {
        Assert.Equal(1, BigScreens.Enjoyment(0));
        Assert.Equal(1, BigScreens.Enjoyment(BigScreens.ClearMetres));
        Assert.Equal(0.75, BigScreens.Enjoyment(40), 6);
        Assert.Equal(BigScreens.FarEnjoyment, BigScreens.Enjoyment(BigScreens.FarMetres));
        Assert.Equal(BigScreens.FarEnjoyment, BigScreens.Enjoyment(200));
    }

    [Fact]
    public void Screens_are_saved_with_the_site_and_old_saves_still_load()
    {
        var game = new FestivalGame(seed: 4);
        var f = game.Field.Field;
        Assert.True(game.BuildScreen(ScreenKind.Medium, f.X + 20, f.Y + 30, Facing.South).Ok);
        Assert.True(game.BuildScreen(ScreenKind.Large, f.X + 60, f.Y + 30, Facing.West).Ok);

        var json = System.Text.Json.JsonSerializer.Serialize(game.Save());
        var loaded = FestivalGame.Load(System.Text.Json.JsonSerializer.Deserialize<SaveData>(json)!);
        Assert.Equal(game.Site.Screens, loaded.Site.Screens);
        Assert.False(loaded.Field.IsWalkable(new TilePoint(f.X + 22, f.Y + 32)));
        Assert.NotNull(loaded.Crowd.OnScreen);

        var old = game.Save() with { Site = game.Site.Save() with { Screens = null } };
        Assert.Empty(FestivalGame.Load(old).Site.Screens);
    }

    [Fact]
    public void At_a_big_festival_screens_bring_the_act_to_the_people_at_the_back()
    {
        var bare = GrowthTests.Built(1500);
        var screened = GrowthTests.Built(1500);
        var stage = screened.Site.Stages[0].Rect;
        var middle = stage.X + (stage.Width / 2);

        // Two large screens out in the crowd, either side of the middle, facing away from the stage.
        Assert.True(screened.BuildScreen(ScreenKind.Large, middle - 40, stage.Bottom + 30, Facing.South).Ok);
        Assert.True(screened.BuildScreen(ScreenKind.Large, middle + 16, stage.Bottom + 30, Facing.South).Ok);
        Assert.Equal(bare.Money - 2400, screened.Money);

        foreach (var game in new[] { bare, screened })
        {
            game.OpenGates();
            var until = game.Day.AddHours(20.2);
            while (game.Phase == Phase.Day && game.Now < until)
            {
                game.Clock.Step();
            }
        }

        // Without screens some of the crowd is too far back to get all of the act.
        var far = bare.Crowd.Parties.Select(p => bare.Crowd.FromTheStage(p)).Where(m => m is not null).Select(m => m!.Value).ToList();
        Assert.True(bare.WatchingTooFar > 0, $"Somebody should be watching from far back: {far.Count} watching, furthest {(far.Count == 0 ? 0 : far.Max()):0} m, at {bare.Now:HH:mm}.");
        Assert.Equal(0, bare.WatchingOnScreen);
        Assert.Contains(bare.Crowd.Parties, p => bare.Crowd.ShareOfTheAct(p) < 1);

        // With screens, people at the back in front of one get all of it.
        var seen = screened.Crowd.Parties.Where(p => p.State == PartyState.Watching).Select(p => (screened.Crowd.FromTheStage(p) ?? 0, screened.Site.OnScreen(p.X, p.Y))).ToList();
        Assert.True(screened.WatchingOnScreen > 0, $"Somebody should be watching a screen: far {seen.Count(s => s.Item1 > 30)}, on screen {seen.Count(s => s.Item2)}, furthest {seen.Max(s => s.Item1):0}, too far {screened.WatchingTooFar}, bare too far {bare.WatchingTooFar}.");
        foreach (var party in screened.Crowd.Parties.Where(p => p.State == PartyState.Watching && screened.Site.OnScreen(p.X, p.Y)))
        {
            Assert.Equal(1, screened.Crowd.ShareOfTheAct(party));
        }
    }
}
