using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>
/// The crowd's side of the dev panel: more people turning up at once, everyone feeling or needing
/// something at the press of a button, and a crowd packed into one spot to try out a crush.
/// None of it happens in a normal game.
/// </summary>
public sealed partial class Crowd
{
    /// <summary>
    /// Brings more people to the festival, arriving over the next few minutes on foot along the lane.
    /// They come in through the gates like everyone else. Returns how many are on their way.
    /// </summary>
    public int AddPeople(int people, DateTime now)
    {
        if (people <= 0)
        {
            return 0;
        }

        var random = GameRandom.FromSeed(Seed, 90000 + _parties.Count);
        var personId = _parties.Count == 0 ? 0 : _parties.Max(p => p.Members.Count == 0 ? 0 : p.Members.Max(m => m.Id)) + 1;
        var lastIn = _day + EveryoneOut - TimeSpan.FromMinutes(30);
        var added = 0;
        while (added < people)
        {
            var roll = random.NextDouble();
            var size = Math.Min(people - added, roll < 0.25 ? 1 : roll < 0.65 ? 2 : roll < 0.85 ? 3 : 4);
            var members = new List<PartyMember>();
            for (var i = 0; i < size; i++)
            {
                var lookSeed = unchecked((Seed * 31) + personId);
                var look = i == 0 ? LookGenerator.FestivalGoer(lookSeed, AgeGroup.Adult, DressedForRain) : LookGenerator.FestivalGoer(lookSeed, DressedForRain);
                var angle = (Math.Tau * i / size) + random.Between(-0.3, 0.3);
                var radius = size == 1 ? 0 : random.Between(0.34, 0.5);
                members.Add(new PartyMember(personId++, look, Math.Cos(angle) * radius, Math.Sin(angle) * radius));
            }

            var arrives = now + TimeSpan.FromSeconds(random.Between(5, 300));
            var family = members.Any(m => m.Look.Age == AgeGroup.Child);
            var leaves = _day + TimeSpan.FromHours(family ? random.Between(19, 21) : random.Between(21, 23));
            if (leaves < now + TimeSpan.FromHours(2))
            {
                leaves = now + TimeSpan.FromHours(2);
            }

            if (leaves > lastIn + TimeSpan.FromMinutes(30))
            {
                leaves = lastIn + TimeSpan.FromMinutes(30);
            }

            var speed = WalkMetresPerTick * (family ? random.Between(0.72, 0.85) : random.Between(0.88, 1.12));
            var needs = GameRandom.FromSeed(Seed, 95000 + _parties.Count);
            _parties.Add(new Party(_parties.Count, members, arrives, leaves, random.NextDouble() < 0.6, speed, GameRandom.FromSeed(Seed, 96000 + _parties.Count))
            {
                Planned = arrives,
                Hunger = needs.Between(0.1, 0.5),
                Thirst = needs.Between(0.1, 0.4),
                Toilet = needs.Between(0, 0.3),
                Appetite = needs.Between(0.8, 1.25),
                Stretch = 1 + (1.4 * needs.NextDouble()),
            });
            Expected += size;
            added += size;
        }

        return added;
    }

    /// <summary>
    /// Sends everyone home now, and stops anyone not here yet from coming. Returns how many were here.
    /// </summary>
    public int SendAllHome(DateTime now)
    {
        var here = OnSite;
        SendEveryoneHome(now, 0);
        foreach (var party in _parties)
        {
            if (party.State == PartyState.Expected)
            {
                // Never turns up: the gates have shut for the day before they would arrive.
                party.Arrives = _day + EveryoneOut + TimeSpan.FromHours(1);
            }
        }

        return here;
    }

    /// <summary>The parties at the festival just now who can feel something: here, and not on their way home.</summary>
    private IEnumerable<Party> Here => _parties.Where(p => p.IsOnSite && p.State != PartyState.Leaving);

    /// <summary>Sets how everyone here feels, from 0 to 100. Returns how many people it changed.</summary>
    public int SetMood(int happiness)
    {
        var mood = Math.Clamp(happiness, 0, 100) / 100.0;
        var people = 0;
        foreach (var party in Here)
        {
            party.Mood = mood;
            people += party.Members.Count;
        }

        return people;
    }

    /// <summary>Makes everyone here need something badly at once (food, drink or the toilet). Returns how many people.</summary>
    public int MakeEveryoneNeed(Need need)
    {
        var people = 0;
        foreach (var party in Here)
        {
            switch (need)
            {
                case Need.Food:
                    party.Hunger = Math.Max(party.Hunger, 0.9);
                    break;
                case Need.Drink:
                    party.Thirst = Math.Max(party.Thirst, 0.9);
                    break;
                default:
                    party.Toilet = Math.Max(party.Toilet, 0.9);
                    break;
            }

            people += party.Members.Count;
        }

        return people;
    }

    /// <summary>Gets every party of grown-ups here drunk. Returns how many parties.</summary>
    public int MakeEveryoneDrunk()
    {
        var parties = 0;
        foreach (var party in Here.Where(p => !p.HasChild))
        {
            party.Drink = Math.Max(party.Drink, DrunkAt + 1);
            parties++;
        }

        return parties;
    }

    /// <summary>Puts everyone's needs back to fine and sobers them up. Returns how many people.</summary>
    public int ResetNeeds()
    {
        var people = 0;
        foreach (var party in Here)
        {
            party.Hunger = 0.1;
            party.Thirst = 0.1;
            party.Toilet = 0.05;
            party.Drink = 0;
            people += party.Members.Count;
        }

        return people;
    }

    /// <summary>
    /// Packs people tight round a spot and holds them there, so it becomes a crush risk: the nearest
    /// people out and about, up to a number, squeezed in at more than five to a square metre. Returns
    /// how many people were packed in.
    /// </summary>
    public int PackInto(double x, double y, int people, DateTime until)
    {
        var chosen = new List<Party>();
        var count = 0;
        foreach (var party in _parties.Where(OutAndAbout).OrderBy(p => ((p.X - x) * (p.X - x)) + ((p.Y - y) * (p.Y - y))))
        {
            if (count >= people)
            {
                break;
            }

            chosen.Add(party);
            count += party.Members.Count;
        }

        if (count == 0)
        {
            return 0;
        }

        // Tight enough to be well over the crush line across the middle squares.
        var radius = Math.Max(1.2, Math.Sqrt(count / (5.5 * Math.PI)));
        var placed = 0;
        foreach (var party in chosen)
        {
            Halt(party, until);
            // Spread on a sunflower spiral, so the crowd is even right across the circle.
            var r = radius * Math.Sqrt((placed + 0.5) / count);
            var angle = placed * 2.39996;
            var (px, py) = (x + (r * Math.Cos(angle)), y + (r * Math.Sin(angle)));
            if (_map.IsWalkable(FieldMap.TileAt(px, py)))
            {
                (party.X, party.Y) = (px, py);
            }

            placed += party.Members.Count;
        }

        return count;
    }

    /// <summary>Lets everyone held still (packed in by the dev panel, or stopped to be sick) get on with their day. Returns how many parties.</summary>
    public int ReleaseHeld(DateTime now)
    {
        var parties = 0;
        foreach (var party in _parties.Where(p => p.State == PartyState.Standing && p.WaitUntil > now && !p.Fighting))
        {
            party.WaitUntil = now;
            party.RetryAt = now;
            party.SickUntil = default;
            parties++;
        }

        return parties;
    }
}
