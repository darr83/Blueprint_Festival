using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Food vans, bars and the gate are only heard near where the player is looking, and not over the stage music.</summary>
public class StallSoundTests
{
    /// <summary>A village day with a bar and a burger van (on the east side), a stage, and the gates open.</summary>
    private static FestivalGame Open()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        game.OpenGates();
        return game;
    }

    private static void Run(FestivalGame game, double hours)
    {
        var until = game.Now.AddHours(hours);
        while (game.Now < until && game.Phase == Phase.Day)
        {
            game.Clock.Step();
        }
    }

    private static readonly string[] Serving = { Sounds.PourDrink, Sounds.Fry, Sounds.Till };

    [Fact]
    public void A_stall_is_heard_close_to_and_silent_far_away_or_zoomed_out()
    {
        var close = StageMix.Stall(0, 0, 30, 0, 0);
        Assert.True(close > 0.3, $"Looking straight at it: {close}");
        Assert.True(StageMix.Stall(0, 0, 30, 8, 0) < close);
        Assert.Equal(0, StageMix.Stall(0, 0, 30, 80, 0));
        Assert.Equal(0, StageMix.Stall(0, 0, StageMix.FurthestView, 0, 0));

        // A stall carries less far than the fair.
        Assert.True(StageMix.Stall(0, 0, 60, 15, 0) < StageMix.Fair(0, 0, 60, 15, 0));
    }

    [Fact]
    public void The_bar_and_burger_van_are_not_heard_from_across_the_site_or_on_a_stage_camera()
    {
        var game = Open();
        using var script = new SoundScript(game);
        var heard = new List<(string Id, double Level)>();
        script.Heard += (id, level) => heard.Add((id, level));
        var f = game.Field.Field;
        script.Listen(f.X * FieldMap.TileMetres, f.Bottom * FieldMap.TileMetres, 30, filming: false);
        Run(game, 3);
        Assert.DoesNotContain(heard, h => Serving.Contains(h.Id));

        var bar = game.Site.Stalls.First(s => s.Kind == StallKind.Bar);
        var (bx, by) = SoundScript.MiddleOf(bar);
        script.Listen(bx, by, 30, filming: true);
        Run(game, 1);
        Assert.DoesNotContain(heard, h => Serving.Contains(h.Id));

        // Looking at the bar from close by it is heard, quieter than the old full volume.
        script.Listen(bx, by, 30, filming: false);
        Run(game, 2);
        var served = heard.Where(h => Serving.Contains(h.Id)).ToList();
        Assert.NotEmpty(served);
        Assert.All(served, h => Assert.InRange(h.Level, SoundScript.StallCueHeard, 0.8));
    }

    [Fact]
    public void Zoomed_in_on_a_playing_stage_the_music_drowns_the_stalls_nearby()
    {
        var game = Open();
        using var script = new SoundScript(game);
        var show = game.Shows[0];
        var until = game.Now.AddHours(10);
        while (show.Playing(game.Now) is null && game.Now < until)
        {
            game.Clock.Step();
        }

        Assert.NotNull(show.Playing(game.Now));
        var (sx, sy, _, _) = BlueprintFestival.Simulation.Show.RunningOrder.Front(show.Stage!);
        script.Listen(sx, sy, 20, filming: false);
        var music = script.StageHeard();
        Assert.True(music > 0.7, $"The stage zoomed in: {music}");

        // A stall standing right where the player is looking is much quieter with the band on.
        var bar = game.Site.Stalls.First(s => s.Kind == StallKind.Bar);
        var (bx, by) = SoundScript.MiddleOf(bar);
        script.Listen(bx, by, 20, filming: false);
        var withMusic = script.StallHeard(bar);
        var plain = StageMix.Stall(bx, by, 20, bx, by);
        Assert.True(withMusic <= plain * (1 - (SoundScript.StageDrowns * script.StageHeard())) + 1e-9);

        // From far off, the stage is barely heard, so a stall there is at its plain level.
        Assert.True(script.StageHeard() < music);
    }

    [Fact]
    public void Serving_sounds_and_the_till_are_quieter_than_before()
    {
        var volumes = SoundsJson();
        foreach (var id in new[] { Sounds.Till, Sounds.PourDrink, Sounds.Fry, Sounds.CoffeeMachine, Sounds.WokSizzle, Sounds.CocktailShaker })
        {
            Assert.True(volumes[id] <= 0.3, $"{id} is {volumes[id]}");
        }
    }

    private static Dictionary<string, double> SoundsJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "game", "Audio", "sounds.json")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "game", "Audio", "sounds.json")));
        return doc.RootElement.GetProperty("sounds").EnumerateArray()
            .ToDictionary(s => s.GetProperty("id").GetString()!, s => s.GetProperty("volume").GetDouble());
    }
}
