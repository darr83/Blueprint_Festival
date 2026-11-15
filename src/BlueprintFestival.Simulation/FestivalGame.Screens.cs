using BlueprintFestival.Simulation.Site;

namespace BlueprintFestival.Simulation;

/// <summary>Big screens beside and in front of the stages, and who they help.</summary>
public sealed partial class FestivalGame
{
    /// <summary>Whether a big screen could go with its corner on a tile, counting the money.</summary>
    public BuildCheck CheckScreen(ScreenKind kind, int x, int y, Facing facing) => Afford(Site.CheckScreen(kind, x, y, facing));

    /// <summary>Puts up a big screen and pays its hire for the day.</summary>
    public BuildCheck BuildScreen(ScreenKind kind, int x, int y, Facing facing)
    {
        var check = CheckScreen(kind, x, y, facing);
        if (check.Ok)
        {
            Spend(BigScreens.Line, check.Cost);
            Site.AddScreen(kind, x, y, facing);
        }

        return check;
    }

    /// <summary>How many people are watching an act from too far back to get all of it, with no screen to help them.</summary>
    public int WatchingTooFar => Crowd.Parties
        .Where(p => p.State == People.PartyState.Watching && Crowd.FromTheStage(p) is > BigScreens.ClearMetres && !Site.OnScreen(p.X, p.Y))
        .Sum(p => p.Members.Count);

    /// <summary>How many people are watching an act on a big screen from too far back to see the stage well.</summary>
    public int WatchingOnScreen => Crowd.Parties
        .Where(p => p.State == People.PartyState.Watching && Crowd.FromTheStage(p) is > BigScreens.ClearMetres && Site.OnScreen(p.X, p.Y))
        .Sum(p => p.Members.Count);
}
