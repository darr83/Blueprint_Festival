namespace BlueprintFestival.Simulation.World;

/// <summary>What a rented field is for.</summary>
public enum FieldUse
{
    /// <summary>A stage and the crowd in front of it. The only fields a stage can stand in.</summary>
    Music = 0,

    /// <summary>Stalls, bars, toilets and room to sit down.</summary>
    Food = 1,

    /// <summary>Tents. The only fields people camp in.</summary>
    Camping = 2,

    /// <summary>Cars, parked for a charge.</summary>
    CarPark = 3,
}

/// <summary>
/// One field of the Larkmoor estate. X and Y are the north-west corner of its grass in metres on
/// the estate map. Fields in the same block (the lane and the farm track cut the estate in four)
/// that sit either side of one hedge are neighbours.
/// </summary>
public sealed record EstateField(string Id, string Name, int X, int Y, int MetresWide, int MetresDeep, string Block, bool OnLane, bool OnTrack)
{
    public int Right => X + MetresWide;

    public int Bottom => Y + MetresDeep;

    /// <summary>Square metres of grass.</summary>
    public int Area => MetresWide * MetresDeep;

    /// <summary>What the farmer charges for the year. The parish lends the village green for nothing.</summary>
    public int Rent => Id == Estate.VillageGreen ? 0 : (int)Math.Round(Area * 0.12 / 10) * 10;

    /// <summary>How many the field holds for a use: people watching, people eating, campers or cars.</summary>
    public int RoomFor(FieldUse use) => use switch
    {
        FieldUse.Music => Area / 4 / 10 * 10,
        FieldUse.Food => Area / 6 / 10 * 10,
        FieldUse.Camping => Area / 14 / 10 * 10,
        _ => Area / 25 / 5 * 5,
    };
}

/// <summary>The fixed map of the Larkmoor estate: nineteen fields round a lane, a farm track and the village.</summary>
public static class Estate
{
    public const string VillageGreen = "green";

    /// <summary>The hedge between two neighbouring fields is this wide, in metres.</summary>
    public const int HedgeMetres = 2;

    /// <summary>The farm track runs north from the lane between these two lines, in metres.</summary>
    public const int TrackLeft = 244;

    public const int TrackRight = 258;

    /// <summary>The verge and the lane lie between the fields north of it and the fields south of it.</summary>
    public const int LaneTop = 238;

    public const int LaneBottom = 262;

    /// <summary>Where the bus stops, on the lane just east of the farm track.</summary>
    public const int BusStopX = 268;

    public const int MetresWide = 542;

    public const int MetresDeep = 330;

    public static readonly IReadOnlyList<EstateField> All = new[]
    {
        new EstateField("far", "Far Common", 0, 0, 138, 78, "NW", false, false),
        new EstateField("larks", "Larks' Rise", 140, 0, 104, 78, "NW", false, true),
        new EstateField("orchard", "Orchard Piece", 0, 80, 72, 84, "NW", false, false),
        new EstateField("show", "The Showground", 74, 80, 170, 84, "NW", false, true),
        new EstateField("glebe", "Glebe Paddock", 0, 166, 58, 72, "NW", true, false),
        new EstateField("home", "Home Farm Meadow", 60, 166, 102, 72, "NW", true, false),
        new EstateField("ten", "Ten Acre", 164, 166, 80, 72, "NW", true, true),
        new EstateField("top", "Top Pasture", 258, 0, 114, 78, "NE", false, true),
        new EstateField("quarry", "Quarry Field", 374, 0, 168, 78, "NE", false, false),
        new EstateField("hill", "Hill Field", 258, 80, 126, 84, "NE", false, true),
        new EstateField("sheep", "Sheepwash", 436, 80, 106, 84, "NE", false, false),
        new EstateField("long", "Long Acre", 258, 166, 90, 72, "NE", true, true),
        new EstateField("station", "Station Field", 350, 166, 78, 72, "NE", true, false),
        new EstateField("mill", "Mill Close", 430, 166, 112, 72, "NE", true, false),
        new EstateField(VillageGreen, "Village Green", 72, 262, 60, 40, "SW", true, false),
        new EstateField("church", "Church Field", 134, 262, 72, 54, "SW", true, false),
        new EstateField("vicarage", "Vicarage Paddock", 208, 262, 36, 36, "SW", true, false),
        new EstateField("laneend", "Lane End", 258, 262, 102, 48, "SE", true, false),
        new EstateField("brook", "Brook Meadow", 362, 262, 114, 66, "SE", true, false),
    };

