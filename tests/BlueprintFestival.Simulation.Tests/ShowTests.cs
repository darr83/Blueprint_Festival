using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class ShowTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static void RunUntil(FestivalGame game, double hour)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
        }
    }

    private static FestivalGame WithStage(StageKind kind, int seed = 21, Facing facing = Facing.South)
    {
        var game = new FestivalGame(seed: seed);
        var f = game.Field.Field;
        Assert.True(game.BuildStage(kind, f.X + 50, f.Y + 14, facing).Ok);
        return game;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(21)]
    [InlineData(999)]
    public void The_day_has_seven_sets_in_order_that_do_not_overlap_and_the_headliner_is_last(int seed)
    {
        var show = new FestivalGame(seed: seed).Show;
        Assert.Equal(7, show.Slots.Count);
        Assert.Equal(Day.AddHours(12), show.Slots[0].Start);
        for (var i = 1; i < show.Slots.Count; i++)
        {
            Assert.True(show.Slots[i].Start >= show.Slots[i - 1].End.AddMinutes(15), "There should be a changeover between sets.");
        }

        Assert.All(show.Slots.Take(6), s => Assert.False(s.Headliner));
        Assert.True(show.Slots[6].Headliner);
        Assert.Equal(Day.AddHours(22), show.Slots[6].End);
        Assert.True(show.Slots[6].Act.Draw > show.Slots.Take(6).Max(s => s.Act.Draw));
        Assert.Contains(show.Slots[6].Act.Kind, new[] { ActKind.Band, ActKind.Dj });
    }

    [Fact]
    public void Acts_are_the_same_for_the_same_festival_and_every_name_is_different()
    {
        var a = new FestivalGame(seed: 5).Show.Slots.Select(s => s.Act.Name).ToList();
        var b = new FestivalGame(seed: 5).Show.Slots.Select(s => s.Act.Name).ToList();
        var c = new FestivalGame(seed: 6).Show.Slots.Select(s => s.Act.Name).ToList();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        foreach (var seed in Enumerable.Range(1, 40))
        {
            var slots = new FestivalGame(seed: seed).Show.Slots;
            Assert.Equal(7, slots.Select(s => s.Act.Name).Distinct().Count());
            Assert.All(slots, s => Assert.InRange(s.Act.Draw, 0.3, 1.0));
            Assert.All(slots, s => Assert.InRange(s.Act.Members.Count, 1, 5));
            Assert.All(slots, s => Assert.False(string.IsNullOrWhiteSpace(s.Act.Name)));
        }
    }

    [Fact]
    public void With_no_stage_nothing_plays_and_nobody_watches()
    {
        var game = new FestivalGame(seed: 21);
        RunUntil(game, 12.3);
        Assert.Null(game.Show.Stage);
        Assert.Null(game.Show.Playing(game.Now));
        Assert.Empty(game.Show.OnStage(game.Now));
        Assert.Equal(0, game.Crowd.Watching);
        Assert.NotNull(game.Show.Next(game.Now));
    }

    [Fact]
    public void A_set_is_on_between_its_start_and_end_and_not_in_the_changeover()
    {
        var game = WithStage(StageKind.Bandstand);
        Assert.Null(game.Show.Playing(Day.AddHours(11.9)));
        Assert.Equal(0, game.Show.Playing(Day.AddHours(12))!.Index);
        Assert.Equal(0, game.Show.Playing(Day.AddHours(12.7))!.Index);
        Assert.Null(game.Show.Playing(Day.AddHours(12.8)));
        Assert.Equal(1, game.Show.Next(Day.AddHours(12.8))!.Index);
        Assert.Null(game.Show.Next(Day.AddHours(21)));
    }

    [Fact]
    public void A_small_stage_only_takes_the_first_few_of_a_big_band()
    {
        var band = new Act("Test Band", ActKind.Band, new[] { BandRole.Singer, BandRole.Guitarist, BandRole.Bassist, BandRole.Drummer, BandRole.Keys }, 0.5, 1);
        Assert.Equal(new[] { BandRole.Singer, BandRole.Guitarist }, band.On(Catalogue.Of(StageKind.FarmTrailer).Performers));
        Assert.Equal(4, band.On(Catalogue.Of(StageKind.Bandstand).Performers).Count);
        Assert.Equal(5, band.On(Catalogue.Of(StageKind.HiredStage).Performers).Count);

        var game = WithStage(StageKind.FarmTrailer);
        foreach (var slot in game.Show.Slots)
        {
            Assert.InRange(game.Show.OnStage(slot.Start).Count, 1, 2);
        }
    }

    [Theory]
    [InlineData(Facing.South, 0, 1)]
    [InlineData(Facing.North, 0, -1)]
    [InlineData(Facing.East, 1, 0)]
    [InlineData(Facing.West, -1, 0)]
    public void The_front_of_a_stage_is_the_middle_of_the_edge_it_faces(Facing facing, int dx, int dy)
    {
        var stage = new PlacedStage(StageKind.Bandstand, 100, 60, facing);
        var (x, y, fx, fy) = RunningOrder.Front(stage);
        Assert.Equal((dx, dy), (fx, fy));
        var r = stage.Rect;
        var midX = (r.X + (r.Width / 2.0)) * 0.5;
        var midY = (r.Y + (r.Height / 2.0)) * 0.5;
        Assert.Equal(midX + (dx * r.Width * 0.25), x, 6);
        Assert.Equal(midY + (dy * r.Height * 0.25), y, 6);
    }

    [Theory]
    [InlineData(Facing.South)]
    [InlineData(Facing.West)]
    public void When_an_act_is_on_people_gather_in_front_of_the_stage_facing_it(Facing facing)
    {
        var game = WithStage(StageKind.Bandstand, facing: facing);
        RunUntil(game, 12.5);
        var slot = game.Show.Playing(game.Now)!;
        var watching = game.Crowd.Parties.Where(p => p.State == PartyState.Watching).ToList();
        Assert.True(game.Crowd.Watching >= 15, $"Only {game.Crowd.Watching} watching.");
        Assert.True(game.Crowd.Watching < game.Crowd.OnSite, "Not everybody fancies every act.");

        var (fx, fy, dx, dy) = RunningOrder.Front(game.Show.Stage!);
        foreach (var party in watching)
        {
            Assert.Equal(slot.Index, party.Watch);
            // In front of the stage, not behind or on it.
            var ahead = ((party.X - fx) * dx) + ((party.Y - fy) * dy);
            Assert.InRange(ahead, 0.5, 30);
            // Looking at it, give or take.
            var toStage = Math.Atan2(fx - party.X, fy - party.Y);
            var off = Math.Abs(Math.IEEERemainder(party.Heading - toStage, Math.Tau));
            Assert.True(off < 0.3);
        }

        Assert.Contains(game.Crowd.People, p => p.Watching);
    }

    [Fact]
    public void When_the_set_ends_the_crowd_drifts_away_and_comes_back_for_the_next()
    {
        var game = WithStage(StageKind.Bandstand);
        RunUntil(game, 12.7);
        Assert.True(game.Crowd.Watching > 10);
        RunUntil(game, 12.78);
        Assert.Equal(0, game.Crowd.Watching);
        RunUntil(game, 13.6);
        Assert.True(game.Crowd.Watching > 10);
        Assert.All(game.Crowd.Parties.Where(p => p.State == PartyState.Watching), p => Assert.Equal(1, p.Watch));
    }

    [Fact]
    public void The_headliner_pulls_a_bigger_share_of_the_people_still_there_than_an_afternoon_act()
    {
        var game = WithStage(StageKind.HiredStage);
        RunUntil(game, 14.9);
        var afternoon = game.Crowd.Watching / (double)game.Crowd.OnSite;
        RunUntil(game, 21.2);
        var headline = game.Crowd.Watching / (double)Math.Max(1, game.Crowd.OnSite);
        Assert.True(game.Crowd.OnSite > 20);
        Assert.True(headline > afternoon, $"{headline:0.00} against {afternoon:0.00}");
        Assert.True(headline > 0.6);
    }

    [Fact]
    public void Taking_the_stage_down_mid_set_ends_the_show_and_everyone_still_gets_home()
    {
        var game = WithStage(StageKind.Bandstand);
        RunUntil(game, 12.5);
        Assert.True(game.Crowd.Watching > 0);
        var stage = game.Site.Stages[0].Rect;
        game.Remove(new TilePoint(stage.X + 1, stage.Y + 1));
        Assert.Equal(0, game.Crowd.Watching);
        Assert.Null(game.Show.Playing(game.Now));
        RunUntil(game, 23.9);
        Assert.Equal(0, game.Crowd.OnSite);
    }

    [Fact]
    public void A_save_in_the_middle_of_a_set_carries_on_just_the_same()
    {
        var game = WithStage(StageKind.Bandstand);
        RunUntil(game, 13.5);
        var copy = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));
        Assert.Equal(game.Crowd.Watching, copy.Crowd.Watching);
        Assert.Equal(game.Show.Slots.Select(s => s.Act), copy.Show.Slots.Select(s => s.Act), new ActComparer());
        RunUntil(game, 16.5);
        RunUntil(copy, 16.5);
        Assert.Equal(
            game.Crowd.Parties.Select(p => (p.State, p.Watch, Math.Round(p.X, 6), Math.Round(p.Y, 6))),
            copy.Crowd.Parties.Select(p => (p.State, p.Watch, Math.Round(p.X, 6), Math.Round(p.Y, 6))));
    }

    private sealed class ActComparer : IEqualityComparer<Act>
    {
        public bool Equals(Act? a, Act? b) => a!.Name == b!.Name && a.Kind == b.Kind && a.Draw.Equals(b.Draw) && a.Members.SequenceEqual(b.Members);

        public int GetHashCode(Act act) => act.Name.GetHashCode();
    }

    [Fact]
    public void An_act_always_plays_the_same_songs()
    {
        var first = new FestivalGame("One", null, 5).Show.Slots[0].Act;
        var again = new FestivalGame("Two", null, 5).Show.Slots[0].Act;
        Assert.Equal(first.Name, again.Name);
        Assert.Equal(first.SongSeed, again.SongSeed);
        Assert.Equal(first.Songs, again.Songs);
        Assert.Equal(6, first.Songs.Count);
        Assert.Equal(6, first.Songs.Select(s => s.Title).Distinct().Count());

        // The songs hang on the name alone, so the act has them wherever and whenever it turns up.
        Assert.Equal(first.Songs, ActGenerator.SongsFor(first.Name, first.Kind));
        Assert.NotEqual(first.Songs[0], ActGenerator.SongsFor(first.Name + "x", first.Kind)[0]);
    }

    [Fact]
    public void A_set_works_through_the_songs_in_order()
    {
        var game = new FestivalGame("Songs", null, 5);
        var grass = game.Field.Field;
        Assert.True(game.BuildStage(StageKind.Bandstand, grass.X + 40, grass.Y + 10, Facing.South).Ok);
        var slot = game.Show.Slots[0];
        Assert.Null(game.Show.SongNow(slot.Start.AddMinutes(-1)));
        Assert.Equal(slot.Act.Songs[0], game.Show.SongNow(slot.Start));
        Assert.Equal(slot.Act.Songs[5], game.Show.SongNow(slot.End.AddSeconds(-1)));
    }
}
