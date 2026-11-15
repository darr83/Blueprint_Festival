using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>Decorations: things that do nothing but make the field a nicer place to be.</summary>
public enum DecorKind
{
    Scarecrow,
    FlowerPlanter,
    FlagPole,
    FirePit,
    GiantSunflower,

    /// <summary>The festival's name in letters taller than a person.</summary>
    GiantLetters,
}

/// <summary>
/// A decoration's name, price and size in tiles when it faces south, how much it cheers people
/// near it (its charm) and how far away that is felt, in metres.
/// </summary>
public sealed record DecorInfo(DecorKind Kind, string Name, int Cost, int Width, int Depth, double Charm, double Reach, string About);

/// <summary>A decoration that has been put out. Giant letters keep the word they spell.</summary>
public sealed record PlacedDecor(DecorKind Kind, int X, int Y, Facing Facing, string? Word = null)
{
    public TileRect Rect
    {
        get
        {
            var info = Decor.Of(Kind);
            var width = Kind == DecorKind.GiantLetters ? Decor.LettersWidth(Word) : info.Width;
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, width, info.Depth) : new TileRect(X, Y, info.Depth, width);
        }
    }

    /// <summary>The middle of it, in metres.</summary>
    public (double X, double Y) Middle
    {
        get
        {
            var r = Rect;
            return ((r.X + (r.Width / 2.0)) * FieldMap.TileMetres, (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres);
        }
    }
}

/// <summary>A line of bunting from one pole to another.</summary>
public sealed record BuntingRun(TilePoint From, TilePoint To)
{
    /// <summary>How long it is, in metres.</summary>
    public double Metres => Math.Sqrt(((To.X - From.X) * (To.X - From.X)) + ((To.Y - From.Y) * (To.Y - From.Y))) * FieldMap.TileMetres;

    /// <summary>The middle of it, in metres.</summary>
    public (double X, double Y) Middle => ((From.X + To.X + 1) / 2.0 * FieldMap.TileMetres, (From.Y + To.Y + 1) / 2.0 * FieldMap.TileMetres);
}

/// <summary>The catalogue of decorations, and how they cheer people up.</summary>
public static class Decor
{
    private static readonly DecorInfo[] All =
    {
        new(DecorKind.Scarecrow, "Scarecrow", 10, 1, 1, 0.2, 8, "A friendly scarecrow. Children love him."),
        new(DecorKind.FlowerPlanter, "Flower planter", 12, 2, 2, 0.2, 8, "Half a barrel full of flowers."),
        new(DecorKind.FlagPole, "Flag pole", 15, 1, 1, 0.25, 12, "A long flag on a tall pole, seen from across the field."),
        new(DecorKind.FirePit, "Fire pit", 40, 3, 3, 0.4, 10, "A ring of stones with a fire in it. It glows after dark, when it cheers people twice as much."),
        new(DecorKind.GiantSunflower, "Giant sunflower", 80, 2, 2, 0.5, 14, "A sunflower four metres tall. Somewhere to meet."),
        new(DecorKind.GiantLetters, "Giant letters", 150, 3, 2, 0.8, 18, "Your festival's name in letters taller than a person. Everybody wants their picture taken with it."),
    };

    /// <summary>What a metre of bunting costs.</summary>
    public const int BuntingPerMetre = 2;

    /// <summary>The longest one line of bunting can be, in metres. Carry on from its pole for more.</summary>
    public const double LongestBunting = 16;

    /// <summary>How much a line of bunting cheers people near it, and how near, in metres from its middle.</summary>
    public const double BuntingCharm = 0.15;

    public const double BuntingReach = 9;

    /// <summary>However much is put out, a place is never more charming than this.</summary>
    public const double MostCharm = 1;

    /// <summary>How much the most charming place lifts the mood of somebody there, an hour.</summary>
    public const double CharmPerHour = 0.025;

