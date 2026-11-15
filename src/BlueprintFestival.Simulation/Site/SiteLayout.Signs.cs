using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Site;

/// <summary>The signs the player can put up and write on.</summary>
public enum SignKind
{
    /// <summary>A chalk board on an A-frame. Stood by a stall it brings it more passing trade.</summary>
    SandwichBoard,

    /// <summary>A post with up to three pointing fingers. People near it know their way about.</summary>
    FingerPost,

    /// <summary>A cloth banner between two poles, high enough to walk under.</summary>
    Banner,

    /// <summary>A big board with a plan of the site on it. People near it know their way about.</summary>
    MapBoard,
}

/// <summary>A sign's name, price, size in tiles when it faces south, how many lines can be written on it and how long each can be.</summary>
public sealed record SignInfo(SignKind Kind, string Name, int Cost, int Width, int Depth, int Lines, int LineLength, string[] Words, string About);

/// <summary>
/// A sign that has been put up: where, which way it faces, the words on it, which of the sign
/// colours it is painted, and (for a finger post) which fingers point right, as one bit a finger.
/// </summary>
public sealed record PlacedSign(SignKind Kind, int X, int Y, Facing Facing, List<string>? Lines = null, int Colour = 0, int Points = 0)
{
    public TileRect Rect
    {
        get
        {
            var info = Signs.Of(Kind);
            return Facing is Facing.South or Facing.North ? new TileRect(X, Y, info.Width, info.Depth) : new TileRect(X, Y, info.Depth, info.Width);
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

    /// <summary>The words on line (or finger) i, or nothing.</summary>
    public string Line(int i) => Lines is { } lines && i >= 0 && i < lines.Count ? lines[i] : "";

    /// <summary>True if finger i points to the right as you look at the post.</summary>
    public bool PointsRight(int i) => (Points & (1 << i)) != 0;

    /// <summary>The tiles nobody can walk through: the two poles of a banner, and all of anything else.</summary>
    public IEnumerable<TilePoint> Solid()
    {
        var r = Rect;
        if (Kind == SignKind.Banner)
        {
            yield return new TilePoint(r.X, r.Y);
            yield return new TilePoint(r.Right - 1, r.Bottom - 1);
            yield break;
        }

        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                yield return new TilePoint(x, y);
            }
        }
    }
}

/// <summary>The catalogue of signs, and the rules for what can be written on them.</summary>
public static class Signs
{
    private static readonly SignInfo[] All =
    {
        new(SignKind.SandwichBoard, "Sandwich board", 6, 2, 2, 3, 12, new[] { "OPEN", "Come on in" },
            "A chalk board with your own words on. Stood by a stall it brings it more passing trade."),
        new(SignKind.FingerPost, "Finger post", 10, 1, 1, 3, 14, new[] { "Stage", "Toilets", "Food" },
            "Up to three fingers with your own words, each pointing left or right. People near one walk further to the stall they want."),
        new(SignKind.Banner, "Banner", 35, 9, 1, 1, 24, new[] { "WELCOME" },
            "A banner with your own words, high enough to walk under."),
        new(SignKind.MapBoard, "Site map board", 45, 5, 1, 1, 22, new[] { "YOU ARE HERE" },
            "A plan of your site as you have built it, with a title of your own. People near one walk further to the stall they want."),
    };

    /// <summary>How many colours a sign can be painted.</summary>
    public const int Colours = 5;

    /// <summary>How close to a stall a sandwich board has to stand to bring it trade, in metres from its edge.</summary>
    public const double BoardReach = 8;

    /// <summary>How many more people a stall with a sandwich board tempts: 1.25 is a quarter more.</summary>
    public const double BoardPull = 1.25;

    /// <summary>How near a finger post people have to be for it to show them the way, in metres.</summary>
    public const double PostReach = 12;

    /// <summary>How near a site map board people have to be for it to show them the way, in metres.</summary>
    public const double MapReach = 20;

    /// <summary>How much of a walk still puts off somebody who has been shown the way.</summary>
    public const double ShownWalk = 0.6;

    /// <summary>The line signs go under in the accounts.</summary>
    public const string Line = "Signs";

    public static IReadOnlyList<SignInfo> Everything => All;

    public static SignInfo Of(SignKind kind) => All[(int)kind];

    /// <summary>
    /// Tidies what a player typed for a sign: no more lines than it has, each cut to its length,
    /// with nothing but letters, numbers, spaces and everyday marks.
    /// </summary>
    public static List<string> Clean(SignKind kind, IEnumerable<string?>? lines)
    {
        var info = Of(kind);
        var clean = new List<string>();
        foreach (var line in (lines ?? Enumerable.Empty<string?>()).Take(info.Lines))
        {
            var kept = new string((line ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || "£&!?.,'-:+/%()#*".Contains(c)).ToArray()).Trim();
            clean.Add(kept.Length > info.LineLength ? kept[..info.LineLength].TrimEnd() : kept);
        }

        while (clean.Count > 0 && clean[^1].Length == 0)
        {
            clean.RemoveAt(clean.Count - 1);
        }

        return clean;
    }
}

