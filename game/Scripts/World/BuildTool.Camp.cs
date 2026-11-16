using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System.Linq;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Marking out the camping field: drag over camping ground to mark it for quiet, family or
/// accessible camping, or for a fire lane. A drag that starts on ground already marked that way
/// clears it. The marks are tinted on the ground while the Camping group is open.
/// </summary>
public partial class BuildTool
{
    private Node3D? _campMarks;
    private readonly System.Collections.Generic.Dictionary<CampZone, StandardMaterial3D> _campMaterials = new();

    /// <summary>The kind of camping ground being marked.</summary>
    public CampZone CampZone { get; private set; } = CampZone.Quiet;

    /// <summary>True while the marks on the camping ground are tinted.</summary>
    public bool CampMarksShown => _campMarks is { Visible: true };

    /// <summary>How many kinds of mark are drawn on the camping ground.</summary>
    public int CampMarkKinds => _campMarks?.GetChildCount() ?? 0;

    /// <summary>The colour each kind of camping ground is tinted: blue for quiet, lilac for families, gold for accessible, pale for a fire lane.</summary>
    public static Color CampColour(CampZone zone, float alpha) => zone switch
    {
        CampZone.Quiet => new Color(0.24f, 0.49f, 0.69f, alpha),
        CampZone.Family => new Color(0.56f, 0.42f, 0.75f, alpha),
        CampZone.Accessible => new Color(0.88f, 0.63f, 0.12f, alpha),
        CampZone.FireLane => new Color(0.92f, 0.96f, 0.85f, alpha),
        _ => new Color(1, 1, 1, alpha),
    };

    public void PickCampZone(CampZone zone) => Pick(BuildMode.Camp, () => CampZone = zone);

    /// <summary>What a drag starting here marks: the picked kind, or general camping again if it starts on ground already marked that way.</summary>
    private CampZone MarkingZone(TilePoint from) => _game.Site.CampZoneAt(from) == CampZone ? CampZone.General : CampZone;

    /// <summary>Marks the box between two tiles for the picked kind of camping, or clears it if the drag started on ground marked that way.</summary>
    public BuildCheck DragCampZone(TilePoint from, TilePoint to)
    {
        var zone = MarkingZone(from);
        var result = _game.MarkCamp(from, to, zone);
        Refused = !result.Ok;
        SoundBoard.Cue(result.Ok ? zone == CampZone.General ? Sounds.TakeDown : Sounds.BuildTape : Sounds.Refuse);
        Status = !result.Ok ? result.Problem ?? "That cannot be marked."
            : zone == CampZone.General ? "That ground is general camping again."
            : zone == CampZone.FireLane ? "Fire lane marked. It stays clear of tents and stalls." : $"Marked for {CampLayout.Name(zone).ToLowerInvariant()}.";
        return result;
    }

    private void StartCampMarks()
    {
        foreach (var zone in CampLayout.Zones)
        {
            _campMaterials[zone] = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = CampColour(zone, zone == CampZone.FireLane ? 0.7f : 0.45f),
            };
        }

        _game.Site.CampZonesChanged += DrawCampMarks;
        _game.Site.Changed += DrawCampMarks;
        DrawCampMarks();
    }

    private void StopCampMarks()
    {
        _game.Site.CampZonesChanged -= DrawCampMarks;
        _game.Site.Changed -= DrawCampMarks;
    }

    /// <summary>Tints the marked camping ground, one colour for each kind.</summary>
    private void DrawCampMarks()
    {
        _campMarks?.QueueFree();
        _campMarks = new Node3D { Name = "CampMarks" };
        foreach (var zone in CampLayout.Zones)
        {
            var tint = new MeshBuilder();
            var tiles = _game.Site.CampZones.Where(z => z.Value == zone).Select(z => z.Key).ToHashSet();
            foreach (var (x, y, length, vertical) in SiteLayout.RunsOf(tiles))
            {
                var rect = vertical ? new TileRect(x, y, 1, length) : new TileRect(x, y, length, 1);
                tint.Box(SiteView.Middle(rect) + new Vector3(0, 0.045f, 0), new Vector3(rect.Width * T, 0.02f, rect.Height * T), Colors.White);
            }

            if (!tint.IsEmpty)
            {
                var node = tint.BuildNode($"Camp{zone}");
                node.MaterialOverride = _campMaterials[zone];
                node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
                _campMarks.AddChild(node);
            }
        }

        AddChild(_campMarks);
        ShowCampMarks();
    }

    /// <summary>The marks show while marking camping ground, or while putting down something for the camp.</summary>
    private void ShowCampMarks()
    {
        if (_campMarks is not null)
        {
            _campMarks.Visible = Mode == BuildMode.Camp || (Mode == BuildMode.Stall && (Catalogue.GroupOf(Stall) == StallGroup.Camping || Stall is StallKind.WaterPoint or StallKind.ToiletBlock or StallKind.Toilets) && _game.HasCampingGround);
        }
    }
}