    /// <summary>The most letters giant letters will spell.</summary>
    public const int MostLetters = 10;

    /// <summary>The line decorations go under in the accounts.</summary>
    public const string Line = "Decorations";

    public static IReadOnlyList<DecorInfo> Everything => All;

    public static DecorInfo Of(DecorKind kind) => All[(int)kind];

    /// <summary>The word giant letters spell for a festival: the first word of its name, in capitals, letters and numbers only.</summary>
    public static string WordFor(string? festival)
    {
        var first = (festival ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var word = new string(first.ToUpperInvariant().Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).Take(MostLetters).ToArray());
        return word.Length == 0 ? "FESTIVAL" : word;
    }

    /// <summary>How wide a word in giant letters stands, in tiles: each letter is a little under a metre and a half.</summary>
    public static int LettersWidth(string? word) => (int)Math.Ceiling(Math.Max(1, (word ?? "").Length) * 1.44 / FieldMap.TileMetres) + 1;

    /// <summary>What a line of bunting costs: by the metre, and never less than two metres' worth.</summary>
    public static int BuntingCost(BuntingRun run) => BuntingPerMetre * Math.Max(2, (int)Math.Ceiling(run.Metres));
}

public sealed partial class SiteLayout
{
    private readonly List<PlacedDecor> _decor = new();
    private readonly List<BuntingRun> _bunting = new();
    private float[] _charm = Array.Empty<float>();
    private bool _charmStale = true;

    /// <summary>The decorations that have been put out.</summary>
    public IReadOnlyList<PlacedDecor> Decorations => _decor;

    /// <summary>The lines of bunting that have been strung.</summary>
    public IReadOnlyList<BuntingRun> Bunting => _bunting;

    public PlacedDecor? DecorAt(TilePoint p) => _decor.FirstOrDefault(d => d.Rect.Contains(p));

    /// <summary>True if a bunting pole stands on a tile.</summary>
    public bool IsBuntingPole(TilePoint p) => _bunting.Any(b => b.From == p || b.To == p);

    /// <summary>Whether a decoration can go with its corner on a tile, and what it costs.</summary>
    public BuildCheck CheckDecor(DecorKind kind, int x, int y, Facing facing, string? word = null)
    {
        var decor = new PlacedDecor(kind, x, y, facing, word);
        foreach (var p in TilesOf(decor.Rect))
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("It must stand on the grass inside the hedge.");
            }

