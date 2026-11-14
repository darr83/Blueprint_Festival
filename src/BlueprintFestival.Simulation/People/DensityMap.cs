using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.People;

/// <summary>How crowded a place is. These are the three safety colours: green, amber and red.</summary>
public enum Crowding
{
    /// <summary>Under 2 people per square metre: free movement.</summary>
    Comfortable,

    /// <summary>2 to 4 people per square metre: slow going, more faints, shorter tempers.</summary>
    Busy,

    /// <summary>Over 4 people per square metre: people start to get hurt.</summary>
    CrushRisk,
}

/// <summary>
/// The whole map cut into two-metre squares, with a count of the people standing in each. The
/// game measures it again every minute of the day.
/// </summary>
public sealed class DensityMap
{
    /// <summary>The side of one square, in metres.</summary>
    public const double SquareMetres = 2.0;

    /// <summary>People per square metre at which a place is busy.</summary>
    public const double Busy = 2.0;

    /// <summary>People per square metre above which a place is a crush risk.</summary>
    public const double Crush = 4.0;

    private const double SquareArea = SquareMetres * SquareMetres;

    private readonly int[] _counts;

    public DensityMap(FieldMap map)
    {
        Across = (int)Math.Ceiling(map.Width * FieldMap.TileMetres / SquareMetres);
        Down = (int)Math.Ceiling(map.Height * FieldMap.TileMetres / SquareMetres);
        _counts = new int[Across * Down];
    }

    /// <summary>How many squares across the map.</summary>
    public int Across { get; }

    /// <summary>How many squares down the map.</summary>
    public int Down { get; }

    /// <summary>The most crowded square at the last measure, in people per square metre.</summary>
    public double Worst { get; private set; }

    /// <summary>The middle of the most crowded square, in metres.</summary>
    public (double X, double Y) WorstAt { get; private set; }

    /// <summary>How many squares were a crush risk at the last measure.</summary>
    public int RedSquares { get; private set; }

    /// <summary>How many squares were busy, but not a crush risk, at the last measure.</summary>
    public int AmberSquares { get; private set; }

    public static Crowding StatusOf(double perMetre) => perMetre > Crush ? Crowding.CrushRisk : perMetre >= Busy ? Crowding.Busy : Crowding.Comfortable;

    /// <summary>The square a place in metres falls in.</summary>
    public (int X, int Y) SquareOf(double x, double y) =>
        (Math.Clamp((int)Math.Floor(x / SquareMetres), 0, Across - 1), Math.Clamp((int)Math.Floor(y / SquareMetres), 0, Down - 1));

    /// <summary>The middle of a square, in metres.</summary>
    public static (double X, double Y) CentreOf(int squareX, int squareY) => ((squareX + 0.5) * SquareMetres, (squareY + 0.5) * SquareMetres);

    /// <summary>How many people stand in a square.</summary>
    public int CountIn(int squareX, int squareY) => _counts[(squareY * Across) + squareX];

    /// <summary>People per square metre in a square.</summary>
    public double In(int squareX, int squareY) => CountIn(squareX, squareY) / SquareArea;

    /// <summary>People per square metre at a place in metres.</summary>
    public double At(double x, double y)
    {
        var (sx, sy) = SquareOf(x, y);
        return In(sx, sy);
    }

    public Crowding StatusAt(double x, double y) => StatusOf(At(x, y));

    /// <summary>Counts everyone again, from where each person stands in metres.</summary>
    public void Measure(IEnumerable<(double X, double Y)> people)
    {
        Array.Clear(_counts);
        foreach (var person in people)
        {
            var (sx, sy) = SquareOf(person.X, person.Y);
            _counts[(sy * Across) + sx]++;
        }

        Worst = 0;
        RedSquares = AmberSquares = 0;
        for (var i = 0; i < _counts.Length; i++)
        {
            var perMetre = _counts[i] / SquareArea;
            if (perMetre > Crush)
            {
                RedSquares++;
            }
            else if (perMetre >= Busy)
            {
                AmberSquares++;
            }

            if (perMetre > Worst)
            {
                Worst = perMetre;
                WorstAt = CentreOf(i % Across, i / Across);
            }
        }
    }
}
