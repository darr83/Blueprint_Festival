using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>
/// Stands: seated stands, standing terraces and raised decks the player drags out facing a stage.
/// A steward (security) by the steps keeps each one to its places and takes the seat tickets;
/// with nobody there people squeeze on, and on a crowded stand someone can fall.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>The money line stands are paid for under.</summary>
    public const string StandsLine = "Stands";

    /// <summary>How many people have fallen on crowded stands today.</summary>
    public int StandFalls { get; private set; }

    /// <summary>Whether a stand could be dragged along a line, counting the money.</summary>
    public BuildCheck CheckStand(StandKind kind, TilePoint from, TilePoint to, int rows, StandRoof roof, bool turned = false) =>
        Afford(Site.CheckStand(kind, from, to, rows, roof, turned));

    /// <summary>Puts up a stand along a line and pays for it, if it fits and there is the money.</summary>
    public BuildCheck BuildStand(StandKind kind, TilePoint from, TilePoint to, int rows, StandRoof roof, bool turned = false)
    {
        var check = CheckStand(kind, from, to, rows, roof, turned);
        if (check.Ok)
        {
            Spend(StandsLine, check.Cost);
            Site.AddStand(kind, from, to, rows, roof, turned);
        }

        return check;
    }

    /// <summary>Opens or closes a set of steps in a stand's rail. Only while building. Returns a problem, or null.</summary>
    public string? ToggleStandSteps(TilePoint p) => !CanBuild ? "Nothing can be built just now." : Site.ToggleSteps(p);

    /// <summary>Changes who may go up on a stand. It can be changed at any time, on the day too.</summary>
    public bool SetStandUse(int stand, StandUse use) => Site.SetStandUse(stand, use);

    /// <summary>Changes what a seat ticket for a stand costs.</summary>
    public int? SetStandPrice(int stand, int price) => Site.SetStandPrice(stand, price);

    /// <summary>True if a member of security is at their post by a stand's steps.</summary>
    public bool StandManned(int stand) => StepsManned(stand) > 0;

    /// <summary>How many of a stand's sets of steps have security by them.</summary>
    public int StepsManned(int stand)
    {
        if (stand < 0 || stand >= Site.Stands.Count)
        {
            return 0;
        }

        var manned = 0;
        foreach (var step in Site.Stands[stand].Steps)
        {
            var (sx, sy) = FieldMap.CentreOf(step);
            if (_staff.Any(s => s.Kind == StaffKind.Security && s.State is StaffState.AtPost or StaffState.Holding && Distance(s.X, s.Y, sx, sy) <= GapReach))
            {
                manned++;
            }
        }

        return manned;
    }

    /// <summary>True for security posted by a stand's steps: they stay there rather than walk a beat.</summary>
    private bool OnSteps(StaffMember member)
    {
        foreach (var stand in Site.Stands)
        {
            foreach (var step in stand.Steps)
            {
                var (sx, sy) = FieldMap.CentreOf(step);
                if (Distance(member.PostX, member.PostY, sx, sy) <= GapReach)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void StartStands()
    {
        Crowd.StandManned = StandManned;
        Crowd.SeatTicketsSold += (_, paid) => Earn("Seat tickets", paid);
        Crowd.FellOnStand += party =>
        {
            StandFalls++;
            Raise(new Incident(_nextIncident++, IncidentKind.Ill, party.X, party.Y, party.Id, Now), DayNoteKind.Hurt, "Fell on a crowded stand");
            Crowd.Upset(party.X, party.Y, 6, 0.03);
        };
    }
}
