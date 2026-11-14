using System.Text.Json;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class AnnouncementTests
{
    /// <summary>The voices that read the crush announcements; the acts are introduced by the same ones.</summary>
    private static readonly string[] CrushVoices = { "JBFqnCBsd6RMkjVDRZzb", "Xb7hH8MSUJpSbSDYk0k2", "onwK4e9ZLuTAKqWW03F9" };

    private static string Folder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "game", "Audio", "Announcements", "announcements.json")))
        {
            folder = folder.Parent;
        }

        Assert.NotNull(folder);
        return Path.Combine(folder.FullName, "game", "Audio", "Announcements");
    }

    private static string KindWord(ActKind kind) => kind switch
    {
        ActKind.Solo => "solo",
        ActKind.Duo => "duo",
        ActKind.Dj => "DJ",
        _ => "band",
    };

    private static string GenreWord(Genre genre) => genre == Genre.HipHop ? "hip hop" : genre.ToString().ToLowerInvariant();

    [Fact]
    public void Every_artist_has_two_announcements_naming_them_their_kind_and_their_music()
    {
        var folder = Folder();
        using var list = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "announcements.json")));
        var lines = list.RootElement.GetProperty("announcements").EnumerateArray()
            .ToDictionary(l => l.GetProperty("id").GetString()!, l => l);

        Assert.Equal(Roster.Artists.Count * Announcements.Takes, lines.Count);
        foreach (var artist in Roster.Artists)
        {
            var voices = new List<string>();
            for (var take = 1; take <= Announcements.Takes; take++)
            {
                var id = Announcements.IdFor(artist.Name, take);
                Assert.True(lines.TryGetValue(id, out var line), $"{artist.Name} has no announcement {id}.");
                var said = line.GetProperty("prompt").GetString()!;
                Assert.Contains(artist.Name, said);
                Assert.Contains(KindWord(artist.Kind), said);
                Assert.Contains(GenreWord(artist.Genre), said);
                voices.Add(line.GetProperty("voice").GetString()!);
                Assert.True(File.Exists(Path.Combine(folder, id + ".ogg")), $"The file for {id} is missing.");
            }

            Assert.All(voices, v => Assert.Contains(v, CrushVoices));
            Assert.Equal(voices.Count, voices.Distinct().Count());
        }
    }

    [Fact]
    public void Names_are_written_plainly_in_file_names()
    {
        Assert.Equal("intro_the_velvet_otters_1", Announcements.IdFor("The Velvet Otters", 1));
        Assert.Equal("intro_dj_otter_2", Announcements.IdFor("DJ Otter", 2));
        Assert.Equal("salt_and_bramble", Announcements.Slug("Salt and Bramble"));
    }

    [Fact]
    public void Each_act_is_announced_a_few_minutes_before_it_goes_on()
    {
        var game = FestivalGame.NewFestival("Larkmoor Festival");
        using var script = new SoundScript(game);
        var heard = new List<(string Id, DateTime At)>();
        script.Cue += id => heard.Add((id, game.Now));

        game.ChooseHeadliner(0);
        Assert.True(game.ConfirmPlan());
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        var f = game.Field.Field;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        Assert.True(game.BuildGate(GateKind.VillageGate, gap.X, y, false).Ok);
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, y)).Ok);
        Assert.True(game.BuildStage(StageKind.FarmTrailer, f.X + 50, f.Y + 6, Facing.South).Ok);
        game.OpenGates();
        var ticks = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++ticks < 70000, "The day never ended.");
            if (ticks % 15 == 0)
            {
                script.Poll();
            }
        }

        var announced = heard.Where(h => h.Id.StartsWith("intro_", StringComparison.Ordinal)).ToList();
        var sets = game.Show.Slots.Where(s => Roster.Named(s.Act.Name) is not null).ToList();
        Assert.NotEmpty(sets);
        Assert.Equal(sets.Count, announced.Count);
        foreach (var set in sets)
        {
            var mine = announced.Where(a => a.Id.StartsWith(Announcements.IdFor(set.Act.Name, 1)[..^1], StringComparison.Ordinal)).ToList();
            Assert.Contains(mine, a => a.At < set.Start && set.Start - a.At <= TimeSpan.FromMinutes(Announcements.MainStageMinutes));
        }
    }
}
