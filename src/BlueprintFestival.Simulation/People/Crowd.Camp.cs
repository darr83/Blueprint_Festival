using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>
/// The camping field: who pitches where, and how campers feel about their pitch. Parties with
/// children look for family camping, some grown-ups for quiet camping, and a few campers need an
/// accessible pitch. Tents never go up on a fire lane. A camper's pitch cheers them up or lets
/// them down once, when they come through the gate and put their tent up.
/// </summary>
public sealed partial class Crowd
{
    /// <summary>The share of campers who need an accessible pitch.</summary>
    public const double NeedAccessShare = 0.05;

    /// <summary>The share of grown-up campers (no children) who would rather pitch somewhere quiet.</summary>
    public const double QuietShare = 0.3;

    /// <summary>What a camper gains from getting the kind of pitch they wanted.</summary>
    public const double WishMet = 0.03;

    /// <summary>What a camper who needs an accessible pitch loses without one.</summary>
    public const double NoAccess = 0.08;

    /// <summary>What a family loses pitched among everyone else when there is family camping, but it is full.</summary>
    public const double FamilyLeftOut = 0.015;

    /// <summary>What a camper loses with no toilets, and with no water, within reach of their tent.</summary>
    public const double NoToiletsNear = 0.03;

    public const double NoWaterNear = 0.02;

    /// <summary>What each extra way to camp gets from the guests who like it most: a better start than a pitch of your own.</summary>
    public const double ExtraWishMet = 0.04;

    private readonly double[] _kindShares = { 0, CampLayout.UsualShare(CampKind.ReadyPitched), CampLayout.UsualShare(CampKind.Campervan), CampLayout.UsualShare(CampKind.Glamping) };

    /// <summary>Sets the share of campers who buy each dearer way to camp, from its price.</summary>
    public void SetKindShare(CampKind kind, double share) => _kindShares[(int)kind] = Math.Clamp(share, 0, 0.5);

    /// <summary>The share of campers who buy a way to camp at today's prices.</summary>
    public double KindShare(CampKind kind) => _kindShares[(int)kind];

    /// <summary>
    /// The way to camp a party would buy at today's prices: most bring their own tent, a few pay
    /// for glamping, a campervan pitch or a tent ready pitched. The same parties every time.
    /// </summary>
    public CampKind BoughtKind(Party party)
    {
        var roll = (uint)((party.Id * 2654435769u) ^ 0x5bd1e995u) % 10000 / 10000.0;
        var upTo = 0.0;
        foreach (var kind in new[] { CampKind.Glamping, CampKind.Campervan, CampKind.ReadyPitched })
        {
            upTo += _kindShares[(int)kind];
            if (roll < upTo)
            {
                return kind;
            }
        }

        return CampKind.OwnTent;
    }

    /// <summary>The way a party is camping: by the ground their pitch is on.</summary>
    public CampKind KindOf(Party party) => party.Pitch is { } pitch ? CampLayout.KindOn(_site.CampZoneAt(pitch)) : CampKind.OwnTent;

    /// <summary>True if this party will bring a tent at today's pitch price. The same parties camp every time the day is planned.</summary>
    public bool WouldCamp(Party party) => (uint)(party.Id * 2654435761u) % 100 < CamperShare * 100;

    /// <summary>The kind of camping a party looks for: accessible, family, quiet or general.</summary>
    public static CampZone WishOf(Party party) =>
        party.NeedsAccess ? CampZone.Accessible
        : party.HasChild ? CampZone.Family
        : (uint)(party.Id * 3266489917u) % 100 < QuietShare * 100 ? CampZone.Quiet
        : CampZone.General;

    /// <summary>
    /// Every pitch a tent could go on now, in the order they fill (nearest the gateway first), with
    /// how its ground is marked. Nothing is pitched where something has been built or on a fire
    /// lane, and tents in accessible camping are spaced further apart.
    /// </summary>
    public List<(TilePoint Pitch, CampZone Zone)> OpenPitches()
    {
        var open = new List<(TilePoint, CampZone)>();
        var roomy = new List<TilePoint>();
        foreach (var p in _map.Pitches())
        {
            var clear = true;
            for (var dx = -3; dx <= 3 && clear; dx++)
            {
                for (var dy = -3; dy <= 3 && clear; dy++)
                {
                    var t = new TilePoint(p.X + dx, p.Y + dy);
                    clear = !_map.IsBlocked(t) && !_site.IsFireLane(t);
                }
            }

            if (!clear)
            {
                continue;
            }

            var zone = _site.CampZoneAt(p);
            var spacing = CampLayout.SpacingOn(zone);
            if (spacing > 0)
            {
                if (roomy.Any(r => _site.CampZoneAt(r) == zone && ((r.X - p.X) * (r.X - p.X)) + ((r.Y - p.Y) * (r.Y - p.Y)) < spacing * spacing))
                {
                    continue;
                }

                roomy.Add(p);
            }

            open.Add((p, zone));
        }

        return open;
    }

