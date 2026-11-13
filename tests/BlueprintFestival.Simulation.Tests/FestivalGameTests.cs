using BlueprintFestival.Simulation.Core;

namespace BlueprintFestival.Simulation.Tests;

public class FestivalGameTests
{
    [Theory]
    [InlineData(null, "Larkmoor Festival")]
    [InlineData("   ", "Larkmoor Festival")]
    [InlineData("  Little   Wickham  Fayre ", "Little Wickham Fayre")]
    public void Names_are_tidied_and_default_to_Larkmoor(string? typed, string expected)
    {
        Assert.Equal(expected, new FestivalGame(typed).Name);
    }

    [Fact]
    public void Very_long_names_are_cut_short()
    {
        Assert.Equal(FestivalGame.MaxNameLength, new FestivalGame(new string('x', 200)).Name.Length);
    }

    [Fact]
    public void A_whole_day_runs_from_morning_light_into_the_dark()
    {
        var game = new FestivalGame();
        Assert.Equal(1, game.Light, 3);
        game.Clock.SetSpeed(GameSpeed.Fastest);
        var until = game.Now.Date.AddHours(23);
        while (game.Now < until)
        {
            game.Advance(1);
        }

        Assert.Equal(0, game.Light, 3);
        Assert.Equal(game.Now.Date, GameClock.DefaultStart.Date);
    }

    [Fact]
    public void A_game_saves_and_loads_through_json()
    {
        var game = new FestivalGame("Ashby Green Weekender");
        game.Clock.SetSpeed(GameSpeed.Fast);
        game.Advance(120);

        var json = game.Save().ToJson();
        var loaded = FestivalGame.Load(SaveData.FromJson(json));

        Assert.Equal(game.Name, loaded.Name);
        Assert.Equal(game.Now, loaded.Now);
        Assert.Equal(game.Clock.TickCount, loaded.Clock.TickCount);
        Assert.Equal(GameSpeed.Fast, loaded.Clock.Speed);
        Assert.Equal(game.Field.Width, loaded.Field.Width);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("null")]
    public void A_broken_save_file_gives_a_clear_error(string json)
    {
        Assert.Throws<InvalidDataException>(() => SaveData.FromJson(json));
    }

    [Fact]
    public void A_save_from_a_newer_game_is_refused()
    {
        var data = new FestivalGame().Save() with { Version = SaveData.CurrentVersion + 1 };
        Assert.Throws<InvalidDataException>(() => FestivalGame.Load(data));
    }
}
