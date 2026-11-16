using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;

namespace BlueprintFestival.Simulation;

/// <summary>How one of the camping field's checks stands: met, worth a look, or not met.</summary>
public enum CampCheckState
{
    Met,
    Short,
    NotMet,
}

/// <summary>One line of the camping field's checks, as the Camping tab shows it.</summary>
public sealed record CampCheck(CampCheckState State, string Text);

/// <summary>
/// Laying out the camping field: marking ground for quiet, family and accessible camping and for
/// fire lanes, and the checks that say what the camp still needs. The fire officer looks round
/// as the gates open, and fines the festival if any tent is out of reach of a fire lane.
/// </summary>
public sealed partial class FestivalGame
{
    /// <summary>Whether the box between two tiles can be marked for a kind of camping, or cleared with <see cref="CampZone.General"/>.</summary>
    public BuildCheck CheckCampZone(TilePoint from, TilePoint to, CampZone zone)
    {
        // The dearer ways to camp are set up and sold before the gates open, so their ground is fixed by then.
        if (Phase == Phase.Day && (CampLayout.IsExtra(zone) || Site.CampZones.Any(z => CampLayout.IsExtra(z.Value) && InBox(z.Key, from, to))))
        {
            return BuildCheck.No("Ready-pitched, campervan and glamping ground is set up before the gates open.");
        }

        var check = Site.CheckCampZone(from, to, zone);
        return check.Ok && !CanBuild ? BuildCheck.No("Nothing can be built just now.") : check;
    }

    private static bool InBox(TilePoint p, TilePoint a, TilePoint b) =>
        p.X >= Math.Min(a.X, b.X) && p.X <= Math.Max(a.X, b.X) && p.Y >= Math.Min(a.Y, b.Y) && p.Y <= Math.Max(a.Y, b.Y);

    /// <summary>Marks camping ground for a kind of camping or a fire lane (or clears it). It costs nothing.</summary>
    public BuildCheck MarkCamp(TilePoint from, TilePoint to, CampZone zone)
    {
        var check = CheckCampZone(from, to, zone);
        return check.Ok ? Site.SetCampZone(from, to, zone) : check;
    }

    /// <summary>True if there is camping ground on this year's map to lay out.</summary>
    public bool HasCampingGround => Field.CampFields.Any();

    /// <summary>
    /// The camping field's checks, worked out from who would pitch where today: fire lanes, toilets
    /// and water near every tent, enough showers, and the right kind of pitch for families and for
    /// campers who need an accessible one. Empty when no camping is booked.
    /// </summary>
    public IReadOnlyList<CampCheck> CampChecks()
    {
        var checks = new List<CampCheck>();
        if (!CampingBooked || !HasCampingGround)
        {
            return checks;
        }

        var plan = Crowd.PlanPitches();
        if (plan.Count == 0)
        {
            checks.Add(new CampCheck(CampCheckState.Met, "Nobody has a tent to pitch yet."));
            return checks;
        }

        var tents = plan.Count;
        var campers = plan.Sum(p => p.Party.Members.Count);
        var far = TentsOutOfFireReach(plan.Select(p => p.Pitch));
        checks.Add(far == 0
            ? new CampCheck(CampCheckState.Met, $"Every tent within {CampLayout.FireLaneReach:0} m of a fire lane")
            : new CampCheck(CampCheckState.NotMet, $"{far:N0} tents more than {CampLayout.FireLaneReach:0} m from a fire lane: the fire officer will fine you £{CampLayout.FireFine:N0}"));

        var noLoo = plan.Count(p => Crowd.Nearest(p.Pitch, StallKind.Toilets, StallKind.ToiletBlock) > CampLayout.FacilitiesReach);
        checks.Add(noLoo == 0
            ? new CampCheck(CampCheckState.Met, $"Toilets within {CampLayout.FacilitiesReach:0} m of every tent")
            : new CampCheck(CampCheckState.NotMet, $"{noLoo:N0} tents with no toilets within {CampLayout.FacilitiesReach:0} m"));

        var noTap = plan.Count(p => Crowd.Nearest(p.Pitch, StallKind.WaterPoint) > CampLayout.FacilitiesReach);
        checks.Add(noTap == 0
            ? new CampCheck(CampCheckState.Met, $"Water within {CampLayout.FacilitiesReach:0} m of every tent")
            : new CampCheck(CampCheckState.NotMet, $"{noTap:N0} tents with no water point within {CampLayout.FacilitiesReach:0} m"));

        var showers = Site.Stalls.Count(s => s.Kind == StallKind.ShowerBlock) * Catalogue.Of(StallKind.ShowerBlock).Servers;
        var aim = (int)Math.Ceiling(campers / (double)CampLayout.CampersAShower);
        checks.Add(new CampCheck(showers >= aim ? CampCheckState.Met : showers == 0 ? CampCheckState.NotMet : CampCheckState.Short,
            $"Showers: {showers} for about {campers:N0} campers (aim for {aim})"));

        foreach (var zone in new[] { CampZone.Family, CampZone.Accessible, CampZone.Quiet })
        {
            var want = plan.Count(p => Crowd.WishOf(p.Party) == zone);
            if (want == 0)
            {
                continue;
            }

            var got = plan.Count(p => Crowd.WishOf(p.Party) == zone && Site.CampZoneAt(p.Pitch) == zone);
            var who = zone switch { CampZone.Family => "families", CampZone.Accessible => "campers who need an accessible pitch", _ => "campers who want it quiet" };
            var state = got >= want ? CampCheckState.Met : zone == CampZone.Accessible ? CampCheckState.NotMet : CampCheckState.Short;
            checks.Add(new CampCheck(state, $"{CampLayout.Name(zone)}: {got:N0} of {want:N0} {who}"));
        }

        checks.AddRange(ExtraChecks(plan));
        checks.Add(new CampCheck(CampCheckState.Met, $"{tents:N0} tents on {Crowd.OpenPitches().Count:N0} pitches"));
        return checks;
    }

    /// <summary>How many of these pitches are further than the fire officer allows from a fire lane or the hedge.</summary>
    private int TentsOutOfFireReach(IEnumerable<TilePoint> pitches)
    {
        var reach = Site.FireReach();
        var most = (int)Math.Floor(CampLayout.FireLaneReach / FieldMap.TileMetres);
        return pitches.Count(p => !reach.TryGetValue(p, out var d) || d > most);
    }

    /// <summary>
    /// As the day starts the fire officer walks the camping field. Every tent must be within reach
    /// of a fire lane (or the hedge), or the festival is fined.
    /// </summary>
    private void FireOfficerCalls()
    {
        var far = TentsOutOfFireReach(Crowd.Parties.Where(p => p.Pitch is not null).Select(p => p.Pitch!.Value));
        if (far == 0)
        {
            Note(DayNoteKind.Camp, "Fire officer walked the camp", "Every tent in reach of a fire lane", DayTone.Good);
            return;
        }

        Spend("Fire officer's fine", CampLayout.FireFine);
        Note(DayNoteKind.Camp, "Fire officer walked the camp", $"{far:N0} tents out of reach of a fire lane: fined £{CampLayout.FireFine:N0}", DayTone.Bad);
    }
}
