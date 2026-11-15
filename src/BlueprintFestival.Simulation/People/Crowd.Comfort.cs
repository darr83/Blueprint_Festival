using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>Sitting down, getting under cover and washing hands: what people do with the comforts the player puts out.</summary>
public sealed partial class Crowd
{
    /// <summary>How much a sit down lifts the mood, for each hour of it.</summary>
    public const double SitPerHour = 0.03;

    /// <summary>How far people will walk for a seat, in metres.</summary>
    public const double SeatReach = 30;

    /// <summary>How far people will walk to get under cover, in metres.</summary>
    public const double ShelterReach = 45;

    /// <summary>The chance that a party with nothing to do, and a seat near, fancies a sit down.</summary>
    public const double RestChance = 0.25;

    /// <summary>The shortest time on their feet between one rest and the next, in minutes. It can be twice this.</summary>
    public const double RestGap = 40;

    /// <summary>The chance that a party with food or drink in its hands looks for a seat to have it on.</summary>
    public const double EatSittingChance = 0.8;

    /// <summary>The chance that a party coming out of the toilets washes its hands, if there is somewhere near.</summary>
    public const double WashChance = 0.85;

    private readonly Dictionary<(PlacedComfort Comfort, int Seat), Party> _seats = new();
    private List<TilePoint> _lawns = new();

    /// <summary>How easy the ground is to walk on at a place in metres: 1 is firm, less is slow going. Left unset, it is all firm.</summary>
    public Func<double, double, double>? Footing { get; set; }

    /// <summary>True once the ground is wet enough that nobody will sit on straw or on the grass.</summary>
    public bool Damp { get; set; }

    /// <summary>How many people have sat down on a seat or on laid grass or sand so far today.</summary>
    public int SatDown { get; internal set; }

    /// <summary>How many people have stopped to wash or gel their hands so far today.</summary>
    public int WashedHands { get; internal set; }

    /// <summary>How many people are sitting down right now.</summary>
    public int Seated => _parties.Where(p => p.State == PartyState.Sitting).Sum(p => p.Members.Count);

    /// <summary>Tells the crowd where artificial grass and sand have been laid: places to sit on the ground. One tile for each patch.</summary>
    public void SetLawns(IEnumerable<TilePoint> tiles) => _lawns = tiles.ToList();

    /// <summary>True if a party is out of the rain and the sun: under a parasol, a sail or the marquee roof.</summary>
    public bool Sheltered(Party party)
    {
        if (_site.Comforts.Count == 0)
        {
            return false;
        }

        if (party.State == PartyState.Sitting && party.Seat is { } seat)
        {
            return Comforts.Of(seat.Kind).Roofed;
        }

        return party.State is PartyState.Standing or PartyState.Sitting && _site.RoofOverPlace(party.X, party.Y);
    }

    private double? SeatHeight(Party party, int member) =>
        party.Seat is { } comfort && party.Seats is { } seats && member < seats.Length ? Comforts.Of(comfort.Kind).Seats[seats[member]].Height : 0;

    private bool SeatTaken(PlacedComfort comfort, int seat)
    {
        if (!_seats.TryGetValue((comfort, seat), out var holder))
        {
            return false;
        }

        if (holder.Seat == comfort && holder.State is PartyState.Sitting or PartyState.Strolling && holder.Seats?.Contains(seat) == true)
        {
            return true;
        }

        _seats.Remove((comfort, seat));
        return false;
    }

    /// <summary>Lets go of a seat, or of the walk to one or to a sink. The party's state is left alone.</summary>
    private void GiveUpSeat(Party party)
    {
        if (party.Seat is { } comfort && party.Seats is { } seats)
        {
            foreach (var seat in seats)
            {
                if (_seats.TryGetValue((comfort, seat), out var holder) && ReferenceEquals(holder, party))
                {
                    _seats.Remove((comfort, seat));
                }
            }
        }

        party.Seat = null;
        party.Seats = null;
        party.OnGround = false;
        party.Wash = null;
    }

    private bool InsideTheFestival(Party party) => _map.InFestival(FieldMap.TileAt(party.X, party.Y));

