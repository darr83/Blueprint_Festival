using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class BuskingTests
{
    private static FieldPlan Plan(params (string Field, FieldUse Use)[] choices) => new(choices.Select(c => new FieldChoice(c.Field, c.Use)));

    /// <summary>A planned village festival on its build day: a gate with ticket staff, a farm trailer at the top of the green, and stalls down the right.</summary>
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
        Assert.True(game.HireStaff(StaffKind.TicketStaff, new TilePoint(gap.X + 1, gap.Y + 1)).Ok);
        return game;
    }

    /// <summary>Near the way in, at the bottom of the green, where everybody walks past.</summary>
    private static TilePoint ByTheGate(FestivalGame game) => new(game.Field.Field.X + 10, game.Field.Field.Bottom - 8);

    [Fact]
    public void There_are_four_entertainers_to_hire_each_with_a_music_folder_of_their_own()
    {
        Assert.Equal(new[] { StaffKind.Guitarist, StaffKind.Pianist, StaffKind.Drummer, StaffKind.Dj }, Busking.Kinds);
        foreach (var kind in Busking.Kinds)
        {
            Assert.True(Busking.IsEntertainer(kind));
            Assert.True(Catalogue.Of(kind).Wage > 0);
            Assert.StartsWith("Entertainers/", Busking.MusicFolder(kind));
        }

        Assert.Equal(new[] { "Guitarist", "Pianist", "Drummer", "DJ" }, Busking.Kinds.Select(Busking.FolderName));
        Assert.Equal(4, Busking.Kinds.Select(Busking.RoleOf).Distinct().Count());
        Assert.False(Busking.IsEntertainer(StaffKind.Security));
        Assert.False(Busking.IsEntertainer(StaffKind.Cleaner));
    }

    [Fact]
    public void On_the_village_green_they_play_away_from_the_stage_and_apart_from_each_other()
    {
        var game = BuildDay();
        var f = game.Field.Field;
        var nearStage = game.CheckStaff(StaffKind.Guitarist, new TilePoint(f.X + 58, f.Y + 30));
        Assert.False(nearStage.Ok);
        Assert.Contains("stage", nearStage.Problem);

        var money = game.Money;
        Assert.True(game.HireStaff(StaffKind.Guitarist, ByTheGate(game)).Ok);
        Assert.Equal(money - Catalogue.Of(StaffKind.Guitarist).Wage, game.Money);
        var guitarist = game.Staff[^1];
        Assert.Equal(Busking.UsualHours, guitarist.Sets);

        // Too close to the guitarist.
        var close = game.CheckStaff(StaffKind.Drummer, new TilePoint(ByTheGate(game).X + 12, ByTheGate(game).Y));
        Assert.False(close.Ok);
        Assert.Contains("apart", close.Problem);

        // Their pitch is walked round, and nothing can be built on it.
        foreach (var tile in FestivalGame.PitchTiles(guitarist.Tile))
        {
            Assert.False(game.Field.IsWalkable(tile));
        }

        Assert.False(game.BuildStall(StallKind.Bin, guitarist.Tile.X - 1, guitarist.Tile.Y - 1, Facing.South).Ok);

        // Sent home, the grass is free again.
        game.DismissStaff(guitarist);
        Assert.True(game.Field.IsWalkable(guitarist.Tile));
        Assert.True(game.HireStaff(StaffKind.Drummer, new TilePoint(ByTheGate(game).X + 12, ByTheGate(game).Y)).Ok);
    }

    [Fact]
    public void With_a_food_field_they_play_in_the_food_or_camping_fields_and_not_with_the_music()
    {
        var game = FestivalGame.StartYear("Larkmoor Festival", new YearSetup(4, 1500, 40000, false, 80))
            .WithFields(Plan(("home", FieldUse.Music), ("ten", FieldUse.Food)))!;
        Assert.True(game.ConfirmPlan());
        var grass = game.Field.Fields.ToDictionary(f => f.Info.Id, f => f.Grass);
        var (music, food) = (grass["home"], grass["ten"]);

        var inMusic = game.CheckStaff(StaffKind.Pianist, new TilePoint(music.X + (music.Width / 2), music.Bottom - 10));
        Assert.False(inMusic.Ok);
        Assert.Contains("camping field or a food field", inMusic.Problem);
        Assert.True(game.HireStaff(StaffKind.Pianist, new TilePoint(food.X + (food.Width / 2), food.Y + (food.Height / 2))).Ok);
    }

    [Fact]
    public void The_player_picks_the_hours_they_play_and_each_set_lasts_45_minutes()
    {
        var game = BuildDay();
        Assert.True(game.HireStaff(StaffKind.Dj, ByTheGate(game)).Ok);
        var dj = game.Staff[^1];
        Assert.True(game.SetBuskHour(dj, 14, false));
        Assert.True(game.SetBuskHour(dj, 20, true));
        Assert.True(game.SetBuskHour(dj, 11, true));
        Assert.True(game.SetBuskHour(dj, 11, true));
        Assert.Equal(new[] { 11, 12, 16, 18, 20 }, dj.Sets);
        Assert.False(game.SetBuskHour(dj, Busking.FirstHour - 1, true));
        Assert.False(game.SetBuskHour(dj, Busking.LastHour + 1, true));
        Assert.False(game.SetBuskHour(game.Staff[0], 12, true));

        game.OpenGates();
        var seen = new HashSet<int>();
        while (game.Phase == Phase.Day && game.Now.Hour < 23)
        {
            game.Clock.Step();
            var set = game.BuskSetNow(dj);
            if (set >= 0)
            {
                seen.Add(set);
                Assert.Equal(set, game.Now.Hour);
                Assert.True(game.Now.Minute < Busking.SetMinutes);
            }
            else if (game.Now.Hour is 11 or 12 or 16 or 18 or 20)
            {
                Assert.True(game.Now.Minute >= Busking.SetMinutes);
            }

            if (game.Now.Hour == 13 && game.Now.Minute == 0 && game.Now.Second == 0)
            {
                // A set that has not started yet can still be changed on the day; one that is over cannot.
                Assert.True(game.SetBuskHour(dj, 15, true));
                Assert.False(game.SetBuskHour(dj, 12, false));
            }
        }

        Assert.Equal(new HashSet<int> { 11, 12, 15, 16, 18, 20 }, seen);
    }

    [Fact]
    public void People_walking_past_stop_to_listen_for_five_ten_or_fifteen_minutes_then_move_on()
    {
        var game = BuildDay();
        Assert.True(game.HireStaff(StaffKind.Guitarist, ByTheGate(game)).Ok);
        var guitarist = game.Staff[^1];
        foreach (var hour in Enumerable.Range(Busking.FirstHour, Busking.LastHour - Busking.FirstHour + 1))
        {
            game.SetBuskHour(guitarist, hour, true);
        }

        var (postX, postY) = (guitarist.X, guitarist.Y);
        var started = new Dictionary<int, DateTime>();
        var stays = new List<double>();
        var most = 0;
        game.OpenGates();
        var guard = 0;
        while (game.Phase == Phase.Day)
        {
            game.Clock.Step();
            Assert.True(++guard < 70000, "The day never ended.");
            if (game.Clock.TickCount % 5 != 0)
            {
                continue;
            }

            foreach (var party in game.Crowd.Parties)
            {
                if (party.ListeningToBusker && party.Busker == guitarist.Id)
                {
                    started.TryAdd(party.Id, game.Now);
                    Assert.True(Math.Abs(party.X - postX) <= Busking.FurthestWatch + 2 && Math.Abs(party.Y - postY) <= Busking.FurthestWatch + 2);
                }
                else if (started.Remove(party.Id, out var from))
                {
                    stays.Add((game.Now - from).TotalMinutes);
                }
            }

            most = Math.Max(most, game.ListeningTo(guitarist));
            if (!game.IsBusking(guitarist))
            {
                // Nobody stands listening long after a set ends.
                Assert.True(game.ListeningTo(guitarist) == 0 || game.Now.Minute <= Busking.SetMinutes + 2);
            }

            // The entertainer stays on their pitch all day.
            Assert.Equal((postX, postY), (guitarist.X, guitarist.Y));
        }

        Assert.True(game.BuskListeners > 10, $"Only {game.BuskListeners} people stopped to listen.");
        Assert.True(stays.Count > 5, $"Only {stays.Count} parties stood and listened.");
        Assert.All(stays, m => Assert.InRange(m, 0, 16));
        Assert.Contains(stays, m => m > 4);
        Assert.True(most <= Busking.CrowdWatching + 6, $"{most} people round one guitarist.");
    }

    [Fact]
    public void About_one_set_in_five_catches_on_and_draws_a_crowd()
    {
        var hits = 0;
        var tries = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            for (var hour = Busking.FirstHour; hour <= Busking.LastHour; hour++)
            {
                tries++;
                hits += Busking.CatchesOn(seed, seed % 4, hour) ? 1 : 0;
            }
        }

        Assert.InRange(hits / (double)tries, 0.12, 0.28);
        Assert.Equal(Busking.CatchesOn(7, 2, 15), Busking.CatchesOn(7, 2, 15));
        Assert.True(Busking.CrowdWatching > Busking.FewWatching * 3);
    }

    [Fact]
    public void Their_hours_are_kept_in_a_save()
    {
        var game = BuildDay();
        Assert.True(game.HireStaff(StaffKind.Pianist, ByTheGate(game)).Ok);
        var pianist = game.Staff[^1];
        game.SetBuskHour(pianist, 12, false);
        game.SetBuskHour(pianist, 21, true);

        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        var again = loaded.Staff.Single(s => s.Kind == StaffKind.Pianist);
        Assert.Equal(new[] { 14, 16, 18, 21 }, again.Sets);
        Assert.False(loaded.Field.IsWalkable(again.Tile));
    }

    [Fact]
    public void An_entertainer_is_only_heard_close_to()
    {
        var close = Sound.StageMix.Busker(0, 0, 12, 2, 2);
        var across = Sound.StageMix.Busker(0, 0, 12, 60, 0);
        var zoomedOut = Sound.StageMix.Busker(0, 0, 200, 0, 0);
        Assert.True(close > 0.5, $"{close}");
        Assert.Equal(0, across);
        Assert.True(zoomedOut < 0.05, $"{zoomedOut}");
    }
}
