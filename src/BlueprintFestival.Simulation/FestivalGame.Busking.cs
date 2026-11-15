using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// The entertainers: a guitarist, pianist, drummer or DJ hired like staff and put in a camping or
/// food field. They play 45-minute sets from the hours the player picks, and people walking past
/// stop to listen for five, ten or fifteen minutes. See <see cref="Busking"/> for the numbers.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How far apart two entertainers must be, in metres, so their music does not clash.</summary>
    public const double BuskersApart = 12;

    /// <summary>On a festival with no food field of its own, how far from the front of a stage an entertainer must be, in metres.</summary>
    public const double BuskFromStage = 25;

    private readonly Dictionary<int, HashSet<int>> _buskNoticed = new();
    private GameRandom? _busk;

    /// <summary>How many people have stopped to listen to an entertainer today.</summary>
    public int BuskListeners { get; private set; }

    /// <summary>Everyone hired to entertain.</summary>
    public IEnumerable<StaffMember> Entertainers => _staff.Where(s => Busking.IsEntertainer(s.Kind));

    /// <summary>The ground an entertainer and their instrument take up, centred on the tile they are posted on.</summary>
    public static TileRect PitchOf(TilePoint tile) => new(tile.X - 2, tile.Y - 1, 5, 4);

    /// <summary>Every tile of an entertainer's pitch.</summary>
    public static IEnumerable<TilePoint> PitchTiles(TilePoint tile)
    {
        var r = PitchOf(tile);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                yield return new TilePoint(x, y);
            }
        }
    }

    /// <summary>Whether an entertainer could be posted on a tile: why not, or null if they could.</summary>
    private string? CheckBusker(TilePoint tile)
    {
        var use = Field.UseAt(tile);
        var foodField = Field.FromPlan && Field.Fields.Any(f => f.Use == FieldUse.Food);
        if (use == FieldUse.Music && !foodField)
        {
            // No food field: the stalls are in with the music, so the entertainers can be too, well away from the stages.
            var (x, y) = FieldMap.CentreOf(tile);
            foreach (var stage in Site.Stages)
            {
                var (fx, fy, _, _) = SiteLayout.Front(stage.Rect, stage.Facing);
                if (Distance(x, y, fx, fy) < BuskFromStage)
                {
                    return $"Too close to a stage: entertainers play at least {BuskFromStage:0} m from the front of one.";
                }
            }
        }
        else if (use is not (FieldUse.Food or FieldUse.Camping))
        {
            return "Entertainers play in a camping field or a food field.";
        }

        foreach (var p in PitchTiles(tile))
        {
            if (!Field.IsWalkableForStaff(p) || !Field.Rented(p) || !Site.IsEmpty(p))
            {
                return "They need a clear patch of grass about 2.5 m across.";
            }
        }

        var (tx, ty) = FieldMap.CentreOf(tile);
        if (Entertainers.Any(s => Distance(s.PostX, s.PostY, tx, ty) < BuskersApart))
        {
            return $"Too close to another entertainer: keep them {BuskersApart:0} m apart.";
        }

        return null;
    }

    /// <summary>Marks the ground the entertainers stand on, so people walk round them.</summary>
    private void UpdatePitches() => Site.SetPitches(Entertainers.SelectMany(s => PitchTiles(s.Tile)));

    /// <summary>True if a set can be added at an hour, or taken away: before it has started.</summary>
    public bool CanChangeSet(int hour) =>
        hour is >= Busking.FirstHour and <= Busking.LastHour
        && (Phase == Phase.Build || (Phase == Phase.Day && Day + TimeSpan.FromHours(hour) > Now));

    /// <summary>
    /// Turns an entertainer's set at an hour on or off. Each set is 45 minutes from the hour. False if
    /// it cannot be changed now (it has started, or the hour is outside the day).
    /// </summary>
    public bool SetBuskHour(StaffMember member, int hour, bool on)
    {
        if (!Busking.IsEntertainer(member.Kind) || !_staff.Contains(member) || !CanChangeSet(hour))
        {
            return false;
        }

        if (on && !member.SetHours.Contains(hour))
        {
            member.SetHours.Add(hour);
            member.SetHours.Sort();
        }
        else if (!on)
        {
            member.SetHours.Remove(hour);
        }

        return true;
    }

    /// <summary>The hour of the set an entertainer is playing now, or -1 if they are not playing.</summary>
    public int BuskSetNow(StaffMember member)
    {
        if (Phase != Phase.Day || !Busking.IsEntertainer(member.Kind))
        {
            return -1;
        }

        var t = Now - Day;
        foreach (var hour in member.SetHours)
        {
            if (t >= TimeSpan.FromHours(hour) && t < Busking.EndOf(hour))
            {
                return hour;
            }
        }

        return -1;
    }

    /// <summary>True while an entertainer is playing.</summary>
    public bool IsBusking(StaffMember member) => BuskSetNow(member) >= 0;

    /// <summary>True if this set of an entertainer's has caught on and draws a crowd.</summary>
    public bool BuskCatchesOn(StaffMember member, int hour) => Busking.CatchesOn(Seed, member.Id, hour);

    /// <summary>How many people are stood listening to an entertainer right now.</summary>
    public int ListeningTo(StaffMember member) => Crowd.Parties.Where(p => p.Busker == member.Id && p.ListeningToBusker).Sum(p => p.Members.Count);

    /// <summary>How many people are listening, or on their way over to listen.</summary>
    private int DrawnTo(StaffMember member) => Crowd.Parties.Where(p => p.Busker == member.Id && p.IsOnSite).Sum(p => p.Members.Count);

    /// <summary>A few times a minute on the day: people passing an entertainer who is playing may stop to listen.</summary>
    private void TickBusking(DateTime now)
    {
        if (Phase != Phase.Day || Clock.TickCount % 5 != 0)
        {
            return;
        }

        foreach (var member in _staff)
        {
            var hour = BuskSetNow(member);
            if (hour < 0)
            {
                continue;
            }

            _busk ??= GameRandom.FromSeed(Seed, 67);
            var big = BuskCatchesOn(member, hour);
            var most = big ? Busking.CrowdWatching : Busking.FewWatching;
            var drawn = DrawnTo(member);
            var key = (member.Id * 100) + hour;
            if (!_buskNoticed.TryGetValue(key, out var noticed))
            {
                noticed = new HashSet<int>();
                _buskNoticed[key] = noticed;
            }

            var setEnds = Day + Busking.EndOf(hour);
            foreach (var party in Crowd.Parties)
            {
                if (drawn >= most)
                {
                    break;
                }

                if (party.State is not (PartyState.Strolling or PartyState.Standing) || party.Busker >= 0 || party.Watch >= 0 || party.Seat is not null
                    || now >= party.Leaves || noticed.Contains(party.Id)
                    || Distance(party.X, party.Y, member.PostX, member.PostY) > Busking.NoticeReach)
                {
                    continue;
                }

                // Each party passing makes its mind up once a set.
                noticed.Add(party.Id);
                var stops = _busk.NextDouble() < (big ? Busking.CrowdStopChance : Busking.StopChance);
                var minutes = Busking.WatchMinutes[_busk.Next(Busking.WatchMinutes.Count)];
                var angle = _busk.Between(-1.25, 1.25);
                var spread = _busk.Between(0, 1.6);
                if (!stops)
                {
                    continue;
                }

                // They stand in a ring in front, further back as the crowd grows.
                var radius = Math.Min(Busking.FurthestWatch, Busking.NearestWatch + (Busking.FurthestWatch - Busking.NearestWatch) * drawn / Busking.CrowdWatching * 1.6 + spread);
                var facing = member.PostHeading + angle;
                var (sx, sy) = (member.PostX + (Math.Sin(facing) * radius), member.PostY + (Math.Cos(facing) * radius));
                var spot = FieldMap.TileAt(sx, sy);
                if (!Field.IsWalkable(spot) || !Field.Rented(spot))
                {
                    continue;
                }

                var until = now + TimeSpan.FromMinutes(minutes);
                if (until > setEnds + TimeSpan.FromMinutes(1))
                {
                    until = setEnds + TimeSpan.FromMinutes(1);
                }

                if (Crowd.StopForBusker(party, member.Id, member.PostX, member.PostY, spot, until))
                {
                    drawn += party.Members.Count;
                    BuskListeners += party.Members.Count;
                }
            }
        }
    }
}