    private static double Away(Party party, PlacedComfort comfort)
    {
        var (x, y) = comfort.Middle;
        return Math.Sqrt(((x - party.X) * (x - party.X)) + ((y - party.Y) * (y - party.Y)));
    }

    /// <summary>
    /// Looks for somewhere near with a seat for every one of a party, and sets off for it. In the
    /// rain only a seat under a roof will do, and nobody sits on wet straw. True if they set off.
    /// </summary>
    private bool FindSeat(Party party, DateTime until, bool toEat, bool roofOnly = false)
    {
        if (!InsideTheFestival(party))
        {
            return false;
        }

        var people = party.Members.Count;
        var wet = Rain > 0.05;
        var hot = Heat >= 1.5;
        PlacedComfort? best = null;
        var bestScore = double.MaxValue;
        foreach (var comfort in _site.Comforts)
        {
            var info = Comforts.Of(comfort.Kind);
            if (info.Seats.Length < people || ((wet || roofOnly) && !info.Roofed) || (comfort.Kind == ComfortKind.StrawBale && (wet || Damp)))
            {
                continue;
            }

            var away = Away(party, comfort);
            if (away > SeatReach)
            {
                continue;
            }

            var score = away + (toEat && !info.Table ? 12 : 0) + (hot && !info.Roofed ? 10 : 0);
            if (score >= bestScore)
            {
                continue;
            }

            var free = 0;
            for (var i = 0; i < info.Seats.Length; i++)
            {
                free += SeatTaken(comfort, i) ? 0 : 1;
            }

            if (free >= people)
            {
                (best, bestScore) = (comfort, score);
            }
        }

        var from = FieldMap.TileAt(party.X, party.Y);
        if (best is not null)
        {
            // The seats nearest the first free one, so a party sits together.
            var all = Comforts.Of(best.Kind).Seats;
            var first = Enumerable.Range(0, all.Length).First(i => !SeatTaken(best, i));
            var mine = Enumerable.Range(0, all.Length)
                .Where(i => !SeatTaken(best, i))
                .OrderBy(i => ((all[i].X - all[first].X) * (all[i].X - all[first].X)) + ((all[i].Y - all[first].Y) * (all[i].Y - all[first].Y)))
                .Take(people)
                .ToArray();
            var (sx, sy, _) = best.Place(all[mine[0]]);
            var beside = FieldMap.TileAt(sx, sy);
            if (!_map.IsWalkable(beside))
            {
                beside = NearestWalkable(beside);
            }

            if (Route(party, from, beside) is { } path)
            {
                GiveUpSeat(party);
                foreach (var seat in mine)
                {
                    _seats[(best, seat)] = party;
                }

                party.Seat = best;
                party.Seats = mine;
                SetOff(party, beside, path, until);
                return true;
            }
        }

        if (wet || Damp || roofOnly || toEat || _lawns.Count == 0)
        {
            return false;
        }

        // No seat: the laid grass or the sand will do on a dry day.
        TilePoint? lawn = null;
        var nearest = SeatReach / FieldMap.TileMetres;
        foreach (var tile in _lawns)
        {
            var away = Math.Sqrt(((tile.X - from.X) * (tile.X - from.X)) + ((tile.Y - from.Y) * (tile.Y - from.Y)));
            if (away < nearest && _map.IsWalkable(tile))
            {
                (lawn, nearest) = (tile, away);
            }
        }

        if (lawn is { } patch)
        {
            // Not all in the same spot of it.
            var spot = new TilePoint(patch.X + (party.Id % 3) - 1, patch.Y + (party.Id / 3 % 3) - 1);
            if (!_map.IsWalkable(spot))
            {
                spot = patch;
            }

            if (Route(party, from, spot) is { } path)
            {
                GiveUpSeat(party);
                party.OnGround = true;
                SetOff(party, spot, path, until);
                return true;
            }
        }

        return false;
    }

    private static void SetOff(Party party, TilePoint to, List<TilePoint> path, DateTime until)
    {
        party.SitUntil = until;
        party.Watch = -1;
        party.Target = to;
        party.Path = path;
        party.PathIndex = 0;
        party.State = PartyState.Strolling;
    }

