using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Staff on their rounds. Security and first-aiders walk a beat round where they were posted
/// instead of standing still all day, and are sent from wherever they have got to. Cleaners walk
/// the field picking up litter and emptying bins. Bins take the rubbish of most people who pass one.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>How many pieces of rubbish a bin holds before it has to be emptied.</summary>
    public const int BinHolds = 40;

    /// <summary>How far someone with rubbish in their hands will go out of their way to a bin, in metres.</summary>
    public const double BinReach = 12;

    /// <summary>How full a bin is before a cleaner goes to empty it.</summary>
    public const int BinWantsEmptying = 30;

    /// <summary>How far a cleaner can see litter from, in metres.</summary>
    public const double CleanerSight = 70;

    /// <summary>How far round them a cleaner clears with one stop, in metres.</summary>
    public const double PickReach = 1.5;

    private const int PickTicks = 20;
    private const int EmptyTicks = 90;

    private readonly Dictionary<TilePoint, int> _binFill = new();
    private GameRandom _beat = null!;

    /// <summary>Pieces of rubbish people have put in a bin instead of dropping.</summary>
    public int LitterBinned { get; private set; }

    /// <summary>Pieces of litter the cleaners have picked up off the ground.</summary>
    public int LitterPicked { get; private set; }

    /// <summary>How many times a cleaner has emptied a bin.</summary>
    public int BinsEmptied { get; private set; }

    /// <summary>How many pieces of rubbish are in a bin.</summary>
    public int BinFill(PlacedStall bin) => bin is not null && _binFill.TryGetValue(new TilePoint(bin.X, bin.Y), out var fill) ? fill : 0;

    /// <summary>How far each kind of staff wanders from their post, in metres. Ticket staff stay on their gate.</summary>
    public static double BeatOf(StaffKind kind) => kind switch
    {
        StaffKind.Security => 14,
        StaffKind.FirstAider => 12,
        StaffKind.Cleaner => 25,
        _ => 0,
    };

    private void StartRounds()
    {
        _beat = GameRandom.FromSeed(Seed, 59);
        Crowd.Binned = IntoBin;
        Site.Changed += () =>
        {
            // A bin that has been taken away takes its rubbish with it.
            foreach (var gone in _binFill.Keys.Where(k => !Site.Stalls.Any(s => s.Kind == StallKind.Bin && s.X == k.X && s.Y == k.Y)).ToList())
            {
                _binFill.Remove(gone);
            }
        };
    }

    /// <summary>Three people in four who would drop their rubbish use a bin instead, if one with room in it is close by.</summary>
    private bool IntoBin(Party party, int pieces)
    {
        if ((uint)party.Id * 2654435761u % 4 == 0)
        {
            return false;
        }

        PlacedStall? best = null;
        var nearest = BinReach;
        foreach (var stall in Site.Stalls)
        {
            if (stall.Kind != StallKind.Bin || BinFill(stall) >= BinHolds)
            {
                continue;
            }

            var (bx, by) = FieldMap.CentreOf(new TilePoint(stall.X, stall.Y));
            var away = Distance(bx, by, party.X, party.Y);
            if (away <= nearest)
            {
                (best, nearest) = (stall, away);
            }
        }

        if (best is null)
        {
            return false;
        }

        var key = new TilePoint(best.X, best.Y);
        var room = BinHolds - BinFill(best);
        var goesIn = Math.Min(room, pieces);
        _binFill[key] = BinFill(best) + goesIn;
        LitterBinned += goesIn;

        // Whatever will not fit ends up on the ground beside it.
        for (var i = goesIn; i < pieces; i++)
        {
            var (bx, by) = FieldMap.CentreOf(key);
            _litter.Add(new LitterPiece(Math.Round(bx + 0.9 + (0.3 * i), 2), Math.Round(by + 0.9, 2)));
        }

        return true;
    }

    /// <summary>True for security who do not leave their post just now: those behind a front barrier, and those minding the front of a stage while an act is on.</summary>
    private bool StaysPut(StaffMember member)
    {
        if (member.Kind != StaffKind.Security)
        {
            return false;
        }

        if (InPit(member) || OnAGap(member) || OnSteps(member))
        {
            return true;
        }

        if (!MusicOn)
        {
            return false;
        }

        foreach (var stage in Site.Stages)
        {
            var (deep, across) = InFrontOf(stage, member.PostX, member.PostY);
            if (deep >= -1 && deep <= 12 && Math.Abs(across) <= HalfWidth(stage) + 3)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What someone with nothing to deal with does: waits a little, walks their beat, and (a cleaner) goes after litter.</summary>
    private void Rounds(StaffMember member)
    {
        if (member.Kind == StaffKind.TicketStaff || Busking.IsEntertainer(member.Kind))
        {
            // Ticket staff stay on their gate, and entertainers on their pitch.
            return;
        }

        var cleaner = member.Kind == StaffKind.Cleaner;
        var working = Phase == Phase.Day || (cleaner && Phase == Phase.Tidy);
        switch (member.State)
        {
            case StaffState.AtPost or StaffState.Looking:
                if (!working || StaysPut(member))
                {
                    if (member.State == StaffState.Looking)
                    {
                        Walk(member, member.PostX, member.PostY, StaffState.Returning);
                    }

                    return;
                }

                if (cleaner && (Clock.TickCount + (member.Id * 7)) % 10 == 0 && SetOffToClean(member))
                {
                    return;
                }

                if (--member.Wait > 0)
                {
                    return;
                }

                if (BeatSpot(member) is { } spot)
                {
                    (member.HoldX, member.HoldY) = spot;
                    Walk(member, spot.X, spot.Y, StaffState.Patrolling);
                    if (member.Path is null or { Count: > 160 })
                    {
                        // No sensible way there: have a look round here instead.
                        member.Path = null;
                        member.State = member.State == StaffState.Patrolling && Distance(member.X, member.Y, member.PostX, member.PostY) < 0.3 ? StaffState.AtPost : StaffState.Looking;
                        member.Wait = 30;
                    }
                }
                else
                {
                    member.Wait = 30;
                }

                break;

            case StaffState.Patrolling:
                if (!working || StaysPut(member))
                {
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                }
                else if (cleaner && (Clock.TickCount + (member.Id * 7)) % 10 == 0 && SetOffToClean(member))
                {
                    return;
                }
                else if (Step(member))
                {
                    member.State = StaffState.Looking;
                    member.Wait = (int)_beat.Between(15, 60);
                }

                break;

            case StaffState.Cleaning:
                if (!working)
                {
                    Walk(member, member.PostX, member.PostY, StaffState.Returning);
                }
                else if (Step(member) && _sick.Any(p => Distance(p.X, p.Y, member.HoldX, member.HoldY) <= 1.6))
                {
                    // Sick to mop up: stand back from it a little and face it.
                    member.State = StaffState.Mopping;
                    member.HoldLeft = MopTicks;
                    member.Heading = Math.Atan2(member.HoldX - member.X, member.HoldY - member.Y);
                }
                else if (member.Path is null)
                {
                    member.State = StaffState.Picking;
                    member.HoldLeft = BinBy(member.X, member.Y) is { } bin && BinFill(bin) >= BinWantsEmptying ? EmptyTicks : PickTicks;
                    if (BinBy(member.X, member.Y) is { } facing && member.HoldLeft == EmptyTicks)
                    {
                        var (bx, by) = FieldMap.CentreOf(new TilePoint(facing.X, facing.Y));
                        member.Heading = Math.Atan2(bx - member.X, by - member.Y);
                    }
                }

                break;

            case StaffState.Mopping:
                if (--member.HoldLeft <= 0)
                {
                    Mopped(member);
                    member.State = StaffState.Looking;
                    member.Wait = 4;
                    if (working)
                    {
                        SetOffToClean(member);
                    }
                }

                break;

            case StaffState.Picking:
                if (--member.HoldLeft <= 0)
                {
                    var picked = _litter.RemoveAll(l => Distance(l.X, l.Y, member.X, member.Y) <= PickReach);
                    LitterPicked += picked;
                    if (BinBy(member.X, member.Y) is { } bin && BinFill(bin) >= BinWantsEmptying)
                    {
                        _binFill.Remove(new TilePoint(bin.X, bin.Y));
                        BinsEmptied++;
                    }

                    // Straight on to the next bit, if there is any in sight.
                    member.State = StaffState.Looking;
                    member.Wait = 4;
                    if (working)
                    {
                        SetOffToClean(member);
                    }
                }

                break;
        }
    }

    /// <summary>The bin a place is right beside, if any.</summary>
    private PlacedStall? BinBy(double x, double y)
    {
        foreach (var stall in Site.Stalls)
        {
            if (stall.Kind == StallKind.Bin)
            {
                var (bx, by) = (((stall.X + 1) * FieldMap.TileMetres), (stall.Y + 1) * FieldMap.TileMetres);
                if (Distance(bx, by, x, y) <= 1.6)
                {
                    return stall;
                }
            }
        }

        return null;
    }

    /// <summary>Sends a cleaner to the nearest bin that wants emptying, or else the nearest litter nobody else is going for. False if there is nothing in sight.</summary>
    private bool SetOffToClean(StaffMember member)
    {
        bool Taken(double x, double y) => _staff.Any(s => s != member && s.State is StaffState.Cleaning or StaffState.Picking or StaffState.Mopping && Distance(s.HoldX, s.HoldY, x, y) <= PickReach + 0.5);

        // Sick comes before anything else, and a cleaner will go further for it.
        if (SickToMop(member, CleanerSight * 1.5) is { } patch)
        {
            // Stop a step short of it, on the side they come from.
            var (px, py) = (member.X - patch.X, member.Y - patch.Y);
            var far = Math.Sqrt((px * px) + (py * py));
            var (sx, sy) = far < 0.9 ? (member.X, member.Y) : (patch.X + (px / far * 0.85), patch.Y + (py / far * 0.85));
            if (!Field.IsWalkableForStaff(FieldMap.TileAt(sx, sy)))
            {
                (sx, sy) = (patch.X, patch.Y);
            }

            var route = _staffPaths.FindPath(FieldMap.TileAt(member.X, member.Y), FieldMap.TileAt(sx, sy));
            if (route is not null)
            {
                (member.HoldX, member.HoldY) = (patch.X, patch.Y);
                member.State = StaffState.Cleaning;
                member.Path = route;
                member.PathIndex = 0;
                return true;
            }
        }

        (double X, double Y)? job = null;
        var nearest = CleanerSight;
        foreach (var stall in Site.Stalls)
        {
            if (stall.Kind != StallKind.Bin || BinFill(stall) < BinWantsEmptying)
            {
                continue;
            }

            // Stand on whichever side of the bin can be stood on.
            foreach (var (ox, oy) in new[] { (1, 2), (2, 0), (0, -1), (-1, 1) })
            {
                var beside = new TilePoint(stall.X + ox, stall.Y + oy);
                var (sx, sy) = FieldMap.CentreOf(beside);
                var away = Distance(sx, sy, member.X, member.Y);
                if (Field.IsWalkableForStaff(beside) && away < nearest && !Taken(sx, sy))
                {
                    (job, nearest) = ((sx, sy), away);
                    break;
                }
            }
        }

        if (job is null)
        {
            foreach (var piece in _litter)
            {
                var away = Distance(piece.X, piece.Y, member.X, member.Y);
                if (away < nearest && Field.IsWalkableForStaff(FieldMap.TileAt(piece.X, piece.Y)) && !Taken(piece.X, piece.Y))
                {
                    (job, nearest) = ((piece.X, piece.Y), away);
                }
            }
        }

        if (job is not { } to)
        {
            return false;
        }

        var way = _staffPaths.FindPath(FieldMap.TileAt(member.X, member.Y), FieldMap.TileAt(to.X, to.Y));
        if (way is null)
        {
            return false;
        }

        (member.HoldX, member.HoldY) = to;
        member.State = StaffState.Cleaning;
        member.Path = way;
        member.PathIndex = 0;
        return true;
    }

    /// <summary>Somewhere on their beat to walk to next: a clear bit of the festival within reach of their post.</summary>
    private (double X, double Y)? BeatSpot(StaffMember member)
    {
        var reach = BeatOf(member.Kind);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var angle = _beat.Between(0, Math.PI * 2);
            var far = reach * Math.Sqrt(_beat.Between(0.15, 1));
            var (x, y) = (member.PostX + (Math.Cos(angle) * far), member.PostY + (Math.Sin(angle) * far));
            var tile = FieldMap.TileAt(x, y);
            if (Field.IsWalkable(tile) && Field.InFestival(tile) && Distance(x, y, member.X, member.Y) > 3)
            {
                return FieldMap.CentreOf(tile);
            }
        }

        return null;
    }

    private RoundsSave SaveRounds() => new(_beat.State, _binFill.Select(b => new BinSave(b.Key.X, b.Key.Y, b.Value)).ToList(), LitterBinned, LitterPicked, BinsEmptied);

    private void RestoreRounds(RoundsSave? save)
    {
        if (save is null)
        {
            return;
        }

        _beat = new GameRandom(save.Beat);
        _binFill.Clear();
        foreach (var bin in save.Bins ?? new())
        {
            _binFill[new TilePoint(bin.X, bin.Y)] = bin.Fill;
        }

        (LitterBinned, LitterPicked, BinsEmptied) = (save.LitterBinned, save.LitterPicked, save.BinsEmptied);
    }
}

/// <summary>How full one bin is, as written to a save file.</summary>
public sealed record BinSave(int X, int Y, int Fill);

/// <summary>The staff's rounds and the bins, as written to a save file.</summary>
public sealed record RoundsSave(ulong Beat, List<BinSave>? Bins, int LitterBinned, int LitterPicked, int BinsEmptied);
