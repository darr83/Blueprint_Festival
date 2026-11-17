using System.Collections.Generic;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Draws the staff in their uniforms, gliding to where the rules say they are, and a marker over
/// anything still waiting for someone to come: a green cross for first aid, an amber sign for trouble.
/// The entertainers stand on their pitches with their instruments and play along to their music.
/// </summary>
public partial class StaffView : Node3D
{
    private readonly Dictionary<int, Node3D> _views = new();
    private readonly Dictionary<int, MeshInstance3D> _markers = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _gone = new();
    private readonly Dictionary<int, (BandMember Member, StageBeat Beat)> _buskers = new();
    private FestivalGame _game = null!;
    private double _time;

    /// <summary>The entertainers' music, which they play along to. Without it they keep a steady beat.</summary>
    public BuskerMusic? Buskers { get; set; }

    /// <summary>How many members of staff are drawn.</summary>
    public int Count => _views.Count;

    /// <summary>How many markers are showing.</summary>
    public int MarkerCount => _markers.Count;

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Sync(0, snap: true);
    }

    public override void _Process(double delta)
    {
        if (_game.SkippingTo is not null)
        {
            // Like the crowd, the staff stand still while skipping to the next act and jump on at the end.
            _frozen = true;
            return;
        }

        if (_frozen)
        {
            _frozen = false;
            Snap();
            return;
        }

        Sync(delta, snap: false);
    }

    private bool _frozen;

    /// <summary>Puts everyone exactly where the rules say, with no gliding.</summary>
    public void Snap() => Sync(0, snap: true);

    private void Sync(double delta, bool snap)
    {
        var speed = _game.Phase == Phase.Day ? (int)_game.Clock.Speed : 0;
        var glide = snap ? 1f : Mathf.Min(1f, (float)delta * (6f + (2f * speed)));
        _time += speed > 0 ? delta : 0;
        _seen.Clear();
        foreach (var member in _game.Staff)
        {
            _seen.Add(member.Id);
            var target = new Vector3((float)member.X, 0, (float)member.Y);
            if (Busking.IsEntertainer(member.Kind))
            {
                Entertain(member, target, delta, speed);
                continue;
            }

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
            var mopping = member.State == StaffState.Mopping;
            person.Pose = mopping ? Pose.Performing : member.IsWalking && speed > 0 ? Pose.Walking : Pose.Standing;
            if (member.Kind == Simulation.Site.StaffKind.Cleaner)
            {
                Mop(view, person, member, mopping);
            }

            // A cleaner picking something up leans down to it.
            var lean = member.State == StaffState.Picking ? 0.38f : 0f;
            person.Rotation = new Vector3(snap ? lean : Mathf.Lerp(person.Rotation.X, lean, Mathf.Min(1f, (float)delta * 8f)), 0, 0);
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
        if (_buskers.Count > 0)
        {
            _gone.Clear();
            foreach (var id in _buskers.Keys)
            {
                if (!_seen.Contains(id))
                {
                    _gone.Add(id);
                }
            }

            foreach (var id in _gone)
            {
                _buskers.Remove(id);
            }
        }

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

    /// <summary>
    /// A cleaner mopping up: the litter picker and sack are swapped for a mop in both hands, pushed
    /// back and forth over the mess, with the yellow bucket and caution board stood beside it.
    /// </summary>
    private void Mop(Node3D view, PersonView person, StaffMember member, bool mopping)
    {
        var kit = view.GetNodeOrNull<Node3D>("Mopping");
        if (!mopping)
        {
            if (kit is { Visible: true })
            {
                kit.Visible = false;
                view.GetNode<Node3D>("Mop bucket").Visible = false;
                view.GetNode<Node3D>("Uniform").Visible = true;
            }

            return;
        }

        var h = person.StandingHeight / 1.6f;
        if (kit is null)
        {
            kit = SiteProps.MoppingKit(h);
            view.AddChild(kit);
            var bucket = SiteProps.MopBucket();
            bucket.Position = new Vector3(-1.1f, 0, 0.5f);
            bucket.RotationDegrees = new Vector3(0, 25, 0);
            view.AddChild(bucket);
        }

        kit.Visible = true;
        view.GetNode<Node3D>("Mop bucket").Visible = true;
        view.GetNode<Node3D>("Uniform").Visible = false;

        // Pushing and pulling: the mop and the hands on it slide forward and back together.
        var push = Mathf.Sin((float)_time * 4.2f + member.Id) * 0.12f;
        kit.Position = new Vector3(0, 0, push);
        var (left, right) = SiteProps.MopHands(h);
        person.Perform(left + new Vector3(0, 0, push), right + new Vector3(0, 0, push), 0, new Vector3(10 + (push * 40), 0, 0));
    }

    /// <summary>An entertainer on their pitch: idle between sets, playing along to their music during one.</summary>
    private void Entertain(StaffMember member, Vector3 at, double delta, int speed)
    {
        if (!_views.TryGetValue(member.Id, out var view))
        {
            view = SiteProps.Entertainer(member.Kind, member.Id);
            view.Position = at;
            view.Rotation = new Vector3(0, (float)member.PostHeading, 0);
            _views[member.Id] = view;
            AddChild(view);
            var performer = view.GetNode<Node3D>("Performer");
            _buskers[member.Id] = (new BandMember(performer, Busking.RoleOf(member.Kind), Vector3.Zero, member.Id % 5), new StageBeat());
        }

        var (band, beat) = _buskers[member.Id];
        var held = speed <= 0;
        var step = held ? 0 : delta;
        var music = Buskers?.For(member.Id);
        beat.Busk(_game.IsBusking(member), Busking.GenreOf(member.Kind), music?.Beat, music?.SongSeconds ?? 0, music?.SongPlaying ?? false, music?.BetweenSongs ?? false, held, step);
        BandMoves.Animate(band, beat, _time, step);
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
