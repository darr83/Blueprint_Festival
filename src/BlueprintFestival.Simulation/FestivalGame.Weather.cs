using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>The kind of day the festival gets.</summary>
public enum WeatherKind
{
    /// <summary>Dry and pleasant.</summary>
    Fine,

    /// <summary>Dry and hot: people get thirsty quickly.</summary>
    Hot,

    /// <summary>Dry spells with a shower or two.</summary>
    Showers,

    /// <summary>Rain for most of the afternoon.</summary>
    Rain,
}

/// <summary>Something laid on the grass to keep feet out of the mud.</summary>
public enum GroundKind
{
    /// <summary>Straw scattered on the ground: cheap, and soaks up most of the mud.</summary>
    Straw,

    /// <summary>Metal trackway panels: dear, but the grass under them comes to no harm.</summary>
    Trackway,
}

/// <summary>
/// One day's weather: the kind of day, the afternoon high in degrees C, and how hard it rains in
/// each hour of the day from 0 (dry) to 1 (pouring).
/// </summary>
public sealed record DayWeather(WeatherKind Kind, int High, double[] Rain)
{
    public bool Wet => Kind is WeatherKind.Showers or WeatherKind.Rain;

    /// <summary>How hard it is raining at a time of day.</summary>
    public double RainAt(DateTime when) => Rain.Length == 24 ? Rain[when.Hour] : 0;

    /// <summary>The hours of the day in which it rains, as runs: (first hour, hour it stops).</summary>
    public IReadOnlyList<(int From, int To)> Spells
    {
        get
        {
            var spells = new List<(int, int)>();
            for (var h = 0; h < Rain.Length; h++)
            {
                if (Rain[h] > 0.05)
                {
                    var from = h;
                    while (h < Rain.Length && Rain[h] > 0.05)
                    {
                        h++;
                    }

                    spells.Add((from, h));
                }
            }

            return spells;
        }
    }

    /// <summary>The forecast as it reads on the morning: exact, as forecasts in games are.</summary>
    public string Summary
    {
        get
        {
            var spells = Spells;
            var what = Kind switch
            {
                WeatherKind.Hot => "Hot and sunny all day. People will be thirsty.",
                WeatherKind.Showers => "Showers " + string.Join(" and ", spells.Select(s => s.To - s.From == 1 ? $"around {Hour(s.From)}" : $"from {Hour(s.From)} to {Hour(s.To)}")) + ".",
                WeatherKind.Rain => "Rain " + string.Join(" and ", spells.Select(s => $"from {Hour(s.From)} to {Hour(s.To)}")) + ".",
                _ => "Dry and fine all day.",
            };
            return $"{what} High {High}°C.";
        }
    }

    /// <summary>One or two words for the top bar.</summary>
    public string Word => Kind switch { WeatherKind.Hot => "Hot", WeatherKind.Showers => "Showers", WeatherKind.Rain => "Rain", _ => "Fine" };

    private static string Hour(int hour) => hour == 0 || hour == 24 ? "midnight" : hour == 12 ? "noon" : hour < 12 ? $"{hour} am" : $"{hour - 12} pm";

    /// <summary>A day of one kind. The same seed always gives the same day.</summary>
    public static DayWeather Of(WeatherKind kind, int seed = 0)
    {
        var random = GameRandom.FromSeed(seed, 23);
        var rain = new double[24];
        switch (kind)
        {
            case WeatherKind.Showers:
                // Two showers, one either side of the middle of the afternoon.
                rain[11 + random.Next(4)] = Math.Round(random.Between(0.4, 0.7), 2);
                rain[16 + random.Next(4)] = Math.Round(random.Between(0.4, 0.7), 2);
                return new DayWeather(kind, 16 + random.Next(4), rain);
            case WeatherKind.Rain:
                var from = 12 + random.Next(3);
                var hours = 4 + random.Next(3);
                for (var h = from; h < from + hours; h++)
                {
                    rain[h] = Math.Round(random.Between(0.6, 1.0), 2);
                }

                rain[from - 1] = 0.25;
                rain[from + hours] = 0.25;
                return new DayWeather(kind, 13 + random.Next(4), rain);
            case WeatherKind.Hot:
                return new DayWeather(kind, 27 + random.Next(5), rain);
            default:
                return new DayWeather(kind, 19 + random.Next(5), rain);
        }
    }

    /// <summary>
    /// The weather a festival gets in one of its years. The first year is always fine, so nobody's
    /// first festival is rained off; after that it is an English July.
    /// </summary>
    public static DayWeather ForYear(int seed, int year)
    {
        if (year <= 1)
        {
            return Of(WeatherKind.Fine, seed);
        }

        var roll = GameRandom.FromSeed(seed, 25).NextDouble();
        return Of(roll < 0.40 ? WeatherKind.Fine : roll < 0.55 ? WeatherKind.Hot : roll < 0.82 ? WeatherKind.Showers : WeatherKind.Rain, seed);
    }
}

