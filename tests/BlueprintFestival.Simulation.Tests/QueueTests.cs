using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Tests;

/// <summary>Queues are single file: one person behind the next, nobody standing on anybody else.</summary>
public class QueueTests
{
    private static readonly DateTime Day = GameClock.DefaultStart.Date;

    private static void CloseTheGap(FestivalGame game, GateKind kind = GateKind.VillageGate)
    {
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        Assert.True(game.BuildFence(FenceKind.Rope, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y)).Ok);
        var width = Catalogue.Of(kind).Width;
        Assert.True(game.BuildGate(kind, gap.X + ((gap.Width - width) / 2), y, false).Ok);
    }

    /// <summary>The people standing still in queues, or being served, with where each one is.</summary>
    private static List<PersonOnSite> Waiting(FestivalGame game)
    {
        var ids = game.Crowd.Parties
            .Where(p => (p.State is PartyState.Queuing or PartyState.AtStall) && (p.InLine || p.Server >= 0))
            .SelectMany(p => p.Members.Select(m => m.Id))
            .ToHashSet();
        return game.Crowd.People.Where(p => ids.Contains(p.Id)).ToList();
    }

    private static double Nearest(IReadOnlyList<PersonOnSite> people)
    {
        var nearest = double.MaxValue;
        for (var i = 0; i < people.Count; i++)
        {
            for (var j = i + 1; j < people.Count; j++)
            {
                var dx = people[i].X - people[j].X;
                var dy = people[i].Y - people[j].Y;
                nearest = Math.Min(nearest, Math.Sqrt((dx * dx) + (dy * dy)));
            }
        }

        return nearest;
    }

    [Fact]
    public void A_queue_line_has_places_a_step_apart_and_part_places_in_between()
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var line = game.Crowd.GateLine(0, 0)!;
        Assert.True(line.Places.Count > 40);
        for (var i = 1; i < line.Places.Count; i++)
        {
            var (ax, ay) = line.Places[i - 1];
            var (bx, by) = line.Places[i];
            Assert.Equal(QueueLine.Spacing, Math.Sqrt(((ax - bx) * (ax - bx)) + ((ay - by) * (ay - by))), 3);
            Assert.True(game.Field.IsWalkable(FieldMap.TileAt(bx, by)));
            Assert.Equal(Side.Outside, game.Site.SideOf(FieldMap.TileAt(bx, by)));
        }

        var (hx, hy, _) = line.At(2.5);
        Assert.Equal((line.Places[2].X + line.Places[3].X) / 2, hx, 6);
        Assert.Equal((line.Places[2].Y + line.Places[3].Y) / 2, hy, 6);
    }

    [Fact]
    public void People_at_the_gate_queue_in_single_file_and_nobody_overlaps()
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var longest = 0;
        while (game.Now < Day.AddHours(11))
        {
            game.Clock.Step();
            var line = game.Crowd.GateLine(0, 0)!;
            var standing = game.Crowd.Parties.Where(p => p.State == PartyState.Queuing && p.InLine && !p.IsWalking).ToList();
            longest = Math.Max(longest, line.Standing);
            if (game.Now.Second != 0 || standing.Count < 2)
            {
                continue;
            }

            // Everyone standing in the line is on one of its places, a different one each.
            var ids = standing.SelectMany(p => p.Members.Skip(p.Checked).Select(m => m.Id)).ToHashSet();
            var people = game.Crowd.People.Where(p => ids.Contains(p.Id)).ToList();
            Assert.True(Nearest(people) > QueueLine.Spacing - 0.01, $"{game.Now:HH:mm:ss}: people {Nearest(people):0.00} m apart");
            Assert.All(people, p => Assert.Contains(line.Places, place => Math.Abs(place.X - p.X) < 0.001 && Math.Abs(place.Y - p.Y) < 0.001));
        }

        Assert.True(longest >= 8, $"the longest queue was {longest}");
        Assert.Equal(0, game.Crowd.WalkedInFree);
    }

    [Fact]
    public void Tickets_are_checked_one_person_at_a_time()
    {
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var seconds = Catalogue.Of(GateKind.VillageGate).SecondsPerPerson;
        Party? family = null;
        var shown = new List<int>();
        DateTime? began = null;
        while (game.Now < Day.AddHours(11))
        {
            game.Clock.Step();
            family ??= game.Crowd.Parties.FirstOrDefault(p => p.State == PartyState.Queuing && p.Members.Count >= 3 && p.ServiceLeft >= 0);
            if (family is null)
            {
                continue;
            }

            began ??= game.Now;
            if (family.State != PartyState.Queuing)
            {
                break;
            }

            // Exactly one person holds a ticket up at a time, and it is one of this family.
            var holding = game.Crowd.People.Where(p => p.Ticket).ToList();
            Assert.Single(holding);
            if (shown.Count == 0 || shown[^1] != holding[0].Id)
            {
                shown.Add(holding[0].Id);
            }
        }

        Assert.NotNull(family);
        Assert.Equal(family!.Members.Select(m => m.Id), shown);
        Assert.True(family.Ticketed);
        Assert.InRange((game.Now - began!.Value).TotalSeconds, seconds * family.Members.Count, (seconds * family.Members.Count) + 5);
    }

    [Fact]
    public void A_stall_has_one_queue_and_each_server_takes_the_next_party()
    {
        // One food van for the whole festival: both hatches are busy and a queue forms.
        var game = new FestivalGame("One Van", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 40, f.Y + 20, Facing.South).Ok);
        var allBusy = false;
        var longest = 0;
        var closest = double.MaxValue;
        while (game.Now < Day.AddHours(17))
        {
            game.Clock.Step();
            allBusy |= game.Crowd.BeingServed(0) == 2;
            longest = Math.Max(longest, game.Crowd.StallLine(0)!.Standing);
            if (game.Now.Second == 0)
            {
                var people = Waiting(game).Where(p => !p.Walking).ToList();
                if (people.Count > 1)
                {
                    closest = Math.Min(closest, Nearest(people));
                }
            }

            // Nobody is served twice over, and nobody being served is still in the queue.
            var served = game.Crowd.Parties.Where(p => p.State == PartyState.AtStall && p.Server >= 0).ToList();
            Assert.Equal(served.Count, served.Select(p => p.Server).Distinct().Count());
            Assert.All(served, p => Assert.False(p.InLine));
        }

        Assert.True(allBusy);
        Assert.True(longest >= 6, $"the longest queue was {longest}");
        Assert.True(closest > 0.45, $"people stood {closest:0.00} m apart");
        Assert.True(game.TotalIncome > 0);
    }

    [Fact]
    public void A_long_queue_folds_back_on_itself_and_keeps_clear_of_the_counter()
    {
        var game = new FestivalGame(seed: 3);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 40, f.Y + 20, Facing.South).Ok);
        var stall = game.Site.Stalls[0];
        var line = game.Crowd.StallLine(0)!;
        Assert.True(line.Places.Count >= 60);
        var front = stall.Rect.Bottom * FieldMap.TileMetres;
        Assert.All(line.Places, p => Assert.True(p.Y >= front + Crowd.StallQueueStart - 0.01));

        // It runs away from the stall, then comes back alongside itself.
        Assert.True(line.Places[5].Y > line.Places[0].Y);
        Assert.Contains(Enumerable.Range(1, line.Places.Count - 1), i => line.Places[i].Y < line.Places[i - 1].Y);

        // No two places are within an arm's length, in this queue or the next stall's.
        Assert.True(game.BuildStall(StallKind.Burgers, f.X + 52, f.Y + 20, Facing.South).Ok);
        var all = game.Crowd.StallLine(0)!.Places.Concat(game.Crowd.StallLine(1)!.Places).ToList();
        for (var i = 0; i < all.Count; i++)
        {
            for (var j = i + 1; j < all.Count; j++)
            {
                var d = Math.Sqrt(Math.Pow(all[i].X - all[j].X, 2) + Math.Pow(all[i].Y - all[j].Y, 2));
                Assert.True(d > 0.7, $"places {i} and {j} are {d:0.00} m apart");
            }
        }
    }

    [Fact]
    public void A_queue_turns_a_corner_when_something_is_in_the_way()
    {
        // The lane beyond the gate is only so wide: a long gate queue has to bend along it.
        var game = new FestivalGame(seed: 21);
        CloseTheGap(game);
        var line = game.Crowd.GateLine(0, 0)!;
        var (x0, y0) = line.Places[0];
        Assert.Contains(line.Places, p => Math.Abs(p.X - x0) > 1);
        Assert.Contains(line.Places, p => Math.Abs(p.Y - y0) > 1);
    }

    [Fact]
    public void A_queue_survives_saving_and_loading()
    {
        var game = new FestivalGame("Saved", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(StallKind.Fries, f.X + 40, f.Y + 20, Facing.South).Ok);
        while (game.Now < Day.AddHours(17) && (game.Crowd.StallLine(0)!.Standing < 6 || game.Crowd.BeingServed(0) == 0))
        {
            game.Clock.Step();
        }

        Assert.True(game.Crowd.StallLine(0)!.Standing >= 6);
        var before = game.Crowd.People.OrderBy(p => p.Id).ToList();
        var loaded = FestivalGame.Load(game.Save());
        var after = loaded.Crowd.People.OrderBy(p => p.Id).ToList();
        Assert.Equal(before.Count, after.Count);
        for (var i = 0; i < before.Count; i++)
        {
            Assert.Equal(before[i].X, after[i].X, 6);
            Assert.Equal(before[i].Y, after[i].Y, 6);
        }

        Assert.Equal(game.Crowd.StallLine(0)!.Standing, loaded.Crowd.StallLine(0)!.Standing);
        Assert.Equal(game.Crowd.BeingServed(0), loaded.Crowd.BeingServed(0));

        // And both carry on the same way.
        for (var i = 0; i < 1800; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.TotalIncome, loaded.TotalIncome);
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
    }

    /// <summary>A festival with one set of toilets and a bar, so a toilet queue is sure to form.</summary>
    private static FestivalGame WithToilets(StallKind kind = StallKind.Toilets)
    {
        var game = new FestivalGame("Toilets", seed: 8);
        var f = game.Field.Field;
        Assert.True(game.BuildStall(kind, f.X + 30, f.Y + 20, Facing.South).Ok);
        Assert.True(game.BuildStall(StallKind.Bar, f.X + 60, f.Y + 20, Facing.South).Ok);
        return game;
    }

    [Fact]
    public void At_the_toilets_each_person_goes_into_a_cubicle_alone_and_the_door_shuts_behind_them()
    {
        var game = WithToilets();
        var cabins = Catalogue.Of(StallKind.Toilets).Servers;
        var seen = new HashSet<CubicleLook>();
        var allEngaged = false;
        var longest = 0;
        var everyone = game.Crowd.Parties.SelectMany(p => p.Members).Count();
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            var looks = Enumerable.Range(0, cabins).Select(k => game.Crowd.Cubicle(0, k)).ToList();
            seen.UnionWith(looks);
            allEngaged |= looks.All(l => l == CubicleLook.Engaged);
            longest = Math.Max(longest, game.Crowd.StallLine(0)!.Standing);

            // Whoever is behind a shut door cannot be seen; everyone else on site can.
            var engaged = looks.Count(l => l == CubicleLook.Engaged);
            Assert.Equal(game.Crowd.OnSite - engaged, game.Crowd.People.Count());
            Assert.True(game.Crowd.BeingServed(0) <= cabins);

            // Nobody queuing is ever where someone else is standing.
            if (game.Now.Second == 0)
            {
                var people = Waiting(game).Where(p => !p.Walking).ToList();
                Assert.True(people.Count < 2 || Nearest(people) > 0.45, $"{game.Now:HH:mm}: {Nearest(people):0.00} m apart");
            }
        }

        Assert.Equal(3, seen.Count);
        Assert.True(allEngaged);
        Assert.True(longest >= 5, $"the longest queue was {longest}");
    }

    [Fact]
    public void A_visit_to_the_toilet_takes_the_door_twice_and_the_time_inside()
    {
        var game = WithToilets();
        var seconds = Catalogue.Of(StallKind.Toilets).SecondsPerPerson;
        Party? one = null;
        DateTime? opened = null;
        var looks = new List<CubicleLook>();
        var cubicle = -1;
        while (game.Now < Day.AddHours(12))
        {
            // Someone on their own, the moment they are given a cubicle.
            if (one is null)
            {
                var before = Enumerable.Range(0, 3).Select(k => game.Crowd.BeingServed(0)).First();
                game.Clock.Step();
                one = game.Crowd.Parties.FirstOrDefault(p => p.State == PartyState.AtStall && p.Stall == 0 && p.Members.Count == 1 && p.Checked == 1 && before == 0 && game.Crowd.BeingServed(0) == 1);
                continue;
            }

            game.Clock.Step();
            if (one.State != PartyState.AtStall)
            {
                break;
            }

            cubicle = cubicle >= 0 ? cubicle : Enumerable.Range(0, 3).FirstOrDefault(k => game.Crowd.Cubicle(0, k) != CubicleLook.Vacant, -1);
            if (cubicle < 0)
            {
                continue;
            }

            opened ??= game.Now;
            var look = game.Crowd.Cubicle(0, cubicle);
            if (looks.Count == 0 || looks[^1] != look)
            {
                looks.Add(look);
            }
        }

        Assert.NotNull(one);
        Assert.Equal(new[] { CubicleLook.DoorOpen, CubicleLook.Engaged, CubicleLook.DoorOpen }, looks.Take(3));
        Assert.Equal(0, one!.Toilet);
        Assert.Equal(PartyState.Standing, one.State);

        // From the door opening to it shutting again is no longer than a visit used to take.
        Assert.InRange((game.Now - opened!.Value).TotalSeconds, seconds - 15, seconds);
    }

    [Fact]
    public void A_family_uses_several_cubicles_at_once_and_waits_for_each_other()
    {
        var game = WithToilets(StallKind.ToiletBlock);
        Party? family = null;
        var together = 0;
        while (game.Now < Day.AddHours(16))
        {
            game.Clock.Step();
            family ??= game.Crowd.Parties.FirstOrDefault(p => p.State == PartyState.AtStall && p.Stall == 0 && p.Members.Count >= 3 && p.Checked >= 1);
            if (family is null)
            {
                continue;
            }

            if (family.State != PartyState.AtStall)
            {
                break;
            }

            var ids = family.Members.Select(m => m.Id).ToHashSet();
            together = Math.Max(together, family.Members.Count - game.Crowd.People.Count(p => ids.Contains(p.Id)));
        }

        Assert.NotNull(family);
        Assert.True(together >= 2, $"only {together} of the family were in cubicles at once");
        Assert.Equal(0, family!.Toilet);
        Assert.Equal(PartyState.Standing, family.State);
    }

    [Fact]
    public void Toilets_in_use_survive_saving_and_loading()
    {
        var game = WithToilets();
        while (game.Now < Day.AddHours(16)
            && !(game.Crowd.StallLine(0)!.Standing >= 3 && game.Crowd.Cubicle(0, 0) == CubicleLook.Engaged && Enumerable.Range(0, 3).Any(k => game.Crowd.Cubicle(0, k) == CubicleLook.DoorOpen)))
        {
            game.Clock.Step();
        }

        Assert.Equal(CubicleLook.Engaged, game.Crowd.Cubicle(0, 0));
        var loaded = FestivalGame.Load(game.Save());
        for (var k = 0; k < 3; k++)
        {
            Assert.Equal(game.Crowd.Cubicle(0, k), loaded.Crowd.Cubicle(0, k));
        }

        var before = game.Crowd.People.OrderBy(p => p.Id).ToList();
        var after = loaded.Crowd.People.OrderBy(p => p.Id).ToList();
        Assert.Equal(before.Select(p => (p.Id, Math.Round(p.X, 4), Math.Round(p.Y, 4))), after.Select(p => (p.Id, Math.Round(p.X, 4), Math.Round(p.Y, 4))));
        for (var i = 0; i < 1800; i++)
        {
            game.Clock.Step();
            loaded.Clock.Step();
        }

        Assert.Equal(game.Crowd.Content(Need.Toilet), loaded.Crowd.Content(Need.Toilet));
        Assert.Equal(game.Crowd.Happiness, loaded.Crowd.Happiness);
        Assert.Equal(game.Crowd.People.Count(), loaded.Crowd.People.Count());
    }
}
