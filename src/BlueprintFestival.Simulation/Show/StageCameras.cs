using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation.Show;

/// <summary>The two stage cameras: one out front looking at the band, one at the back of the stage looking at the crowd.</summary>
public enum StageShot
{
    /// <summary>A camera on a riser out in the crowd, looking back at the band.</summary>
    Front,

    /// <summary>A camera at the back of the stage, over the band's shoulders, looking out at the crowd.</summary>
    Back,
}

/// <summary>
/// Where a stage camera stands and what it looks at, in metres: X and Y across the field as
/// everywhere else, and Height above the ground.
/// </summary>
public readonly record struct CameraShot(double EyeX, double EyeY, double EyeHeight, double LookX, double LookY, double LookHeight, double FieldOfView);

/// <summary>Picks the stage to film and works out where each stage camera goes on it.</summary>
public static class StageCameras
{
    /// <summary>The main stage is the hired stage made bigger by this much every way.</summary>
    public const double MainStageScale = 20.0 / 12.0;

    /// <summary>
    /// The stage to film: the one with an act on, the main stage first if both are playing; between
    /// sets the main stage; and with no music booked, the first stage that was put up. Null with no stage.
    /// </summary>
    public static PlacedStage? Pick(FestivalGame game) => Pick(game, game.Now);

    /// <summary>The stage to film at a given time of day.</summary>
    public static PlacedStage? Pick(FestivalGame game, DateTime now)
    {
        if (game.Show.Stage is { } main && game.Show.Playing(now) is not null)
        {
            return main;
        }

        if (game.SecondShow.Stage is { } second && game.SecondShow.Playing(now) is not null)
        {
            return second;
        }

        return game.Show.Stage ?? game.SecondShow.Stage ?? (game.Site.Stages.Count > 0 ? game.Site.Stages[0] : null);
    }

    /// <summary>Every stage on the site, the main stage first, in the order the cameras go round them.</summary>
    public static IReadOnlyList<PlacedStage> All(FestivalGame game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return RunningOrder.InOrder(game.Site);
    }

    /// <summary>
    /// The stage to film when the player has picked one: that stage while it is still up, otherwise
    /// the usual choice (see <see cref="Pick(FestivalGame)"/>).
    /// </summary>
    public static PlacedStage? Pick(FestivalGame game, PlacedStage? chosen)
    {
        ArgumentNullException.ThrowIfNull(game);
        return chosen is not null && game.Site.Stages.Contains(chosen) ? chosen : Pick(game);
    }

    /// <summary>The next stage round from one, back to the first after the last; null with no stage.</summary>
    public static PlacedStage? After(FestivalGame game, PlacedStage? current)
    {
        var all = All(game);
        if (all.Count == 0)
        {
            return null;
        }

        var at = current is null ? -1 : IndexOf(all, current);
        return all[(at + 1) % all.Count];
    }

    /// <summary>What the player calls a stage: Stage 1 is the main stage, Stage 2 the next, and so on.</summary>
    public static string Name(FestivalGame game, PlacedStage stage)
    {
        var at = IndexOf(All(game), stage);
        return at < 0 ? "Stage" : $"Stage {at + 1}";
    }

    private static int IndexOf(IReadOnlyList<PlacedStage> all, PlacedStage stage)
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i] == stage)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Where the camera goes for a shot of a stage.</summary>
    public static CameraShot Of(PlacedStage stage, StageShot shot)
    {
        // Distances along the stage's facing from the middle of its ground (out towards the crowd is
        // positive), heights above the ground, for each kind of stage. They follow the stage models:
        // the band stands round the middle of the deck and the backdrop is at the back edge.
        var (eye, eyeUp, look, lookUp, fov) = (stage.Kind, shot) switch
        {
            (StageKind.FarmTrailer, StageShot.Front) => (7.0, 3.0, -0.1, 1.8, 40.0),
            (StageKind.FarmTrailer, _) => (-1.1, 3.0, 16.0, 0.6, 62.0),
            (StageKind.Bandstand, StageShot.Front) => (8.0, 2.9, -0.4, 1.4, 40.0),
            (StageKind.Bandstand, _) => (-2.2, 2.5, 16.0, 0.5, 62.0),
            (_, StageShot.Front) => (10.0, 3.0, 0.0, 2.1, 40.0),
            _ => (-2.3, 3.6, 18.0, 0.4, 62.0),
        };

        if (stage.Kind == StageKind.MainStage)
        {
            eye *= MainStageScale;
            eyeUp *= MainStageScale;
            look *= MainStageScale;
            lookUp *= MainStageScale;
        }

        var r = stage.Rect;
        var midX = (r.X + (r.Width / 2.0)) * FieldMap.TileMetres;
        var midY = (r.Y + (r.Height / 2.0)) * FieldMap.TileMetres;
        var (_, _, dx, dy) = SiteLayout.Front(r, stage.Facing);
        return new CameraShot(midX + (dx * eye), midY + (dy * eye), eyeUp, midX + (dx * look), midY + (dy * look), lookUp, fov);
    }
}
