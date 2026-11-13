using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Draws what the player has built: stages, fences and gates. Redrawn whenever the layout changes.</summary>
public partial class SiteView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    private FestivalGame _game = null!;
    private bool _stale;

    /// <summary>How many stages are drawn.</summary>
    public int StageCount { get; private set; }

    /// <summary>How many gates are drawn.</summary>
    public int GateCount { get; private set; }

    /// <summary>How many straight runs of fence are drawn.</summary>
    public int FenceRunCount { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        game.Site.Changed += MarkStale;
        Rebuild();
    }

    public override void _ExitTree() => _game.Site.Changed -= MarkStale;

    private void MarkStale() => _stale = true;

    public override void _Process(double delta)
    {
        if (_stale)
        {
            Rebuild();
        }
    }

    /// <summary>The turn, in degrees, that points a model built facing south the way it should face.</summary>
    public static float Turn(Facing facing) => facing switch { Facing.West => -90f, Facing.North => 180f, Facing.East => 90f, _ => 0f };

    /// <summary>The middle of a rectangle of tiles, in metres, on the ground.</summary>
    public static Vector3 Middle(TileRect r) => new((r.X + (r.Width / 2f)) * T, 0, (r.Y + (r.Height / 2f)) * T);

    /// <summary>The turn for a gate: its front faces the side people queue on.</summary>
    public static float Turn(PlacedGate gate, GateUse use)
    {
        if (use.InUse)
        {
            return use.OutX > 0 ? 90f : use.OutX < 0 ? -90f : use.OutY < 0 ? 180f : 0f;
        }

        return gate.Vertical ? 90f : 0f;
    }

    /// <summary>Adds a run of fence to a mesh: from the outer edge of its first tile to that of its last.</summary>
    public static void AddRun(MeshBuilder builder, FenceRun run)
    {
        var from = new Vector3((run.X + (run.Vertical ? 0.5f : 0f)) * T, 0, (run.Y + (run.Vertical ? 0f : 0.5f)) * T);
        var to = from + (run.Vertical ? new Vector3(0, 0, run.Length * T) : new Vector3(run.Length * T, 0, 0));
        SiteProps.AddFence(builder, run.Kind, from, to);
    }

    public void Rebuild()
    {
        _stale = false;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var site = _game.Site;
        foreach (var stage in site.Stages)
        {
            var node = SiteProps.Stage(stage.Kind, _game.Name);
            node.Position = Middle(stage.Rect);
            node.RotationDegrees = new Vector3(0, Turn(stage.Facing), 0);
            AddChild(node);
        }

        for (var i = 0; i < site.Gates.Count; i++)
        {
            var node = SiteProps.Gate(site.Gates[i].Kind, _game.Name);
            node.Position = Middle(site.Gates[i].Rect);
            node.RotationDegrees = new Vector3(0, Turn(site.Gates[i], site.GateUses[i]), 0);
            AddChild(node);
        }

        var runs = site.FenceRuns();
        if (runs.Count > 0)
        {
            var builder = new MeshBuilder();
            foreach (var run in runs)
            {
                AddRun(builder, run);
            }

            AddChild(builder.BuildNode("Fences"));
        }

        StageCount = site.Stages.Count;
        GateCount = site.Gates.Count;
        FenceRunCount = runs.Count;
    }
}
