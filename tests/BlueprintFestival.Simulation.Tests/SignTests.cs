using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Signs the player puts up and writes on.</summary>
public class SignTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    [Fact]
    public void There_are_four_signs_each_with_a_price_and_words_to_start_with()
    {
        Assert.Equal(Enum.GetValues<SignKind>(), Signs.Everything.Select(s => s.Kind));
        Assert.Equal(new[] { 6, 10, 35, 45 }, Signs.Everything.Select(s => s.Cost));
        Assert.All(Signs.Everything, s => Assert.True(s.Lines is >= 1 and <= 3 && s.LineLength >= 10 && s.Words.Length <= s.Lines && s.About.Length > 10));
        Assert.All(Signs.Everything, s => Assert.Equal(s.Words, Signs.Clean(s.Kind, s.Words)));
    }

    [Fact]
    public void What_is_typed_is_tidied_to_fit_the_sign()
    {
        Assert.Equal(new[] { "BURGERS", "2 for £9" }, Signs.Clean(SignKind.SandwichBoard, new[] { "  BURGERS ", "2 for £9", "", null }));
        Assert.Equal("A very long", Signs.Clean(SignKind.SandwichBoard, new[] { "A very long line of words" })[0]);
        Assert.All(Signs.Clean(SignKind.SandwichBoard, new[] { "A very long line of words", "x", "y", "z" }), l => Assert.True(l.Length <= 12));
        Assert.Equal(3, Signs.Clean(SignKind.SandwichBoard, new[] { "a", "b", "c", "d" }).Count);
        Assert.Single(Signs.Clean(SignKind.Banner, new[] { "WELCOME", "second line" }));
        Assert.Equal(new[] { "Teabcake" }, Signs.Clean(SignKind.Banner, new[] { "Tea\n<b>\tcake" }));
        Assert.Equal(new[] { "", "Toilets" }, Signs.Clean(SignKind.FingerPost, new[] { "", "Toilets", " " }));
        Assert.Empty(Signs.Clean(SignKind.Banner, null));
    }

    [Fact]
    public void A_sign_is_paid_for_written_on_for_nothing_and_comes_back_for_its_money()
    {
        var game = new FestivalGame("Signs", seed: 8);
        var f = game.Field.Field;
        var money = game.Money;
        Assert.True(game.BuildSign(SignKind.FingerPost, f.X + 30, f.Y + 30, Facing.South).Ok);
        Assert.Equal(money - 10, game.Money);
        Assert.Equal(new LedgerLine("Signs", 10), Assert.Single(game.Spending));
        var post = Assert.Single(game.Site.Signs);
        Assert.Equal(new[] { "Stage", "Toilets", "Food" }, post.Lines);
        Assert.True(post.PointsRight(0) && !post.PointsRight(1) && post.PointsRight(2));
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 30, f.Y + 30)));
        Assert.False(game.CheckSign(SignKind.SandwichBoard, f.X + 29, f.Y + 29, Facing.South).Ok);
        Assert.False(game.CheckStall(StallKind.Bar, f.X + 26, f.Y + 26, Facing.South).Ok);

        var written = 0;
        var moved = 0;
        game.Site.SignWritten += () => written++;
        game.Site.Changed += () => moved++;
        Assert.True(game.WriteSign(new TilePoint(f.X + 30, f.Y + 30), new[] { "Way out", "", "Bar & food" }, 2, 0b010));
        post = Assert.Single(game.Site.Signs);
        Assert.Equal(new[] { "Way out", "", "Bar & food" }, post.Lines);
        Assert.Equal(2, post.Colour);
        Assert.True(!post.PointsRight(0) && post.PointsRight(1) && !post.PointsRight(2));
        Assert.Equal(money - 10, game.Money);
        Assert.Equal((1, 0), (written, moved));
        Assert.False(game.WriteSign(new TilePoint(f.X + 40, f.Y + 40), new[] { "x" }, 0, 0));
        Assert.True(game.WriteSign(new TilePoint(f.X + 30, f.Y + 30), new[] { "x" }, 99, 0));
        Assert.Equal(Signs.Colours - 1, game.Site.Signs[0].Colour);

        Assert.Equal(10, game.Remove(new TilePoint(f.X + 30, f.Y + 30)));
        Assert.Equal(money, game.Money);
        Assert.Empty(game.Site.Signs);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 30, f.Y + 30)));
    }

    [Fact]
    public void People_walk_under_a_banner_but_not_through_its_poles()
    {
        var game = new FestivalGame("Signs", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildSign(SignKind.Banner, f.X + 30, f.Y + 30, Facing.South).Ok);
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 30, f.Y + 30)));
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 38, f.Y + 30)));
        Assert.All(Enumerable.Range(31, 7), x => Assert.True(game.Field.IsWalkable(new TilePoint(f.X + x, f.Y + 30))));

        // Turned, it runs the other way. Nothing is built under one.
        Assert.True(game.BuildSign(SignKind.Banner, f.X + 50, f.Y + 30, Facing.East).Ok);
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 50, f.Y + 38)));
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 50, f.Y + 34)));
        Assert.False(game.CheckStall(StallKind.Bin, f.X + 34, f.Y + 30, Facing.South).Ok);
    }

    [Fact]
    public void Signs_and_their_words_survive_saving()
    {
        var game = new FestivalGame("Signs", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildSign(SignKind.SandwichBoard, f.X + 20, f.Y + 20, Facing.West).Ok);
        Assert.True(game.BuildSign(SignKind.MapBoard, f.X + 30, f.Y + 20, Facing.South).Ok);
        Assert.True(game.WriteSign(new TilePoint(f.X + 20, f.Y + 20), new[] { "PIES", "£4 each" }, 3, 0));
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Site.Signs.Select(s => (s.Kind, s.X, s.Y, s.Facing, string.Join("|", s.Lines!), s.Colour, s.Points)),
            loaded.Site.Signs.Select(s => (s.Kind, s.X, s.Y, s.Facing, string.Join("|", s.Lines!), s.Colour, s.Points)));
        Assert.Equal(game.Money, loaded.Money);

        // An old save has no signs in it, and loads.
        var old = SaveData.FromJson(game.Save().ToJson());
        Assert.Empty(FestivalGame.Load(old with { Site = old.Site! with { Signs = null } }).Site.Signs);
    }

    [Fact]
    public void A_sandwich_board_counts_for_stalls_near_it_and_posts_and_maps_show_the_way_near_them()
    {
        var game = new FestivalGame("Signs", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.IceCream, f.X + 20, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Coffee, f.X + 70, f.Y + 20, Facing.South).Ok);
        var (near, far) = (game.Site.Stalls[0], game.Site.Stalls[1]);
        Assert.False(game.Site.Advertised(near));
        Assert.True(game.BuildSign(SignKind.Banner, f.X + 20, f.Y + 32, Facing.South).Ok);
        Assert.False(game.Site.Advertised(near));
        Assert.True(game.BuildSign(SignKind.SandwichBoard, f.X + 22, f.Y + 30, Facing.South).Ok);
        Assert.True(game.Site.Advertised(near));
        Assert.False(game.Site.Advertised(far));

        const double t = FieldMap.TileMetres;
        Assert.False(game.Site.WayShown((f.X + 22) * t, (f.Y + 31) * t));
        Assert.True(game.BuildSign(SignKind.FingerPost, f.X + 60, f.Y + 50, Facing.South).Ok);
        Assert.True(game.Site.WayShown((f.X + 60) * t, ((f.Y + 50) * t) + Signs.PostReach - 1));
        Assert.False(game.Site.WayShown((f.X + 60) * t, ((f.Y + 50) * t) + Signs.PostReach + 1));
        Assert.True(game.BuildSign(SignKind.MapBoard, f.X + 10, f.Y + 60, Facing.South).Ok);
        Assert.True(game.Site.WayShown(((f.X + 12) * t) + Signs.MapReach - 2, (f.Y + 60) * t));
        Assert.True(Signs.BoardPull > 1 && Signs.ShownWalk < 1 && Signs.MapReach > Signs.PostReach);
    }

    [Fact]
    public void A_stall_with_a_sandwich_board_sells_at_least_as_much()
    {
        static int Sold(bool board)
        {
            var game = new FestivalGame("Signs", seed: 8);
            game.SetWeather(DayWeather.Of(WeatherKind.Hot, 3));
            Amenities.Add(game);
            var f = game.Field.Field;
            Assert.True(game.BuildStall(StallKind.IceCream, f.X + 30, f.Y + 30, Facing.South).Ok);
            if (board)
            {
                Assert.True(game.BuildSign(SignKind.SandwichBoard, f.X + 40, f.Y + 34, Facing.South).Ok);
            }

            var until = Day.AddHours(18);
            while (game.Now < until)
            {
                game.Clock.Step();
            }

            return game.SalesOf(game.Site.Stalls.Count - 1).Items;
        }

        var (without, with) = (Sold(false), Sold(true));
        Assert.True(without > 0 && with > without, $"{with} ices with a board against {without} without.");
    }
}