    public static EstateField? Find(string id) => All.FirstOrDefault(f => f.Id == id);

    /// <summary>The least two fields must overlap along a hedge for a gateway to go through it, in metres.</summary>
    public const int LeastShared = 12;

    /// <summary>True if the two fields sit either side of one hedge with room for a gateway.</summary>
    public static bool Neighbours(EstateField a, EstateField b) => Gateway(a, b) is not null;

    /// <summary>
    /// The middle of the hedge two neighbours share, in metres, and whether the hedge runs north
    /// to south. Null if they are not neighbours.
    /// </summary>
    public static (double X, double Y, bool NorthSouth)? Gateway(EstateField a, EstateField b)
    {
        if (a.Id == b.Id || a.Block != b.Block)
        {
            return null;
        }

        var acrossX = Math.Max(a.X, b.X) - Math.Min(a.Right, b.Right);
        var acrossY = Math.Max(a.Y, b.Y) - Math.Min(a.Bottom, b.Bottom);
        var sharedX = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        var sharedY = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        if (acrossX == HedgeMetres && sharedY >= LeastShared)
        {
            return (Math.Min(a.Right, b.Right) + (HedgeMetres / 2.0), Math.Max(a.Y, b.Y) + (sharedY / 2.0), true);
        }

        if (acrossY == HedgeMetres && sharedX >= LeastShared)
        {
            return (Math.Max(a.X, b.X) + (sharedX / 2.0), Math.Min(a.Bottom, b.Bottom) + (HedgeMetres / 2.0), false);
        }

        return null;
    }
}

/// <summary>One field taken for the year and what it is for, as it is saved.</summary>
public sealed record FieldChoice(string Field, FieldUse Use);

/// <summary>The fields rented for a year and what each is for.</summary>
public sealed class FieldPlan
{
    private readonly Dictionary<string, FieldUse> _uses = new();

    public FieldPlan()
    {
    }

    public FieldPlan(IEnumerable<FieldChoice> choices)
    {
        foreach (var c in choices)
        {
            if (Estate.Find(c.Field) is not null && Enum.IsDefined(c.Use))
            {
                _uses[c.Field] = c.Use;
            }
        }
    }

    /// <summary>The first year's plan: the village green, for music, and nothing else.</summary>
    public static FieldPlan FirstYear() => new(new[] { new FieldChoice(Estate.VillageGreen, FieldUse.Music) });

    /// <summary>The smallest licence at which camping is allowed.</summary>
    public const int CampingFrom = 5000;

    public FieldUse? UseOf(string id) => _uses.TryGetValue(id, out var use) ? use : null;

    /// <summary>Rents a field for a use, or gives it up with null.</summary>
    public void Set(string id, FieldUse? use)
    {
        if (Estate.Find(id) is null)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No such field.");
        }

