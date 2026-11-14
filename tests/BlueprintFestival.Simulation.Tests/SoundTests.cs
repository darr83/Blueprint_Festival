using System.Reflection;
using System.Text.Json;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class SoundTests
{
    /// <summary>A whole village day, in fine weather or wet, with every sound that was asked for written down.</summary>
    private static (FestivalGame Game, List<string> Heard, Dictionary<string, double> Loudest) Day(DayWeather? weather)
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        if (weather is not null)
        {
            game.SetWeather(weather);
        }

        using var script = new SoundScript(game);
        var heard = new List<string>();
        script.Cue += heard.Add;
        var loudest = Sounds.Loops.ToDictionary(id => id, _ => 0.0);

        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        script.Poll();
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        Amenities.Add(game);
        Assert.True(game.BuildStall(StallKind.TeaAndCake, f.X + 8, f.Y + 40, Facing.East).Ok);
        Assert.True(game.BuildStall(StallKind.FirstAidTent, f.X + 30, f.Y + 6, Facing.South).Ok);
        Assert.True(game.HireStaff(StaffKind.FirstAider, new TilePoint(f.X + 34, f.Y + 14)).Ok);
        Assert.True(game.HireStaff(StaffKind.Security, new TilePoint(f.X + 60, f.Y + 30)).Ok);
        game.OpenGates();
        var ticks = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++ticks < 70000, "The day never ended.");
            if (ticks % 15 == 0)
            {
                // The game looks four times a second; at normal speed that is every few game seconds.
                script.Poll();
                foreach (var (id, level) in script.Levels)
                {
                    loudest[id] = Math.Max(loudest[id], level);
                }
            }
        }

        script.Poll();
        game.PayScouts();
        game.HandBack();
        script.Poll();
        return (game, heard, loudest);
    }

    private static readonly Lazy<(FestivalGame Game, List<string> Heard, Dictionary<string, double> Loudest)> Fine = new(() => Day(null));
    private static readonly Lazy<(FestivalGame Game, List<string> Heard, Dictionary<string, double> Loudest)> Wet = new(() => Day(DayWeather.Of(WeatherKind.Rain, 3)));

    private static string Root()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "game", "Audio", "sounds.json")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return folder.FullName;
    }

    [Fact]
    public void The_list_of_sounds_and_the_names_the_game_uses_are_the_same()
    {
        using var list = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root(), "game", "Audio", "sounds.json")));
        var listed = list.RootElement.GetProperty("sounds").EnumerateArray().ToList();
        var names = typeof(Sounds).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToList();

        Assert.Equal(names.Order(), listed.Select(s => s.GetProperty("id").GetString()!).Order());
        Assert.Equal(Sounds.Loops.Order(), listed.Where(s => s.GetProperty("loop").GetBoolean()).Select(s => s.GetProperty("id").GetString()!).Order());
        Assert.All(listed, s =>
        {
            Assert.InRange(s.GetProperty("seconds").GetDouble(), 0.5, 22);
            Assert.InRange(s.GetProperty("volume").GetDouble(), 0.1, 1);
            Assert.True(s.GetProperty("prompt").GetString()!.Length > 30);
            Assert.Contains(s.GetProperty("bus").GetString(), new[] { "Ambience", "Crowd", "Effects", "Interface" });
        });
    }

    [Fact]
    public void The_murmur_of_the_crowd_grows_with_the_crowd()
    {
        Assert.Equal((0, 0, 0), SoundScript.Murmur(0));
        Assert.Equal((1, 0, 0), SoundScript.Murmur(120));
        Assert.Equal((0, 1, 0), SoundScript.Murmur(500));
        Assert.Equal((0, 0, 1), SoundScript.Murmur(5000));
        var (small, medium, _) = SoundScript.Murmur(275);
        Assert.Equal(0.5, small, 3);
        Assert.Equal(0.5, medium, 3);
        Assert.Equal(Sounds.CheerSmall, SoundScript.CheerFor(30));
        Assert.Equal(Sounds.CheerMedium, SoundScript.CheerFor(200));
        Assert.Equal(Sounds.CheerBig, SoundScript.CheerFor(2000));
    }

    [Fact]
    public void A_day_is_heard_from_the_stamp_on_the_plan_to_the_results()
    {
        var (game, heard, loudest) = Fine.Value;

        Assert.Equal(Sounds.RubberStamp, heard[0]);
        Assert.Equal(Sounds.GatesOpen, heard[1]);
        Assert.True(heard.IndexOf(Sounds.DayEnd) > heard.IndexOf(Sounds.GatesOpen));
        Assert.Contains(heard[^1], new[] { Sounds.ResultsGood, Sounds.ResultsBad });
        Assert.Equal(Phase.Results, game.Phase);

        // Tickets at the gate, and each kind of stall makes its own sound.
        Assert.InRange(heard.Count(h => h == Sounds.GateTicket), 20, 200);
        Assert.DoesNotContain(Sounds.GateTurnstile, heard);
        foreach (var sound in new[] { Sounds.Fry, Sounds.PourDrink, Sounds.ToiletDoor, Sounds.WaterTap, Sounds.TeaCup, Sounds.Till })
        {
            Assert.Contains(sound, heard);
        }

        // Water is free, so the till rings less often than people are served.
        Assert.True(heard.Count(h => h == Sounds.Till) < heard.Count(h => h is Sounds.Fry or Sounds.PourDrink or Sounds.ToiletDoor or Sounds.WaterTap or Sounds.TeaCup));

        // The field by day and by night, and a small crowd; nobody heard rain.
        Assert.Equal(1, loudest[Sounds.Day], 2);
        Assert.True(loudest[Sounds.Night] > 0.5);
        Assert.Equal(1, loudest[Sounds.CrowdSmall], 2);
        Assert.Equal(0, loudest[Sounds.CrowdLarge]);
        Assert.Equal(0, loudest[Sounds.RainLight] + loudest[Sounds.RainHeavy]);
        Assert.DoesNotContain(Sounds.RainStart, heard);
    }

    [Fact]
    public void Every_act_is_cheered_on_and_clapped_and_cheered_off()
    {
        var (game, heard, _) = Fine.Value;
        var cheers = heard.Count(h => h is Sounds.CheerSmall or Sounds.CheerMedium or Sounds.CheerBig or Sounds.SetEndOvation);
        var claps = heard.Count(h => h is Sounds.ApplauseSmall or Sounds.ApplauseBig);

        // A cheer as each act comes on, and clapping and cheering together, louder, as each goes off.
        var sets = game.Show.Slots.Count;
        Assert.InRange(cheers, (2 * sets) - 2, 2 * sets);
        Assert.InRange(claps, sets - 2, sets);
        Assert.Contains(Sounds.ApplauseBig, heard);
        Assert.Contains(Sounds.SetEndOvation, heard);
        Assert.Equal(Sounds.SongEndSmall, Sounds.SongEndFor(150));
        Assert.Equal(Sounds.SongEndBig, Sounds.SongEndFor(3000));
    }

    [Fact]
    public void Rain_is_heard_when_it_rains()
    {
        var (_, heard, loudest) = Wet.Value;

        Assert.Contains(Sounds.RainStart, heard);
        Assert.True(loudest[Sounds.RainLight] + loudest[Sounds.RainHeavy] > 0.5);
        Assert.Contains(Sounds.MudSquelch, heard);
    }

    [Fact]
    public void Trouble_is_called_on_the_radio_and_so_is_the_all_clear()
    {
        var (game, heard, _) = Fine.Value;
        var calls = heard.Count(h => h is Sounds.FirstAid or Sounds.Trouble);

        Assert.True(calls > 0, "Nothing went wrong all day.");
        Assert.InRange(calls, 1, game.Treated + game.Untreated + game.TroubleSorted + game.TroubleIgnored);
        Assert.Contains(Sounds.AllClear, heard);
    }

    [Fact]
    public void A_stopped_act_is_heard_stopping_and_starting_again()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var f = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        game.OpenGates();
        while (game.Show.Playing(game.Now) is null)
        {
            game.Clock.Step();
        }

        using var script = new SoundScript(game);
        var heard = new List<string>();
        script.Cue += heard.Add;
        Assert.True(game.PauseAct());
        script.Poll();
        Assert.Equal(new[] { Sounds.PaStop, Sounds.Groan }, heard);

        while (game.ActPaused)
        {
            game.Clock.Step();
        }

        script.Poll();
        Assert.Equal(Sounds.PaStart, heard[^1]);
    }
}