    /// <summary>
    /// Who would pitch where, without pitching anyone: first everyone who wants marked ground and
    /// can have it, then everyone else on general ground, then whatever is left anywhere.
    /// </summary>
    public List<(Party Party, TilePoint Pitch)> PlanPitches()
    {
        var open = OpenPitches();
        var pools = new Dictionary<CampZone, Queue<TilePoint>>();
        foreach (var (p, zone) in open)
        {
            if (!pools.TryGetValue(zone, out var pool))
            {
                pools[zone] = pool = new Queue<TilePoint>();
            }

            pool.Enqueue(p);
        }

        var campers = _parties.Where(WouldCamp).ToList();
        var given = new Dictionary<Party, TilePoint>();

        // First those who bought one of the dearer ways to camp, on the ground kept for it.
        foreach (var party in campers)
        {
            var bought = BoughtKind(party);
            if (bought != CampKind.OwnTent && pools.TryGetValue(CampLayout.ZoneFor(bought), out var kept) && kept.Count > 0)
            {
                given[party] = kept.Dequeue();
            }
        }

        foreach (var party in campers.Where(c => !given.ContainsKey(c)))
        {
            var wish = WishOf(party);
            if (wish != CampZone.General && pools.TryGetValue(wish, out var pool) && pool.Count > 0)
            {
                given[party] = pool.Dequeue();
            }
        }

        var rest = new[] { CampZone.General, CampZone.Quiet, CampZone.Family, CampZone.Accessible };
        foreach (var party in campers.Where(c => !given.ContainsKey(c)))
        {
            foreach (var zone in rest)
            {
                // Families and quiet campers go anywhere before a pitch kept for accessible camping.
                if (pools.TryGetValue(zone, out var pool) && pool.Count > 0)
                {
                    given[party] = pool.Dequeue();
                    break;
                }
            }
        }

        return campers.Where(given.ContainsKey).Select(c => (c, given[c])).ToList();
    }

    /// <summary>
    /// How a camper feels about their pitch when they put their tent up: pleased to get the kind of
    /// camping they wanted, let down to be far from toilets or water, and badly let down if they
    /// need an accessible pitch and there is none.
    /// </summary>
    public double CampFeel(Party party)
    {
        if (party.Pitch is not { } pitch)
        {
            return 0;
        }

        var wish = WishOf(party);
        var zone = _site.CampZoneAt(pitch);
        var feel = 0.0;
        if (CampLayout.IsExtra(zone))
        {
            feel += ExtraWishMet;
        }
        else if (wish != CampZone.General && zone == wish)
        {
            feel += WishMet;
        }
        else if (wish == CampZone.Accessible)
        {
            feel -= NoAccess;
        }
        else if (wish == CampZone.Family && _site.HasCampZone(CampZone.Family))
        {
            feel -= FamilyLeftOut;
        }

        if (Nearest(pitch, Catalogue.CabinToilets) > CampLayout.FacilitiesReach)
        {
            feel -= NoToiletsNear;
        }

        if (wish == CampZone.Accessible && Nearest(pitch, StallKind.AccessibleToilet) > CampLayout.FacilitiesReach)
        {
            // Pitched where they can get round, perhaps, but nowhere near a toilet they can use.
            feel -= NoToiletsNear;
        }

        if (Nearest(pitch, StallKind.WaterPoint) > CampLayout.FacilitiesReach)
        {
            feel -= NoWaterNear;
        }

        return feel;
    }

    /// <summary>How far a pitch is from the nearest of these kinds of stall, in metres, as the crow flies.</summary>
    public double Nearest(TilePoint pitch, params StallKind[] kinds)
    {
        var best = double.MaxValue;
        foreach (var stall in _site.Stalls)
        {
            if (Array.IndexOf(kinds, stall.Kind) < 0)
            {
                continue;
            }

            var r = stall.Rect;
            var dx = Math.Max(0, Math.Max(r.X - pitch.X, pitch.X - (r.Right - 1)));
            var dy = Math.Max(0, Math.Max(r.Y - pitch.Y, pitch.Y - (r.Bottom - 1)));
            best = Math.Min(best, Math.Sqrt((dx * dx) + (dy * dy)) * FieldMap.TileMetres);
        }

        return best;
    }

    /// <summary>
    /// The chance a camper thinks about the showers, the info point or the camp shop: a shower in the
    /// afternoon and evening (more on a hot or wet day), a locker and the shop once they have pitched.
    /// Nobody else has a use for them.
    /// </summary>
    private static double CampAppeal(Party party, StallKind kind, double hour, bool hot, bool wet)
    {
        if (!party.Camper || party.Ticketed != true)
        {
            return 0;
        }

        return kind switch
        {
            StallKind.ShowerBlock => party.Showered || hour < 13 || hour >= 21 ? 0 : 0.03 * (hot ? 2 : 1) * (wet ? 1.8 : 1),
            StallKind.CampInfo => party.Lockered ? 0 : 0.012,
            StallKind.CampShop => party.CampShopped ? 0 : 0.01,
            _ => 0,
        };
    }
}
