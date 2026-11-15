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
