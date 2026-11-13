using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws everyone who is on the site. The rules move people once per tick; this glides each figure
/// towards where the rules say they are, turns them to face the way they are going, and swings
/// their arms and legs while they walk.
/// </summary>
public partial class CrowdView : Node3D
{
    private readonly Dictionary<int, PersonView> _views = new();
    private readonly Dictionary<int, (Need Need, MeshInstance3D Node)> _bubbles = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _gone = new();
    private FestivalGame _game = null!;
    private double _time;
    private int _cheerOutOfTen = 3;

    /// <summary>How many thought bubbles are showing.</summary>
    public int BubbleCount => _bubbles.Count;

    /// <summary>How many people are being drawn.</summary>
    public int Count => _views.Count;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Sync(0, snap: true);
    }

    public override void _Process(double delta) => Sync(delta, snap: false);

    /// <summary>Puts every figure exactly where the rules say, with no gliding. Used after a jump in time.</summary>
    public void Snap() => Sync(0, snap: true);

    /// <summary>Shows, changes or takes away the thought bubble over someone, and keeps it turned to the camera.</summary>
    private void Bubble(int id, PersonView view, Need? wants, float cameraYaw)
    {
        if (_bubbles.TryGetValue(id, out var bubble) && bubble.Need != wants)
        {
            bubble.Node.QueueFree();
            _bubbles.Remove(id);
        }

        if (wants is not { } need)
        {
            return;
        }

        if (!_bubbles.TryGetValue(id, out bubble))
        {
            bubble = (need, SiteProps.NeedBubble(need, view.StandingHeight));
            view.AddChild(bubble.Node);
            _bubbles[id] = bubble;
        }

        bubble.Node.Rotation = new Vector3(0, cameraYaw - view.Rotation.Y, 0);
    }

    private void Sync(double delta, bool snap)
    {
        var speed = (int)_game.Clock.Speed;
        var glide = snap ? 1f : Mathf.Min(1f, (float)delta * (6f + (2f * speed)));
        if (speed > 0)
        {
            _time += delta;
        }

        _cheerOutOfTen = _game.Show.Playing(_game.Now) is { Headliner: true } ? 5 : 3;
        var cameraYaw = GetViewport()?.GetCamera3D() is { } camera ? camera.GlobalRotation.Y : 0f;
        _seen.Clear();
        foreach (var person in _game.Crowd.People)
        {
            _seen.Add(person.Id);
            var target = new Vector3((float)person.X, 0, (float)person.Y);
            var heading = (float)person.Heading;
            if (!_views.TryGetValue(person.Id, out var view))
            {
                view = PersonView.Build(person.Look);
                view.Position = target;
                view.Rotation = new Vector3(0, heading, 0);
                _views[person.Id] = view;
                AddChild(view);
            }
            else
            {
                view.Position = view.Position.Lerp(target, glide);
                view.Rotation = new Vector3(0, Mathf.LerpAngle(view.Rotation.Y, heading, glide), 0);
            }

            // People watching the stage put their arms up now and then, each in their own time;
            // more of them, more often, for the headline act.
            var cheering = person.Watching && ((person.Id * 7) + (int)(_time / 2.5)) % 10 < _cheerOutOfTen;
            view.Pose = person.Walking && speed > 0 ? Pose.Walking : cheering ? Pose.Cheering : Pose.Standing;
            Bubble(person.Id, view, person.Wants, cameraYaw);
            if (snap)
            {
                view.SetPose(view.Pose, (person.Id % 8) / 8f);
            }
            else
            {
                view.Animate(delta * Mathf.Max(1, speed));
            }
        }

        if (_seen.Count == _views.Count)
        {
            return;
        }

        _gone.Clear();
        foreach (var id in _views.Keys)
        {
            if (!_seen.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _views[id].QueueFree();
            _views.Remove(id);
            _bubbles.Remove(id);
        }
    }
}