public sealed partial class FestivalGame
{
    /// <summary>What a rain poncho costs the festival, per person expected.</summary>
    public const int PonchoPrice = 1;

    /// <summary>What a worn-out patch of grass costs to put right when the field is handed back.</summary>
    public const int RepairPerPatch = 5;

    /// <summary>How worn a patch has to be before the farmer charges for it, from 0 to 1.</summary>
    public const double NeedsRepair = 0.75;

    /// <summary>How muddy the ground has to be before people start slipping over.</summary>
    public const double Slippery = 0.4;

    private byte[] _ground = Array.Empty<byte>();

    /// <summary>The weather on the day of the festival.</summary>
    public DayWeather Weather { get; private set; } = DayWeather.Of(WeatherKind.Fine);

    /// <summary>The long-range forecast's chance of rain, as a percentage. It is a guess: the build day's forecast is exact.</summary>
    public int RainChance { get; private set; } = 10;

    /// <summary>How wet the ground is, from 0 (dry) to 1 (sodden).</summary>
    public double Wetness { get; private set; }

    /// <summary>Whether rain ponchos have been ordered to hand out at the gate.</summary>
    public bool PonchosOrdered { get; private set; }

    /// <summary>What the ponchos would cost for the crowd expected.</summary>
    public int PonchoCost => ExpectedAt(TicketPrice) * PonchoPrice;

    /// <summary>How hard it is raining right now, from 0 to 1.</summary>
    public double RainNow => Phase == Phase.Day ? Weather.RainAt(Clock.Now) : 0;

    /// <summary>The long-range forecast shown on the planning screen.</summary>
    public string EarlyForecast =>
        (RainChance >= 50 ? "Unsettled" : RainChance >= 25 ? "Changeable" : Weather.Kind == WeatherKind.Hot ? "Settled and warm" : "Settled") + $": {RainChance}% chance of rain";

    /// <summary>How many people a forecast like this puts off, as a share of the crowd that still comes.</summary>
    private double ForecastPull => 1 - (0.3 * Math.Max(0, RainChance - 20) / 100.0);

    public static int CostOf(GroundKind kind) => kind == GroundKind.Trackway ? 10 : 3;

    public static string NameOf(GroundKind kind) => kind == GroundKind.Trackway ? "Trackway" : "Straw";

    private void StartWeather()
    {
        _ground = new byte[WearColumns * WearRows];
        if (Planned)
        {
            Weather = DayWeather.ForYear(Seed, Year);
            RainChance = ChanceFor(Weather);
        }

        Crowd.Mud = MudAtPlace;
        Dress();
    }

    private int ChanceFor(DayWeather weather)
    {
        if (Year <= 1 && !weather.Wet)
        {
            return 10;
        }

        var roll = GameRandom.FromSeed(Seed, 24).NextDouble();
        return 5 * (int)Math.Round((weather.Wet ? 45 + (roll * 40) : 5 + (roll * 35)) / 5);
    }

    private void Dress()
    {
        Crowd.DressedForRain = Weather.Wet;
        Crowd.FreePonchos = PonchosOrdered;
        Crowd.Heat = Weather.Kind switch { WeatherKind.Hot => 1.6, WeatherKind.Showers => 0.9, WeatherKind.Rain => 0.85, _ => 1.0 };
    }

    /// <summary>
    /// Sets the day's weather by hand, for tests and for pictures. Before the gates open the crowd
    /// is planned again, so people dress for it and the forecast puts some of them off.
    /// </summary>
    public void SetWeather(DayWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        Weather = weather;
        RainChance = ChanceFor(weather);
        Dress();
        if (Clock.TickCount == 0 && Phase is Phase.Plan or Phase.Build or Phase.Day)
        {
            Crowd.Replan(Planned && Phase == Phase.Plan ? ExpectedAt(TicketPrice) : Crowd.Expected);
        }
    }

    /// <summary>Orders rain ponchos to hand out at the gate, or cancels them. They are paid for with the acts.</summary>
    public void OrderPonchos(bool order)
    {
        Need(Phase.Plan);
        PonchosOrdered = order;
        Dress();
        Crowd.Replan(ExpectedAt(TicketPrice));
    }

    /// <summary>Which patch of ground a tile is on, or null outside the field.</summary>
    public (int Column, int Row)? PatchAt(TilePoint tile)
    {
        var f = Field.Field;
        return f.Contains(tile) ? (Math.Min(WearColumns - 1, (tile.X - f.X) / WearTiles), Math.Min(WearRows - 1, (tile.Y - f.Y) / WearTiles)) : null;
    }

    /// <summary>What is laid on a patch of ground, if anything.</summary>
    public GroundKind? GroundAt(int column, int row) => _ground[(row * WearColumns) + column] switch { 1 => GroundKind.Straw, 2 => GroundKind.Trackway, _ => null };

    /// <summary>How many patches have straw or trackway on them.</summary>
    public int GroundCovered => _ground.Count(g => g != 0);