        if (use is { } u)
        {
            _uses[id] = u;
        }
        else
        {
            _uses.Remove(id);
        }
    }

    /// <summary>The fields rented, in the order of the estate map.</summary>
    public IEnumerable<EstateField> Fields => Estate.All.Where(f => _uses.ContainsKey(f.Id));

    public IEnumerable<EstateField> Of(FieldUse use) => Estate.All.Where(f => UseOf(f.Id) == use);

    /// <summary>The fields inside the festival: everything rented but the car parks.</summary>
    public IEnumerable<EstateField> Festival => Fields.Where(f => UseOf(f.Id) != FieldUse.CarPark);

    public int Rent => Fields.Sum(f => f.Rent);

    /// <summary>How many people fit in front of the stages. No more tickets than this can be sold.</summary>
    public int MusicRoom => Of(FieldUse.Music).Sum(f => f.RoomFor(FieldUse.Music));

    public int CampingRoom => Of(FieldUse.Camping).Sum(f => f.RoomFor(FieldUse.Camping));

    public int ParkingSpaces => Of(FieldUse.CarPark).Where(f => f.OnLane).Sum(f => f.RoomFor(FieldUse.CarPark));

    /// <summary>Festival fields that cannot be walked to through gateways from the first music field.</summary>
    public IReadOnlyList<EstateField> CutOff
    {
        get
        {
            var festival = Festival.ToList();
            if (festival.Count == 0)
            {
                return festival;
            }

            var reached = new HashSet<string>();
            var waiting = new Stack<EstateField>();
            waiting.Push(festival.FirstOrDefault(f => UseOf(f.Id) == FieldUse.Music) ?? festival[0]);
            while (waiting.Count > 0)
            {
                var at = waiting.Pop();
                if (!reached.Add(at.Id))
                {
                    continue;
                }

                foreach (var next in festival.Where(f => !reached.Contains(f.Id) && Estate.Neighbours(at, f)))
                {
                    waiting.Push(next);
                }
            }

            return festival.Where(f => !reached.Contains(f.Id)).ToList();
        }
    }

    /// <summary>
    /// The field people come in through: the festival field on the lane nearest the bus stop
    /// (a camping field only if there is no other), or failing that the one on the farm track
    /// nearest the lane. Null if no festival field touches either.
    /// </summary>
    public EstateField? WayIn =>
        Festival.Where(f => f.OnLane).OrderBy(f => UseOf(f.Id) == FieldUse.Camping ? 1 : 0)
            .ThenBy(f => Math.Abs(f.X + (f.MetresWide / 2) - Estate.BusStopX)).FirstOrDefault()
        ?? Festival.Where(f => f.OnTrack).OrderByDescending(f => f.Bottom).FirstOrDefault();

    /// <summary>Everything that stops this plan being booked, in plain words. Empty if it is good to go.</summary>
    public IReadOnlyList<string> Problems(int licence, int year)
    {
        var problems = new List<string>();
        if (!Of(FieldUse.Music).Any())
        {
            problems.Add("Choose a music field for the stage.");
        }

        if (year <= 1 && Fields.Any(f => f.Id != Estate.VillageGreen))
        {
            problems.Add("In the first year the parish lends the village green and that is the only field.");
        }

        var cutOff = CutOff;
        if (cutOff.Count > 0)
        {
            problems.Add($"{Names(cutOff)} {(cutOff.Count == 1 ? "does" : "do")} not share a hedge with the rest of the site, so nobody could walk there.");
        }

        if (Festival.Any() && WayIn is null)
        {
            problems.Add("No festival field touches the lane or the farm track, so nobody can get in.");
        }

        var stranded = Of(FieldUse.CarPark).Where(f => !f.OnLane).ToList();
        if (stranded.Count > 0)
        {
            problems.Add($"{Names(stranded)} cannot be a car park because cars cannot reach {(stranded.Count == 1 ? "it" : "them")} from the lane.");
        }

        if (licence < CampingFrom && Of(FieldUse.Camping).Any())
        {
            problems.Add($"Camping opens when the licence reaches {CampingFrom:N0}.");
        }

        return problems;
    }

    public List<FieldChoice> Save() => Estate.All.Where(f => _uses.ContainsKey(f.Id)).Select(f => new FieldChoice(f.Id, _uses[f.Id])).ToList();

    public FieldPlan Copy() => new(Save());

    private static string Names(IReadOnlyList<EstateField> fields) =>
        fields.Count == 1 ? fields[0].Name : string.Join(", ", fields.Take(fields.Count - 1).Select(f => f.Name)) + " and " + fields[^1].Name;
}