    /// <summary>A party that has just been handed food or drink looks for a seat to have it on, a table for choice.</summary>
    private void SitToEat(Party party, DateTime now)
    {
        if (_site.Comforts.Count == 0 || Roll(party.Id, 61, (int)now.TimeOfDay.TotalMinutes) >= EatSittingChance)
        {
            return;
        }

        FindSeat(party, party.HoldUntil + TimeSpan.FromMinutes(1 + (party.Id % 4)), toEat: true);
    }

    /// <summary>
    /// A party with nothing to do thinks about a sit down. In the rain those without a poncho make
    /// for a roof, and in hot sun people look for shade. True if they set off, or are staying put under cover.
    /// </summary>
    private bool Rest(Party party, DateTime now)
    {
        if ((_site.Comforts.Count == 0 && _lawns.Count == 0) || !InsideTheFestival(party))
        {
            return false;
        }

        var moment = (int)(now.TimeOfDay.TotalMinutes / 3);
        var roll = Roll(party.Id, 63, moment);
        var until = now + TimeSpan.FromMinutes(5 + (9 * Roll(party.Id, 65, moment)));
        var raining = Rain > 0.05;
        var wet = raining && party.Members.Any(m => m.Look.Poncho is null);
        var hot = Heat >= 1.5;
        if (raining && !wet)
        {
            // In ponchos: some still fancy a dry seat, and nobody sits out in it.
            return now >= party.RestAfter && roll < 0.3 && FindSeat(party, until, toEat: false, roofOnly: true);
        }

        if (wet || hot)
        {
            if (Sheltered(party))
            {
                // Under cover already, and staying there.
                party.WaitUntil = now + TimeSpan.FromMinutes(4);
                return true;
            }

            if (roll < (wet ? 0.75 : 0.4))
            {
                return FindSeat(party, until, toEat: false, roofOnly: true) || TakeShelter(party);
            }

            return !wet && now >= party.RestAfter && roll < 0.55 && FindSeat(party, until, toEat: false);
        }

        if (now < party.RestAfter)
        {
            return false;
        }

        var chance = RestChance + (party.Members.Any(m => m.Look.Age == AgeGroup.Older) ? 0.15 : 0);
        return roll < chance && FindSeat(party, until, toEat: false);
    }

    /// <summary>Walks a party to stand under the nearest sail or marquee roof. True if they set off.</summary>
    private bool TakeShelter(Party party)
    {
        PlacedComfort? best = null;
        var nearest = ShelterReach;
        foreach (var comfort in _site.Comforts)
        {
            if (comfort.Kind is ComfortKind.ShadeSail or ComfortKind.SeatingMarquee && Away(party, comfort) is var away && away < nearest)
            {
                (best, nearest) = (comfort, away);
            }
        }

        if (best is null)
        {
            return false;
        }

        // A spot of their own under the roof, a little in from its edge.
        var r = best.Rect;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var spot = new TilePoint(
                r.X + 1 + (int)(Roll(party.Id, 67 + attempt, r.X) * Math.Max(1, r.Width - 2)),
                r.Y + 1 + (int)(Roll(party.Id, 73 + attempt, r.Y) * Math.Max(1, r.Height - 2)));
            if (_map.IsWalkable(spot))
            {
                Send(party, spot, PartyState.Strolling);
                return party.State == PartyState.Strolling;
            }
        }

