using System.Collections.Generic;
using BlueprintFestival.Simulation;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Draws the puddles where people have been sick, until a cleaner mops each one up.</summary>
public partial class SickView : Node3D
{
    private readonly Dictionary<int, MeshInstance3D> _puddles = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _gone = new();
    private FestivalGame _game = null!;

    /// <summary>How many puddles are drawn.</summary>
    public int Count => _puddles.Count;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Refresh();
    }

    public override void _Process(double delta) => Refresh();

    public void Refresh()
    {
        var sick = _game.Sick;
        if (sick.Count == 0 && _puddles.Count == 0)
        {
            return;
        }

        _seen.Clear();
        foreach (var patch in sick)
        {
            _seen.Add(patch.Id);
            if (_puddles.ContainsKey(patch.Id))
            {
                continue;
            }

            var puddle = SiteProps.SickPuddle(patch.Id + 3, 0.5f + ((patch.Id * 7 % 5) * 0.05f));
            var height = (float)_game.Site.HeightAt(Simulation.World.FieldMap.TileAt(patch.X, patch.Y));
            puddle.Position = new Vector3((float)patch.X, height, (float)patch.Y);
            _puddles[patch.Id] = puddle;
            AddChild(puddle);
        }

        if (_seen.Count == _puddles.Count)
        {
            return;
        }

        _gone.Clear();
        foreach (var id in _puddles.Keys)
        {
            if (!_seen.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _puddles[id].QueueFree();
            _puddles.Remove(id);
        }
    }
}