            if (!IsEmpty(p))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Decor.Of(kind).Cost, null);
    }

    public BuildCheck AddDecor(DecorKind kind, int x, int y, Facing facing, string? word = null)
    {
        var check = CheckDecor(kind, x, y, facing, word);
        if (check.Ok)
        {
            _decor.Add(new PlacedDecor(kind, x, y, facing, kind == DecorKind.GiantLetters ? word : null));
            Refresh();
        }

        return check;
    }

    /// <summary>Whether bunting can be strung from one tile to another, and what it costs. Either end can be a bunting pole already there.</summary>
    public BuildCheck CheckBunting(TilePoint from, TilePoint to)
    {
        var run = new BuntingRun(from, to);
        if (run.Metres < 2)
        {
            return BuildCheck.No("Drag a little further: bunting needs two metres at least.");
        }

        if (run.Metres > Decor.LongestBunting)
        {
            return BuildCheck.No($"One line can be {Decor.LongestBunting:0} m at most. Carry on from its pole for more.");
        }

        if (_bunting.Any(b => (b.From == from && b.To == to) || (b.From == to && b.To == from)))
        {
            return BuildCheck.No("There is bunting there already.");
        }

        foreach (var p in new[] { from, to })
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("The poles must stand on the grass inside the hedge.");
            }

            if (!IsBuntingPole(p) && !IsEmpty(p))
            {
                return BuildCheck.No("Something is already built where a pole would go.");
            }
        }

        return new BuildCheck(true, Decor.BuntingCost(run), null);
    }

    public BuildCheck AddBunting(TilePoint from, TilePoint to)
    {
        var check = CheckBunting(from, to);
        if (check.Ok)
        {
            _bunting.Add(new BuntingRun(from, to));
            Refresh();
        }

        return check;
    }

    /// <summary>What taking away the decoration on a tile, or the bunting on a pole there, would give back, or 0.</summary>
    public int DecorRefundAt(TilePoint p) =>
        DecorAt(p) is { } decor ? Decor.Of(decor.Kind).Cost : _bunting.Where(b => b.From == p || b.To == p).Sum(Decor.BuntingCost);

    private bool RemoveDecorAt(TilePoint p) =>
        (DecorAt(p) is { } decor && _decor.Remove(decor)) || _bunting.RemoveAll(b => b.From == p || b.To == p) > 0;

    private IEnumerable<TilePoint> DecorInTheWay => _decor.SelectMany(d => TilesOf(d.Rect)).Concat(_bunting.SelectMany(b => new[] { b.From, b.To }));

    private void RestoreDecor(SiteSave save)
    {
        foreach (var d in save.Decor ?? new())
        {
            var word = d.Kind == DecorKind.GiantLetters ? Decor.WordFor(d.Word) : null;
            if (Enum.IsDefined(d.Kind) && CheckDecor(d.Kind, d.X, d.Y, d.Facing, word).Ok)
            {
                _decor.Add(d with { Word = word });
            }
        }

        foreach (var b in save.Bunting ?? new())
        {
            if (CheckBunting(b.From, b.To).Ok)
            {
                _bunting.Add(b);
            }
        }
    }

    /// <summary>
    /// How charming a place is, from 0 to <see cref="Decor.MostCharm"/>: the charm of every
    /// decoration within its reach, added up and capped, so a field stuffed with scarecrows is no
    /// better than one nicely dressed. A fire pit counts double after dark.
    /// </summary>
    public double CharmAt(double x, double y, bool dark = false)
    {
        if (_decor.Count == 0 && _bunting.Count == 0)
        {
            return 0;
        }

        var tile = FieldMap.TileAt(x, y);
        if (!_map.InBounds(tile.X, tile.Y))
        {
            return 0;
        }

        // Worked out once for each 2 m square, by day and by night.
        const int cell = 4;
        var across = (_map.Width / cell) + 1;
        var down = (_map.Height / cell) + 1;
        if (_charmStale || _charm.Length != across * down * 2)
        {
            _charmStale = false;
            _charm = new float[across * down * 2];
            for (var night = 0; night < 2; night++)
            {
                for (var cy = 0; cy < down; cy++)
                {
                    for (var cx = 0; cx < across; cx++)
                    {
                        var px = (cx + 0.5) * cell * FieldMap.TileMetres;
                        var py = (cy + 0.5) * cell * FieldMap.TileMetres;
                        var charm = 0.0;
                        foreach (var d in _decor)
                        {
                            var info = Decor.Of(d.Kind);
                            var (mx, my) = d.Middle;
                            if (((mx - px) * (mx - px)) + ((my - py) * (my - py)) <= info.Reach * info.Reach)
                            {
                                charm += info.Charm * (night == 1 && d.Kind == DecorKind.FirePit ? 2 : 1);
                            }
                        }

                        foreach (var b in _bunting)
                        {
                            var (mx, my) = b.Middle;
                            if (((mx - px) * (mx - px)) + ((my - py) * (my - py)) <= Decor.BuntingReach * Decor.BuntingReach)
                            {
                                charm += Decor.BuntingCharm;
                            }
                        }

                        _charm[(((night * down) + cy) * across) + cx] = (float)Math.Min(Decor.MostCharm, charm);
                    }
                }
            }
        }

        return _charm[((((dark ? 1 : 0) * down) + (tile.Y / cell)) * across) + (tile.X / cell)];
    }
}