public sealed partial class SiteLayout
{
    private readonly List<PlacedSign> _signs = new();

    /// <summary>The field the site is laid out in, in tiles.</summary>
    public TileRect FieldRect => _map.Field;

    /// <summary>Raised when the words, colour or fingers of a sign change. Nothing has moved, so nobody needs a new route.</summary>
    public event Action? SignWritten;

    /// <summary>The signs that have been put up.</summary>
    public IReadOnlyList<PlacedSign> Signs => _signs;

    public PlacedSign? SignAt(TilePoint p) => _signs.FirstOrDefault(s => s.Rect.Contains(p));

    /// <summary>Whether a sign can go with its corner on a tile, and what it costs.</summary>
    public BuildCheck CheckSign(SignKind kind, int x, int y, Facing facing)
    {
        var sign = new PlacedSign(kind, x, y, facing);
        var solid = sign.Solid().ToHashSet();
        foreach (var p in TilesOf(sign.Rect))
        {
            if (!_map.InBounds(p.X, p.Y) || !_map.Rented(p) || !IsBuildable(p))
            {
                return BuildCheck.No("It must stand on the grass inside the hedge.");
            }

            // A banner can hang over a path or a queue, but its poles need clear ground, and no two signs overlap.
            if (SignAt(p) is not null || (solid.Contains(p) ? !IsEmpty(p) : StageAt(p) is not null || StallAt(p) is not null || ScreenAt(p) is not null || ComfortAt(p) is not null))
            {
                return BuildCheck.No("Something is already built there.");
            }
        }

        return new BuildCheck(true, Site.Signs.Of(kind).Cost, null);
    }

    /// <summary>Puts a sign up with the words it comes with. The player writes their own on it afterwards.</summary>
    public BuildCheck AddSign(SignKind kind, int x, int y, Facing facing)
    {
        var check = CheckSign(kind, x, y, facing);
        if (check.Ok)
        {
            // A finger post comes with its fingers pointing right, left, right.
            _signs.Add(new PlacedSign(kind, x, y, facing, Site.Signs.Of(kind).Words.ToList(), kind == SignKind.FingerPost ? 3 : kind == SignKind.Banner ? 1 : 0, kind == SignKind.FingerPost ? 0b101 : 0));
            Refresh();
        }

        return check;
    }

    /// <summary>Writes new words on the sign standing on a tile, and paints and points it. It costs nothing. False if no sign is there.</summary>
    public bool WriteSign(TilePoint at, IEnumerable<string?>? lines, int colour, int points)
    {
        var i = _signs.FindIndex(s => s.Rect.Contains(at));
        if (i < 0)
        {
            return false;
        }

        var sign = _signs[i];
        _signs[i] = sign with { Lines = Site.Signs.Clean(sign.Kind, lines), Colour = Math.Clamp(colour, 0, Site.Signs.Colours - 1), Points = points & 0b111 };
        SignWritten?.Invoke();
        return true;
    }

    /// <summary>What taking down the sign on a tile would give back, or 0 if there is none.</summary>
    public int SignRefundAt(TilePoint p) => SignAt(p) is { } sign ? Site.Signs.Of(sign.Kind).Cost : 0;

    private bool RemoveSignAt(TilePoint p) => SignAt(p) is { } sign && _signs.Remove(sign);

    private IEnumerable<TilePoint> SignsInTheWay => _signs.SelectMany(s => s.Solid());

    private void RestoreSigns(SiteSave save)
    {
        foreach (var s in save.Signs ?? new())
        {
            if (Enum.IsDefined(s.Kind) && CheckSign(s.Kind, s.X, s.Y, s.Facing).Ok)
            {
                _signs.Add(s with { Lines = Site.Signs.Clean(s.Kind, s.Lines), Colour = Math.Clamp(s.Colour, 0, Site.Signs.Colours - 1), Points = s.Points & 0b111 });
            }
        }
    }

    /// <summary>True if a sandwich board stands near enough to a stall to bring it passing trade.</summary>
    public bool Advertised(PlacedStall stall)
    {
        if (_signs.Count == 0)
        {
            return false;
        }

        var r = stall.Rect;
        var reach = Site.Signs.BoardReach / FieldMap.TileMetres;
        foreach (var s in _signs)
        {
            if (s.Kind == SignKind.SandwichBoard)
            {
                var dx = Math.Max(0, Math.Max(r.X - s.X, s.X - (r.Right - 1)));
                var dy = Math.Max(0, Math.Max(r.Y - s.Y, s.Y - (r.Bottom - 1)));
                if ((dx * dx) + (dy * dy) <= reach * reach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>True if a place, in metres, is near enough to a finger post or a site map board to be shown the way.</summary>
    public bool WayShown(double x, double y)
    {
        foreach (var s in _signs)
        {
            var reach = s.Kind == SignKind.FingerPost ? Site.Signs.PostReach : s.Kind == SignKind.MapBoard ? Site.Signs.MapReach : 0;
            if (reach > 0)
            {
                var (mx, my) = s.Middle;
                if (((mx - x) * (mx - x)) + ((my - y) * (my - y)) <= reach * reach)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
