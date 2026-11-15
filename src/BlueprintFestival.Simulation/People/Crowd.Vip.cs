using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>
/// VIP guests: the parties who came on VIP tickets. They wear a gold lanyard, and they are the
/// only ones who may walk on VIP ground or use a stall kept for VIPs.
/// </summary>
public sealed partial class Crowd
{
    /// <summary>The colour of the VIP lanyard.</summary>
    public const string LanyardColour = "#E2B13C";

    private int _vipTickets;

    /// <summary>How many of the crowd hold VIP tickets. Setting it picks which parties they are.</summary>
    public int VipTickets
    {
        get => _vipTickets;
        set
        {
            _vipTickets = Math.Max(0, value);
            MarkVips();
        }
    }

    /// <summary>How many VIP guests are on site now.</summary>
    public int VipsOnSite => _parties.Where(p => p.Vip && p.IsOnSite).Sum(p => p.Members.Count);

    /// <summary>How many VIP guests there are in the whole crowd, counted in people.</summary>
    public int Vips => _parties.Where(p => p.Vip).Sum(p => p.Members.Count);

    /// <summary>
    /// Picks the VIP parties, in an order that comes from the day's seed, until there are as many
    /// VIP guests as VIP tickets sold. A party that would go over is skipped for a smaller one.
    /// </summary>
    private void MarkVips()
    {
        var left = _vipTickets;
        foreach (var party in _parties.OrderBy(p => Roll(p.Id, 91, 0)))
        {
            var vip = left > 0 && party.Members.Count <= left;
            if (vip)
            {
                left -= party.Members.Count;
            }

            if (party.Vip == vip)
            {
                continue;
            }

            party.Vip = vip;
            foreach (var member in party.Members)
            {
                member.Look = member.Look with { Lanyard = vip ? LanyardColour : null };
            }
        }
    }

    /// <summary>True if this party may use a seat, sink or shade: anyone, unless it stands on VIP ground.</summary>
    private bool MayUse(Party party, PlacedComfort comfort) => party.Vip || !_site.ForVips(comfort);

    /// <summary>True if this party may use this stall: anyone at an ordinary stall, only VIPs at a VIP one.</summary>
    public static bool MayUse(Party party, PlacedStall stall) => party.Vip || !stall.ForVips;

    /// <summary>How many seconds more of queue a VIP guest will put up with to use a stall kept for VIPs.</summary>
    public const double VipPreference = 240;

    /// <summary>How much a VIP guest's mood lifts in an hour spent on VIP ground, in the lounge or on the platform.</summary>
    public const double VipAreaPerHour = 0.05;

    /// <summary>How much a VIP guest's mood falls in an hour when there is nothing for VIPs at all.</summary>
    public const double NoPerksPerHour = 0.03;

    /// <summary>How far a VIP platform can be from a stage's front and still be a place to watch it from, in metres.</summary>
    public const double PlatformReach = 45;

    private readonly List<TilePoint> _vipStand = new();
    private readonly List<(PlacedStall Platform, List<TilePoint> Deck)> _decks = new();

    /// <summary>True when the festival has anything at all for VIPs: VIP ground, a VIP building or a stall kept for them.</summary>
    public bool HasVipPerks { get; private set; }

    /// <summary>Works out again where VIP guests can stand about, and which platforms they can watch from.</summary>
    private void RefreshVipPlaces()
    {
        _vipStand.Clear();
        _decks.Clear();
        foreach (var stall in _site.Stalls)
        {
            if (stall.Kind == StallKind.VipPlatform)
            {
                _decks.Add((stall, _site.InsideOf(stall).ToList()));
            }
            else if (stall.Kind == StallKind.VipLounge)
            {
                _vipStand.AddRange(_site.InsideOf(stall));
            }
        }

        _vipStand.AddRange(_site.VipGround.Where(p => _map.IsWalkableForVip(p) && _site.SideOf(p) == Side.Inside));
        HasVipPerks = _site.VipGround.Count > 0 || _site.Stalls.Any(s => s.ForVips);
    }

    /// <summary>
    /// How a VIP guest's mood moves in an hour from being a VIP: up while they enjoy VIP ground,
    /// the lounge or the platform, down slowly if they paid for VIP and there is nothing for them.
    /// </summary>
    private double VipFeel(Party party)
    {
        if (!party.Vip || !party.IsOnSite || party.Ticketed is null)
        {
            return 0;
        }

        if (!HasVipPerks)
        {
            return -NoPerksPerHour;
        }

        return _map.IsVipOnly(FieldMap.TileAt(party.X, party.Y)) ? VipAreaPerHour : 0;
    }

    /// <summary>Somewhere to wander to: a VIP guest often heads for the VIP area, everyone else anywhere in the field.</summary>
    private TilePoint? Wander(Party party)
    {
        if (party.Vip && _vipStand.Count > 0 && party.Random.NextDouble() < 0.6)
        {
            return _vipStand[party.Random.Next(_vipStand.Count)];
        }

        return SomewhereInField(party.Random);
    }

    /// <summary>
    /// A free place on the deck of a VIP platform near a stage, for a VIP party to watch from, or
    /// null if there is no platform near enough or it is full. Most VIPs use it; a few still like
    /// to be down in the crowd.
    /// </summary>
    private TilePoint? PlatformSpot(Party party, PlacedStage stage)
    {
        if (_decks.Count == 0 || party.Random.NextDouble() >= 0.8)
        {
            return null;
        }

        var (fx, fy, _, _) = RunningOrder.Front(stage);
        foreach (var (platform, deck) in _decks.OrderBy(d => Away(d.Platform, fx, fy)))
        {
            if (Away(platform, fx, fy) > PlatformReach || deck.Count == 0)
            {
                continue;
            }

            var start = party.Random.Next(deck.Count);
            for (var k = 0; k < deck.Count; k++)
            {
                var tile = deck[(start + k) % deck.Count];
                var (x, y) = FieldMap.CentreOf(tile);
                if (!_parties.Any(p => p != party && p.Vip && p.IsOnSite && p.Watch >= 0 && Near(p.Target, x, y, 1.0 + (0.25 * party.Members.Count))))
                {
                    return tile;
                }
            }
        }

        return null;
    }

    /// <summary>True if this party may walk on a tile: VIP guests may go on VIP ground too.</summary>
    private bool WalkableFor(Party party, TilePoint tile) => party.Vip ? _map.IsWalkableForVip(tile) : _map.IsWalkable(tile);

    private static double Away(PlacedStall stall, double x, double y)
    {
        var (cx, cy) = (FieldMap.TileMetres * (stall.Rect.X + (stall.Rect.Width / 2.0)), FieldMap.TileMetres * (stall.Rect.Y + (stall.Rect.Height / 2.0)));
        return Math.Sqrt(((cx - x) * (cx - x)) + ((cy - y) * (cy - y)));
    }

    private static bool Near(TilePoint tile, double x, double y, double metres)
    {
        var (tx, ty) = FieldMap.CentreOf(tile);
        return Math.Abs(tx - x) < metres && Math.Abs(ty - y) < metres;
    }
}
