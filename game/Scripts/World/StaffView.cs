using System.Collections.Generic;
using BlueprintFestival.Simulation;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the staff in their uniforms, gliding to where the rules say they are, and a marker over
/// anything still waiting for someone to come: a green cross for first aid, an amber sign for trouble.
/// </summary>
public partial class StaffView : Node3D
{
    private readonly Dictionary<int, Node3D> _views = new();
    private readonly Dictionary<int, MeshInstance3D> _markers = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _gone = new();
    private FestivalGame _game = null!;

    /// <summary>How many members of staff are drawn.</summary>
    public int Count => _views.Count;

    /// <summary>How many markers are showing.</summary>
    public int MarkerCount => _markers.Count;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Sync(0, snap: true);
    }

    public override void _Process(double delta) => Sync(delta, snap: false);

    /// <summary>Puts everyone exactly where the rules say, with no gliding.</summary>
    public void Snap() => Sync(0, snap: true);

    private void Sync(double delta, bool snap)
    {
        var speed = _game.Phase == Phase.Day ? (int)_game.Clock.Speed : 0;
        var glide = snap ? 1f : Mathf.Min(1f, (float)delta * (6f + (2f * speed)));
        _seen.Clear();
        foreach (var member in _game.Staff)
        {
            _seen.Add(member.Id);
            var target = new Vector3((float)member.X, 0, (float)member.Y);
            if (!_views.TryGetValue(member.Id, out var view))
            {
                view = SiteProps.Staff(member.Kind, member.Id);
                view.Position = target;
                view.Rotation = new Vector3(0, (float)member.Heading, 0);
                _views[member.Id] = view;
                AddChild(view);
            }
            else
            {
                view.Position = view.Position.Lerp(target, glide);
                view.Rotation = new Vector3(0, Mathf.LerpAngle(view.Rotation.Y, (float)member.Heading, glide), 0);
            }

            var person = view.GetChild<PersonView>(0);
            person.Pose = member.IsWalking && speed > 0 ? Pose.Walking : Pose.Standing;
            if (snap)
            {
                person.SetPose(person.Pose, (member.Id % 8) / 8f);
            }
            else
            {
                person.Animate(delta * Mathf.Max(1, speed), 1.75f);
            }
        }

        Sweep(_views);

        var cameraYaw = GetViewport()?.GetCamera3D() is { } camera ? camera.GlobalRotation.Y : 0f;
        _seen.Clear();
        foreach (var incident in _game.Incidents)
        {
            // Someone ill is marked until a first-aider reaches them; trouble is marked until it is sorted out.
            if (incident.Resting || (incident.Kind == IncidentKind.Ill && Arrived(incident)))
            {
                continue;
            }

            _seen.Add(incident.Id);
            if (!_markers.TryGetValue(incident.Id, out var marker))
            {
                marker = incident.Kind == IncidentKind.Ill ? SiteProps.HelpMarker(0.5f) : SiteProps.TroubleMarker(1.6f);
                _markers[incident.Id] = marker;
                AddChild(marker);
            }

            marker.Position = new Vector3((float)incident.X, 0, (float)incident.Y);
            marker.Rotation = new Vector3(0, cameraYaw, 0);
        }

        Sweep(_markers);
    }

    private bool Arrived(Incident incident)
    {
        foreach (var member in _game.Staff)
        {
            if (member.Id == incident.Staff)
            {
                return member.State != StaffState.Going;
            }
        }

        return false;
    }

    private void Sweep<T>(Dictionary<int, T> nodes)
        where T : Node
    {
        if (_seen.Count == nodes.Count)
        {
            return;
        }

        _gone.Clear();
        foreach (var id in nodes.Keys)
        {
            if (!_seen.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            nodes[id].QueueFree();
            nodes.Remove(id);
        }
    }
}
