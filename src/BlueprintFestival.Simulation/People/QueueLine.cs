namespace BlueprintFestival.Simulation.People;

/// <summary>
/// One queue, at a gate lane or a stall: the places people stand in, one behind the next from the
/// front, and the parties in it. People queue in single file, so a family of four takes four
/// places. The line runs straight out from the front and bends where something is in the way.
/// </summary>
public sealed class QueueLine
{
    /// <summary>How far apart people stand in a queue, in metres.</summary>
    public const double Spacing = 0.8;

    private readonly List<(double X, double Y)> _places = new();
    private readonly double _frontX;
    private readonly double _frontY;

    internal QueueLine(double frontX, double frontY)
    {
        _frontX = frontX;
        _frontY = frontY;
    }

    /// <summary>Where each place in the line is, in metres, from the front.</summary>
    public IReadOnlyList<(double X, double Y)> Places => _places;

    /// <summary>The parties in the line: those standing in it first, in order, then those still walking to the back.</summary>
    internal List<Party> Parties { get; } = new();

    /// <summary>How many places were fenced in by the player as a queue lane, counted from the front.</summary>
    public int Fenced { get; internal set; }

    internal void Add(double x, double y) => _places.Add((x, y));

    /// <summary>How many people are standing in the line (not counting anyone still walking to it).</summary>
    public int Standing
    {
        get
        {
            var people = 0;
            foreach (var p in Parties)
            {
                if (p.InLine)
                {
                    people += p.Members.Count - p.Checked;
                }
            }

            return people;
        }
    }

    /// <summary>How many people are in the line or on their way to it.</summary>
    public int People
    {
        get
        {
            var people = 0;
            foreach (var p in Parties)
            {
                people += p.Members.Count - p.Checked;
            }

            return people;
        }
    }

    /// <summary>
    /// Where someone a given number of places from the front stands, and the way they face. Part
    /// places are in between, so people can be seen shuffling up. Past the last place everyone
    /// bunches at the back.
    /// </summary>
    public (double X, double Y, double Heading) At(double place)
    {
        if (_places.Count == 0)
        {
            return (_frontX, _frontY, 0);
        }

        place = Math.Clamp(place, 0, _places.Count - 1);
        var behind = Math.Min(_places.Count - 1, (int)Math.Ceiling(place));
        var ahead = Math.Max(0, behind - 1);
        var (ax, ay) = behind == 0 ? (_frontX, _frontY) : _places[ahead];
        var (bx, by) = _places[behind];
        var heading = Math.Atan2(ax - bx, ay - by);
        if (behind == 0 || behind == place)
        {
            return (bx, by, heading);
        }

        var along = place - ahead;
        return (ax + ((bx - ax) * along), ay + ((by - ay) * along), heading);
    }
}

/// <summary>How a toilet cubicle looks from outside.</summary>
public enum CubicleLook
{
    /// <summary>Door shut, sign green.</summary>
    Vacant,

    /// <summary>Door open: someone is going in or coming out.</summary>
    DoorOpen,

    /// <summary>Door shut, sign red.</summary>
    Engaged,
}

internal enum CubiclePhase
{
    Approaching,
    Entering,
    Inside,
    Leaving,
}

/// <summary>One person's visit to a toilet cubicle: who, how far through it they are, and where they are standing.</summary>
internal sealed class CubicleUse
{
    public CubicleUse(Party party, int member)
    {
        Party = party;
        Member = member;
    }

    public Party Party { get; }

    public int Member { get; }

    public CubiclePhase Phase { get; set; }

    public int Left { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public double Heading { get; set; }
}