        return false;
    }

    /// <summary>A party coming out of the toilets stops at a sink or a gel post, if there is one within a few steps.</summary>
    private void GoAndWash(Party party, DateTime now)
    {
        if (_site.Comforts.Count == 0 || Roll(party.Id, 69, (int)now.TimeOfDay.TotalMinutes) >= WashChance)
        {
            return;
        }

        PlacedComfort? best = null;
        var nearest = Comforts.WashReach + 4;
        foreach (var comfort in _site.Comforts)
        {
            // A sink is worth a few more steps than a squirt of gel.
            if (Comforts.Of(comfort.Kind).Washes && Away(party, comfort) + (comfort.Kind == ComfortKind.HandGel ? 4 : 0) is var away && away < nearest)
            {
                (best, nearest) = (comfort, away);
            }
        }

        if (best is null)
        {
            return;
        }

        var info = Comforts.Of(best.Kind);
        var (fx, fy) = best.Out(((party.Id % 2) - 0.5) * (best.Kind == ComfortKind.HandWash ? 0.85 : 0), (info.Depth * FieldMap.TileMetres / 2) + 0.4);
        var front = FieldMap.TileAt(fx, fy);
        if (!_map.IsWalkable(front))
        {
            front = NearestWalkable(front);
        }

        if (Route(party, FieldMap.TileAt(party.X, party.Y), front) is { } path)
        {
            GiveUpSeat(party);
            party.Wash = best;
            SetOff(party, front, path, default);
        }
    }

    /// <summary>A strolling party has got where it was going. True if that was a seat, a patch of grass to sit on, or a sink.</summary>
    private bool Settle(Party party, DateTime now)
    {
        if (party.Wash is { } wash)
        {
            party.Wash = null;
            var (wx, wy) = wash.Middle;
            party.Heading = Math.Atan2(wx - party.X, wy - party.Y);
            party.State = PartyState.Standing;
            party.RetryAt = default;
            party.WaitUntil = now + TimeSpan.FromSeconds(20 + (party.Members.Count * 8));
            WashedHands += party.Members.Count;
            return true;
        }

        if (party.Seat is null && !party.OnGround)
        {
            return false;
        }

        // It came on to rain on the way over: nobody sits down in it.
        if ((Rain > 0.05 || (Damp && (party.OnGround || party.Seat?.Kind == ComfortKind.StrawBale))) && !(party.Seat is { } roofed && Comforts.Of(roofed.Kind).Roofed))
        {
            GiveUpSeat(party);
            return false;
        }

        party.State = PartyState.Sitting;
        party.WaitUntil = party.SitUntil > now ? party.SitUntil : now + TimeSpan.FromMinutes(5);
        if (party.OnGround)
        {
            party.Heading += Roll(party.Id, 71, (int)now.TimeOfDay.TotalMinutes) * Math.PI * 2;
        }

        SatDown += party.Members.Count;
        return true;
    }

    /// <summary>One tick of sitting down: up again when the rest is over, when something is pressing, or when it comes on to rain.</summary>
    private void Sit(Party party, DateTime now, bool closing)
    {
        if (now >= party.Leaves || closing)
        {
            Send(party, Home(party), PartyState.Leaving);
            if (party.State == PartyState.Standing)
            {
                party.RetryAt = now + TimeSpan.FromMinutes(2);
            }

            return;
        }

        var rainedOff = (Rain > 0.05 || (party.OnGround && Damp)) && !Sheltered(party);
        if (now < party.WaitUntil && !rainedOff && party.Wants is null)
        {
            return;
        }

        GiveUpSeat(party);
        party.RestAfter = now + TimeSpan.FromMinutes(RestGap + (RestGap * Roll(party.Id, 77, (int)now.TimeOfDay.TotalMinutes)));
        party.State = PartyState.Standing;
        party.RetryAt = default;
        party.WaitUntil = now + TimeSpan.FromSeconds(20 + (40 * Roll(party.Id, 75, (int)now.TimeOfDay.TotalMinutes)));
    }

    private void RestoreSeat(Party party, PartySave save)
    {
        party.Seat = null;
        party.Seats = null;
        party.Wash = null;
        party.OnGround = false;
        party.SitUntil = save.SitUntil;
        party.RestAfter = save.RestAfter;
        if (save.State != PartyState.Sitting)
        {
            return;
        }

        if (save.OnGround)
        {
            party.OnGround = true;
            return;
        }

        if (save.Seat is { } corner && save.Seats is { } seats && _site.Comforts.FirstOrDefault(c => c.X == corner.X && c.Y == corner.Y) is { } comfort
            && seats.Count == party.Members.Count && seats.All(i => i >= 0 && i < Comforts.Of(comfort.Kind).Seats.Length && !SeatTaken(comfort, i)))
        {
            party.Seat = comfort;
            party.Seats = seats.ToArray();
            foreach (var seat in seats)
            {
                _seats[(comfort, seat)] = party;
            }

            return;
        }

        party.State = PartyState.Standing;
    }
}
