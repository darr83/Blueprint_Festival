using System;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Putting up stands: pick a seated stand, terrace or deck, set its rows and roof, then drag its
/// front along the ground. It faces the nearest stage; R turns it round. Click its rail to open or
/// close a set of steps, and inside it to say who may use it.
/// </summary>
public partial class BuildTool
{
    /// <summary>The kind of stand being put up.</summary>
    public StandKind StandKind { get; private set; }

    /// <summary>How many rows (for a deck, metres deep) the next stand has.</summary>
    public int StandRows { get; private set; } = 4;

    /// <summary>How much roof the next stand has.</summary>
    public StandRoof StandRoof { get; private set; }

    /// <summary>True when the next stand is turned round, to face away from the nearest stage.</summary>
    public bool StandTurned { get; private set; }

    /// <summary>The stand picked to change, or -1.</summary>
    public int SelectedStand { get; private set; } = -1;

    /// <summary>Raised when a stand is picked or put down, or the settings for the next one change.</summary>
    public event Action? StandPicked;

    public void PickStand(StandKind kind) => Pick(BuildMode.Stand, () =>
    {
        StandKind = kind;
        StandRows = Stands.Tiered(kind) ? Math.Max(StandRows, 2) : Math.Clamp(StandRows, 2, 6);
        StandPicked?.Invoke();
    });

    /// <summary>Sets how many rows the next stand has, between one and ten.</summary>
    public void SetStandRows(int rows)
    {
        StandRows = Math.Clamp(rows, Stands.FewestRows, Stands.MostRows);
        StandPicked?.Invoke();
    }

    /// <summary>Sets how much roof the next stand has.</summary>
    public void SetStandRoof(StandRoof roof)
    {
        StandRoof = roof;
        StandPicked?.Invoke();
    }

    /// <summary>Picks a stand to change, or none.</summary>
    public void SelectStand(int stand)
    {
        SelectedStand = stand;
        StandPicked?.Invoke();
    }

    private static string StandHint(StandKind kind) =>
        $"Hold the mouse button and drag along the front of the {Stands.Name(kind).ToLowerInvariant()}. It faces the nearest stage; R turns it round. Click its rail for steps, or inside it to say who may use it.";

    /// <summary>Puts up a stand along the line between two tiles, and picks it.</summary>
    public BuildCheck DragStand(TilePoint from, TilePoint to)
    {
        var result = _game.BuildStand(StandKind, from, to, StandRows, StandRoof, StandTurned);
        Refused = !result.Ok;
        SoundBoard.Cue(result.Ok ? Sounds.BuildFence : Sounds.Refuse);
        Status = !result.Ok ? result.Problem ?? "That cannot go there." : $"{Stands.Name(StandKind)} up. Post security by the steps to keep it to its places.";
        if (result.Ok)
        {
            SelectStand(_game.Site.Stands.Count - 1);
        }

        return result;
    }

    /// <summary>A click with the stand tool: on a stand's rail it opens or closes steps, inside it picks the stand.</summary>
    public void ClickStand(TilePoint at)
    {
        var site = _game.Site;
        var stand = site.StandAt(at);
        if (stand < 0)
        {
            SelectStand(-1);
            Status = StandHint(StandKind);
            return;
        }

        if (site.Stands[stand].IsEdge(at))
        {
            var problem = _game.ToggleStandSteps(at);
            Refused = problem is not null;
            SoundBoard.Cue(problem is null ? Sounds.BuildFence : Sounds.Refuse);
            Status = problem ?? (site.IsStandRail(at) ? "Steps closed." : "Steps opened. Post security by them to keep the stand to its places.");
        }

        SelectStand(stand);
    }

    /// <summary>The line being dragged, shown as the ground the stand would cover, with its places and price.</summary>
    private void HoverStand(TilePoint at)
    {
        ClearGhostOnly();
        var site = _game.Site;
        if (_dragFrom is not { } start || start == at)
        {
            var under = site.StandAt(at);
            ShowPad(new TileRect(at.X, at.Y, 1, 1), true);
            Refused = false;
            Status = under < 0 ? StandHint(StandKind)
                : site.Stands[under].IsEdge(at) ? (site.IsStandRail(at) ? "Click to put steps in the rail here." : "Click to close these steps.")
                : $"{Stands.Name(site.Stands[under].Kind)}: click to say who may use it.";
            return;
        }

        var check = _game.CheckStand(StandKind, start, at, StandRows, StandRoof, StandTurned);
        var (rect, facing) = site.StandBox(start, at, StandRows, StandTurned);
        ShowPad(rect, check.Ok);
        Refused = !check.Ok;
        var probe = new PlacedStand(StandKind, rect, facing, StandRoof, new());
        var places = Stands.Tiered(StandKind) ? $"{probe.Places:N0} {(StandKind == StandKind.Seated ? "seats" : "standing")}" : $"{probe.Places:N0} places";
        Status = !check.Ok ? check.Problem ?? ""
            : $"{Stands.Name(StandKind)}: {probe.Length * FieldMap.TileMetres:0.#} m long, {probe.Rows} {(Stands.Tiered(StandKind) ? "rows" : "m deep")}, {places}. £{check.Cost:N0}";
    }
}
