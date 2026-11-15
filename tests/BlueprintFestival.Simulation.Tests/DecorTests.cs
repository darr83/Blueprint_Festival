using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Decorations: bunting, flags, flowers, a fire pit, a giant sunflower and the festival's name in giant letters.</summary>
public class DecorTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    [Fact]
    public void There_are_six_decorations_and_bunting_by_the_metre()
    {
        Assert.Equal(Enum.GetValues<DecorKind>(), Decor.Everything.Select(d => d.Kind));
        Assert.Equal(new[] { 10, 12, 15, 40, 80, 150 }, Decor.Everything.Select(d => d.Cost));
        Assert.All(Decor.Everything, d => Assert.True(d.Charm > 0 && d.Charm <= Decor.MostCharm && d.Reach >= 6 && d.About.Length > 10));
        Assert.Equal(2, Decor.BuntingPerMetre);
        Assert.Equal(12, Decor.BuntingCost(new BuntingRun(new TilePoint(0, 0), new TilePoint(12, 0))));
        Assert.Equal(4, Decor.BuntingCost(new BuntingRun(new TilePoint(0, 0), new TilePoint(1, 0))));
    }

    [Fact]
    public void Giant_letters_spell_the_first_word_of_the_festivals_name()
    {
        Assert.Equal("LARKMOOR", Decor.WordFor("Larkmoor Festival"));
        Assert.Equal("STJOHNS", Decor.WordFor("  St.John's fete "));
        Assert.Equal("ABCDEFGHIJ", Decor.WordFor("Abcdefghijklmnop"));
        Assert.Equal("FESTIVAL", Decor.WordFor("!!!"));
        Assert.Equal("FESTIVAL", Decor.WordFor(null));
        Assert.True(Decor.LettersWidth("LARKMOOR") > Decor.LettersWidth("OAK"));

        var game = new FestivalGame("Oak Field Fair", seed: 8);
        var f = game.Field.Field;
        Assert.Equal("OAK", game.LettersWord);
        Assert.True(game.BuildDecor(DecorKind.GiantLetters, f.X + 20, f.Y + 20, Facing.South).Ok);
        var letters = Assert.Single(game.Site.Decorations);
        Assert.Equal("OAK", letters.Word);
        Assert.Equal(Decor.LettersWidth("OAK"), letters.Rect.Width);
        Assert.Equal(FestivalGame.StartingMoney - 150, game.Money);
    }

    [Fact]
    public void A_decoration_is_paid_for_stands_on_free_grass_and_comes_back_for_its_money()
    {
        var game = new FestivalGame("Decor", seed: 8);
        var f = game.Field.Field;
        var money = game.Money;
        Assert.True(game.BuildDecor(DecorKind.FirePit, f.X + 30, f.Y + 30, Facing.South).Ok);
        Assert.Equal(money - 40, game.Money);
        Assert.Equal(new LedgerLine("Decorations", 40), Assert.Single(game.Spending));
        Assert.False(game.Field.IsWalkable(new TilePoint(f.X + 31, f.Y + 31)));
        Assert.False(game.CheckDecor(DecorKind.Scarecrow, f.X + 31, f.Y + 31, Facing.South).Ok);
        Assert.False(game.CheckStall(StallKind.Bar, f.X + 26, f.Y + 26, Facing.South).Ok);
        Assert.False(game.CheckDecor(DecorKind.Scarecrow, f.X - 40, f.Y + 31, Facing.South).Ok);
        Assert.Equal(40, game.Remove(new TilePoint(f.X + 31, f.Y + 31)));
        Assert.Equal(money, game.Money);
        Assert.Empty(game.Site.Decorations);
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 31, f.Y + 31)));
    }

    [Fact]
    public void Bunting_is_strung_between_poles_paid_by_the_metre_and_people_walk_under_it()
    {
        var game = new FestivalGame("Decor", seed: 8);
        var f = game.Field.Field;
        var money = game.Money;
        var (a, b, c) = (new TilePoint(f.X + 20, f.Y + 20), new TilePoint(f.X + 36, f.Y + 20), new TilePoint(f.X + 36, f.Y + 32));
        Assert.False(game.CheckBunting(a, new TilePoint(a.X + 2, a.Y)).Ok);
        Assert.False(game.CheckBunting(a, new TilePoint(a.X + 40, a.Y)).Ok);
        Assert.True(game.BuildBunting(a, b).Ok);
        Assert.Equal(money - 16, game.Money);
        Assert.False(game.Field.IsWalkable(a));
        Assert.False(game.Field.IsWalkable(b));
        Assert.True(game.Field.IsWalkable(new TilePoint(f.X + 28, f.Y + 20)));
        Assert.False(game.CheckBunting(b, a).Ok);

        // It carries on from a pole, and nothing else is built on one.
        Assert.True(game.BuildBunting(b, c).Ok);
        Assert.Equal(money - 16 - 12, game.Money);
        Assert.Equal(2, game.Site.Bunting.Count);
        Assert.False(game.CheckDecor(DecorKind.Scarecrow, b.X, b.Y, Facing.South).Ok);

        // Taking down the shared pole takes both lines with it; an end pole takes one.
        Assert.Equal(12, game.Site.DecorRefundAt(c));
        Assert.Equal(28, game.Remove(b));
        Assert.Empty(game.Site.Bunting);
        Assert.Equal(money, game.Money);
        Assert.True(game.Field.IsWalkable(a));
    }

    [Fact]
    public void Charm_adds_up_near_decorations_but_only_so_far()
    {
        var game = new FestivalGame("Decor", seed: 8);
        var f = game.Field.Field;
        const double t = FieldMap.TileMetres;
        var (x, y) = ((f.X + 41) * t, (f.Y + 41) * t);
        Assert.Equal(0, game.Site.CharmAt(x, y));
        Assert.True(game.BuildDecor(DecorKind.Scarecrow, f.X + 40, f.Y + 40, Facing.South).Ok);
        Assert.Equal(0.2, game.Site.CharmAt(x, y), 3);
        Assert.Equal(0, game.Site.CharmAt(x + 30, y));
        Assert.True(game.BuildDecor(DecorKind.FirePit, f.X + 44, f.Y + 40, Facing.South).Ok);
        Assert.Equal(0.6, game.Site.CharmAt(x, y), 3);

        // The fire counts double after dark.
        Assert.Equal(1.0, game.Site.CharmAt(x, y, dark: true), 3);

        // A field stuffed with scarecrows is no better than one nicely dressed.
        for (var i = 0; i < 8; i++)
        {
            Assert.True(game.BuildDecor(DecorKind.Scarecrow, f.X + 36 + (i * 2), f.Y + 46, Facing.South).Ok);
        }

        Assert.Equal(Decor.MostCharm, game.Site.CharmAt(x, y), 3);
        Assert.True(game.BuildBunting(new TilePoint(f.X + 100, f.Y + 20), new TilePoint(f.X + 110, f.Y + 20)).Ok);
        Assert.Equal(Decor.BuntingCharm, game.Site.CharmAt((f.X + 105) * t, (f.Y + 22) * t), 3);
        Assert.InRange(Decor.CharmPerHour, 0.005, 0.04);
    }

    [Fact]
    public void Decorations_and_bunting_survive_saving()
    {
        var game = new FestivalGame("Larkmoor Festival", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildDecor(DecorKind.GiantLetters, f.X + 20, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildDecor(DecorKind.GiantSunflower, f.X + 20, f.Y + 30, Facing.East).Ok);
        Assert.True(game.BuildBunting(new TilePoint(f.X + 60, f.Y + 20), new TilePoint(f.X + 70, f.Y + 26)).Ok);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Site.Decorations, loaded.Site.Decorations);
        Assert.Equal(game.Site.Bunting, loaded.Site.Bunting);
        Assert.Equal(game.Money, loaded.Money);

        // An old save has no decorations in it, and loads.
        var old = SaveData.FromJson(game.Save().ToJson());
        var bare = FestivalGame.Load(old with { Site = old.Site! with { Decor = null, Bunting = null } });
        Assert.Empty(bare.Site.Decorations);
        Assert.Empty(bare.Site.Bunting);
    }

    [Fact]
    public void A_dressed_field_is_a_little_happier_and_no_more_than_a_little()
    {
        static FestivalGame Run(bool dressed)
        {
            var game = new FestivalGame("Decor", seed: 8);
            game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
            Amenities.Add(game);
            var f = game.Field.Field;
            if (dressed)
            {
                for (var i = 0; i < 4; i++)
                {
                    Assert.True(game.BuildDecor(DecorKind.GiantSunflower, f.X + 10 + (i * 14), f.Y + 30, Facing.South).Ok);
                    Assert.True(game.BuildDecor(DecorKind.FlagPole, f.X + 10 + (i * 14), f.Y + 56, Facing.South).Ok);
                }
            }

            var until = Day.AddHours(16);
            while (game.Now < until)
            {
                game.Clock.Step();
            }

            return game;
        }

        var (plain, dressed) = (Run(false), Run(true));
        Assert.True(dressed.Crowd.Happiness >= plain.Crowd.Happiness, $"{dressed.Crowd.Happiness} dressed against {plain.Crowd.Happiness} plain.");
        Assert.True(dressed.Crowd.Happiness <= plain.Crowd.Happiness + 10, $"{dressed.Crowd.Happiness} dressed against {plain.Crowd.Happiness} plain.");
    }
}
