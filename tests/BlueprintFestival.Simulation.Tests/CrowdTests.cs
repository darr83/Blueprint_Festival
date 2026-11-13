using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

public class CrowdTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static void RunUntil(FestivalGame game, double hour, Action? everyMinute = null)
    {
        var until = Day.AddHours(hour);
        while (game.Now < until)
        {
            game.Clock.Step();
            if (everyMinute is not null && game.Now.Second == 0)
            {
                everyMinute();
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(77)]
    [InlineData(123456)]
    public void A_village_festival_expects_between_100_and_200_people(int seed)
    {
        var crowd = new Crowd(FieldMap.Village(), seed, Day);
        Assert.InRange(crowd.Expected, 100, 200);
        Assert.Equal(crowd.Expected, crowd.Parties.Sum(p => p.Members.Count));
        Assert.All(crowd.Parties, p => Assert.InRange(p.Members.Count, 1, 4));
    }

    [Fact]
    public void The_same_seed_plans_the_same_day_and_a_different_seed_a_different_one()
    {
        var a = new Crowd(FieldMap.Village(), 5, Day);
        var b = new Crowd(FieldMap.Village(), 5, Day);
        var c = new Crowd(FieldMap.Village(), 6, Day);
        Assert.Equal(a.Parties.Select(p => (p.Arrives, p.Leaves, p.Members.Count)), b.Parties.Select(p => (p.Arrives, p.Leaves, p.Members.Count)));
        Assert.NotEqual(a.Parties.Select(p => p.Arrives), c.Parties.Select(p => p.Arrives));
    }

    [Fact]
    public void Everyone_has_their_own_number_and_no_child_comes_alone()
    {
        foreach (var seed in Enumerable.Range(1, 30))
        {
            var crowd = new Crowd(FieldMap.Village(), seed, Day);
            var ids = crowd.Parties.SelectMany(p => p.Members).Select(m => m.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(crowd.Parties, p => Assert.NotEqual(AgeGroup.Child, p.Members[0].Look.Age));
        }
    }

    [Fact]
    public void People_arrive_through_the_morning_and_families_go_home_first()
    {
        var crowd = new Crowd(FieldMap.Village(), 9, Day);
        Assert.All(crowd.Parties, p => Assert.InRange(p.Arrives.TimeOfDay, Crowd.GatesOpen, Crowd.LastArrival + TimeSpan.FromMinutes(2)));
        Assert.All(crowd.Parties, p => Assert.True(p.Leaves > p.Arrives.AddHours(3)));
        Assert.All(crowd.Parties.Where(p => p.HasChild), p => Assert.True(p.Leaves.TimeOfDay <= TimeSpan.FromHours(20.5)));
        Assert.All(crowd.Parties.Where(p => !p.HasChild), p => Assert.True(p.Leaves.TimeOfDay >= TimeSpan.FromHours(20.5)));
        Assert.All(crowd.Parties.Where(p => p.HasChild), p => Assert.True(p.Speed < Crowd.WalkMetresPerTick * 0.86));
    }

    [Fact]
    public void Nobody_is_there_before_the_gates_open_and_the_field_fills_by_the_afternoon()
    {
        var game = new FestivalGame("Crowd Test");
        Amenities.Add(game);
        Assert.Equal(0, game.Crowd.OnSite);

        RunUntil(game, 8.5);
        Assert.InRange(game.Crowd.OnSite, 1, game.Crowd.Expected - 1);

        RunUntil(game, 14);
        Assert.Equal(game.Crowd.Expected, game.Crowd.OnSite);
        Assert.Equal(game.Crowd.Expected, game.Crowd.InField);
        Assert.Equal(game.Crowd.Expected, game.Crowd.People.Count());
    }

    [Fact]
    public void All_day_long_everyone_stays_on_ground_they_can_walk_on_and_moves_at_a_walk()
    {
        var game = new FestivalGame("Walkable Test");
        var last = new Dictionary<int, (double X, double Y)>();
        var sawWalking = false;
        var sawStanding = false;
        RunUntil(game, 23.9, () =>
        {
            foreach (var party in game.Crowd.Parties.Where(p => p.IsOnSite))
            {
                Assert.True(game.Field.IsWalkable(FieldMap.TileAt(party.X, party.Y)), $"Party {party.Id} is off the path at {game.Now:HH:mm}.");
                sawWalking |= party.State == PartyState.Strolling;
                sawStanding |= party.State == PartyState.Standing;
                if (last.TryGetValue(party.Id, out var before))
                {
                    var moved = Math.Sqrt(Math.Pow(party.X - before.X, 2) + Math.Pow(party.Y - before.Y, 2));
                    Assert.True(moved <= (party.Speed * 60) + 0.001, "Nobody runs.");
                }

                last[party.Id] = (party.X, party.Y);
            }
        });

        Assert.True(sawWalking);
        Assert.True(sawStanding);
    }

    [Fact]
    public void Everyone_has_gone_home_by_midnight()
    {
        var game = new FestivalGame("Home Time");
        Amenities.Add(game);
        RunUntil(game, 19);
        var evening = game.Crowd.OnSite;
        RunUntil(game, 21.5);
        Assert.True(game.Crowd.OnSite < evening, "Families should have started to leave.");
        Assert.All(game.Crowd.Parties.Where(p => p.HasChild), p => Assert.Equal(PartyState.Gone, p.State));

        RunUntil(game, 24);
        Assert.Equal(0, game.Crowd.OnSite);
        Assert.Equal(game.Crowd.Expected, game.Crowd.Arrived);
        Assert.All(game.Crowd.Parties, p => Assert.Equal(PartyState.Gone, p.State));
    }

    [Fact]
    public void A_party_stays_together()
    {
        var game = new FestivalGame("Together");
        RunUntil(game, 12);
        var people = game.Crowd.People.ToDictionary(p => p.Id);
        foreach (var party in game.Crowd.Parties.Where(p => p.IsOnSite))
        {
            foreach (var member in party.Members)
            {
                var person = people[member.Id];
                Assert.InRange(Math.Abs(person.X - party.X), 0, 0.6);
                Assert.InRange(Math.Abs(person.Y - party.Y), 0, 0.6);
            }
        }
    }

    [Fact]
    public void A_game_saved_at_midday_carries_on_exactly_as_it_would_have()
    {
        var game = new FestivalGame("Save The Crowd");
        RunUntil(game, 11.25);
        var loaded = FestivalGame.Load(SaveData.FromJson(game.Save().ToJson()));

        Assert.Equal(game.Seed, loaded.Seed);
        Assert.Equal(game.Crowd.Expected, loaded.Crowd.Expected);
        Assert.Equal(game.Crowd.OnSite, loaded.Crowd.OnSite);

        RunUntil(game, 15);
        RunUntil(loaded, 15);
        var a = game.Crowd.People.Select(p => (p.Id, X: Math.Round(p.X, 6), Y: Math.Round(p.Y, 6), p.Walking)).ToList();
        var b = loaded.Crowd.People.Select(p => (p.Id, X: Math.Round(p.X, 6), Y: Math.Round(p.Y, 6), p.Walking)).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void An_old_save_without_people_still_loads()
    {
        const string old = """{ "Version": 1, "Name": "Old Fayre", "Clock": { "Now": "2027-07-17T09:30:00", "TickCount": 5400, "Speed": "Normal" } }""";
        var game = FestivalGame.Load(SaveData.FromJson(old));
        Assert.Equal("Old Fayre", game.Name);
        Assert.Equal(new DateTime(2027, 7, 17, 9, 30, 0), game.Now);
        Assert.InRange(game.Crowd.Expected, 100, 200);
    }

    [Fact]
    public void The_random_numbers_pick_up_where_they_left_off()
    {
        var a = GameRandom.FromSeed(12, 3);
        a.NextDouble();
        var b = new GameRandom(a.State);
        Assert.Equal(a.NextDouble(), b.NextDouble());
        Assert.Equal(a.Next(100), b.Next(100));
        Assert.All(Enumerable.Range(0, 1000), _ => Assert.InRange(a.NextDouble(), 0, 0.9999999999));
        Assert.All(Enumerable.Range(0, 1000), _ => Assert.InRange(a.Between(2, 5), 2, 5));
    }
}