    /// <summary>How muddy one patch is, from 0 to 1. Mud is wet ground that has been walked on.</summary>
    public double MudAt(int column, int row) => _ground[(row * WearColumns) + column] switch
    {
        2 => 0,
        1 => 0.3 * BareMud(column, row),
        _ => BareMud(column, row),
    };

    /// <summary>Wet grass that nobody has walked on is only wet; it takes feet to make mud of it.</summary>
    private double BareMud(int column, int row) => Wetness * Math.Clamp((WearAt(column, row) - 0.15) / 0.5, 0, 1);

    /// <summary>How muddy the ground is at a place in metres.</summary>
    public double MudAtPlace(double x, double y) => Wetness <= 0 ? 0 : PatchAt(FieldMap.TileAt(x, y)) is var (c, r) ? MudAt(c, r) : 0;

    /// <summary>How many patches of the field are slippery with mud.</summary>
    public int MuddyPatches
    {
        get
        {
            var count = 0;
            for (var r = 0; r < WearRows; r++)
            {
                for (var c = 0; c < WearColumns; c++)
                {
                    count += MudAt(c, r) >= Slippery ? 1 : 0;
                }
            }

            return count;
        }
    }

    /// <summary>What the farmer will charge to put the worn-out grass right.</summary>
    public int FieldRepair => _wear.Count(w => w >= NeedsRepair * WornOut) * RepairPerPatch;

    public BuildCheck CheckGround(GroundKind kind, int column, int row)
    {
        var cost = CostOf(kind);
        if (column < 0 || row < 0 || column >= WearColumns || row >= WearRows)
        {
            return new BuildCheck(false, cost, "That is outside the field.");
        }

        return GroundAt(column, row) == kind ? new BuildCheck(false, cost, $"There is {NameOf(kind).ToLowerInvariant()} here already.") : Afford(new BuildCheck(true, cost, null));
    }

    /// <summary>Lays straw or trackway on one patch and pays for it. It can be done on the day, once the mud shows.</summary>
    public BuildCheck LayGround(GroundKind kind, int column, int row)
    {
        var check = CheckGround(kind, column, row);
        if (check.Ok)
        {
            Spend("Ground cover", check.Cost);
            _ground[(row * WearColumns) + column] = (byte)(kind == GroundKind.Straw ? 1 : 2);
        }

        return check;
    }

    /// <summary>Takes up what is laid on a patch. On the build day the money comes back. Returns how much.</summary>
    public int LiftGround(int column, int row)
    {
        if (!CanBuild || column < 0 || row < 0 || column >= WearColumns || row >= WearRows || GroundAt(column, row) is not { } kind)
        {
            return 0;
        }

        _ground[(row * WearColumns) + column] = 0;
        var refund = Phase == Phase.Build ? CostOf(kind) : 0;
        if (refund > 0)
        {
            Spend("Ground cover", -refund);
        }

        return refund;
    }

    private void TickWeather(DateTime now)
    {
        if (Phase != Phase.Day || Clock.TickCount % 60 != 15)
        {
            return;
        }

        var rain = Weather.RainAt(now);
        Crowd.Rain = rain;

        // An hour of pouring rain leaves the ground most of the way to sodden; it takes a long dry spell to come back.
        Wetness = Math.Clamp(Wetness + (rain > 0.05 ? rain * 0.012 : Weather.Kind == WeatherKind.Hot ? -0.003 : -0.0012), 0, 1);
    }

    /// <summary>How much harder feet are on a patch than on dry grass: wet ground cuts up fast, and cover protects it.</summary>
    private double WearRate(int index) => _ground[index] switch
    {
        2 => 0,
        1 => 0.5,
        _ => 1 + (1.5 * Wetness),
    };

    private WeatherSave SaveWeather() => new(Weather, RainChance, Wetness, PonchosOrdered, _ground.Any(g => g != 0) ? _ground.ToArray() : null)
    {
        Falling = Crowd.Rain,
    };

    /// <summary>Puts the weather back before the crowd is planned again, so people are dressed for it.</summary>
    private void RestoreWeather(WeatherSave save)
    {
        if (save.Weather is { Rain.Length: 24 } weather)
        {
            Weather = weather;
        }

        RainChance = Math.Clamp(save.RainChance, 0, 100);
        Wetness = Math.Clamp(save.Wetness, 0, 1);
        PonchosOrdered = save.Ponchos;
        if (save.Ground is { } ground && ground.Length == _ground.Length)
        {
            _ground = ground.ToArray();
        }

        Dress();
        Crowd.Rain = Math.Clamp(save.Falling, 0, 1);
    }
}

/// <summary>The weather and the ground as written to a save file.</summary>
public sealed record WeatherSave(DayWeather? Weather, int RainChance, double Wetness, bool Ponchos, byte[]? Ground)
{
    /// <summary>How hard it was raining on the crowd when the game was saved.</summary>
    public double Falling { get; init; }
}
