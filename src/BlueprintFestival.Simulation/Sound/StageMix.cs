namespace BlueprintFestival.Simulation.Sound;

/// <summary>
/// How loud each stage's music is, from where the player is looking. Zoomed right out over the
/// site every stage that is playing is heard, all of them faint. Zooming in towards one stage
/// brings its music up and lets the others fall away into the distance, so close to a stage it is
/// the music you hear, with only a hint of the rest.
/// </summary>
public static class StageMix
{
    /// <summary>The closest the site view can zoom: the height of the view in metres.</summary>
    public const double ClosestView = 8;

    /// <summary>The furthest the site view can zoom out.</summary>
    public const double FurthestView = 260;

    /// <summary>How loud the nearest stage is with the view zoomed right out, against right up close.</summary>
    public const double ZoomedOutLevel = 0.45;

    /// <summary>How far a stage carries, in metres, against the height of the view.</summary>
    public const double CarryPerViewMetre = 0.5;

    /// <summary>A stage's music always carries at least this far, in metres, however close the view.</summary>
    public const double LeastCarry = 18;

    /// <summary>A stage that is playing is never quite silent: the faintest it gets.</summary>
    public const double Faintest = 0.03;

    /// <summary>How loud a stage that is not being filmed is while a stage camera is on.</summary>
    public const double OffCamera = 0.06;

    /// <summary>How far the view is zoomed out, from 0 (as close as it goes) to 1 (as far as it goes).</summary>
    public static double ZoomedOut(double viewHeight)
    {
        var size = Math.Clamp(viewHeight, ClosestView, FurthestView);
        return Math.Log(size / ClosestView) / Math.Log(FurthestView / ClosestView);
    }

    /// <summary>
    /// How loud each stage is, from 0 to 1, with the view centred on a point of the field (in metres)
    /// and a given height of view. Stages are given by where their crowds stand, the front of the stage.
    /// </summary>
    public static double[] Levels(double x, double y, double viewHeight, IReadOnlyList<(double X, double Y)> stages)
    {
        var levels = new double[stages.Count];
        var zoom = ZoomedOut(viewHeight);
        // Most of the quietening is in the furthest part of the zoom, so a normal view of a stage is near full.
        var overall = 1 - ((1 - ZoomedOutLevel) * zoom * zoom);
        var carry = Math.Max(LeastCarry, Math.Clamp(viewHeight, ClosestView, FurthestView) * CarryPerViewMetre);
        for (var i = 0; i < stages.Count; i++)
        {
            var dx = stages[i].X - x;
            var dy = stages[i].Y - y;
            var away = Math.Sqrt((dx * dx) + (dy * dy)) / carry;
            levels[i] = Math.Max(Faintest, overall / (1 + (away * away)));
        }

        return levels;
    }

    /// <summary>How far an entertainer in the fields carries, in metres, against the height of the view: much less than a stage.</summary>
    public const double BuskCarryPerViewMetre = 0.18;

    /// <summary>An entertainer always carries at least this far, in metres.</summary>
    public const double BuskLeastCarry = 7;

    /// <summary>
    /// How loud one of the entertainers is, from 0 to 1, with the view centred on a point and a given
    /// height of view. Close in it is as loud as a stage; zoomed out, or looking elsewhere, it fades
    /// to nothing, so the stages are not drowned by every guitar on the site.
    /// </summary>
    public static double Busker(double x, double y, double viewHeight, double bx, double by)
    {
        var zoom = ZoomedOut(viewHeight);
        var overall = 0.85 * (1 - zoom) * (1 - zoom);
        var carry = Math.Max(BuskLeastCarry, Math.Clamp(viewHeight, ClosestView, FurthestView) * BuskCarryPerViewMetre);
        var dx = bx - x;
        var dy = by - y;
        var away = Math.Sqrt((dx * dx) + (dy * dy)) / carry;
        var level = overall / (1 + (away * away * away));
        return level < 0.01 ? 0 : level;
    }

    /// <summary>How far a fairground ride carries, in metres, against the height of the view: a little more than an entertainer.</summary>
    public const double FairCarryPerViewMetre = 0.22;

    /// <summary>A ride always carries at least this far, in metres.</summary>
    public const double FairLeastCarry = 9;

    /// <summary>
    /// How loud a fairground ride (or hook a duck) is, from 0 to 1, with the view centred on a point
    /// and a given height of view. Looking at the fair it is plain to hear; it falls away quickly as
    /// the view moves off it, and zoomed right out over the site it is silent, so the organ and the
    /// squeals are only heard while the fair is on screen.
    /// </summary>
    public static double Fair(double x, double y, double viewHeight, double fx, double fy)
    {
        var zoom = ZoomedOut(viewHeight);
        var overall = (1 - zoom) * (1 - (0.5 * zoom));
        var carry = Math.Max(FairLeastCarry, Math.Clamp(viewHeight, ClosestView, FurthestView) * FairCarryPerViewMetre);
        var dx = fx - x;
        var dy = fy - y;
        var away = Math.Sqrt((dx * dx) + (dy * dy)) / carry;
        var level = overall / (1 + (away * away * away));
        return level < 0.02 ? 0 : level;
    }

    /// <summary>
    /// How loud each stage is while a stage camera films one of them: that stage in full, the others
    /// faint in the distance.
    /// </summary>
    public static double[] Filming(int filmed, int stages)
    {
        var levels = new double[stages];
        for (var i = 0; i < stages; i++)
        {
            levels[i] = i == filmed ? 1 : OffCamera;
        }

        return levels;
    }

    /// <summary>The loudest of the stages, the one whose music is mostly what is heard; -1 with none.</summary>
    public static int Loudest(IReadOnlyList<double> levels)
    {
        var best = -1;
        for (var i = 0; i < levels.Count; i++)
        {
            if (best < 0 || levels[i] > levels[best])
            {
                best = i;
            }
        }

        return best;
    }
}
