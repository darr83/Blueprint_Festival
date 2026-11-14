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
    /// <summary>The most people drawn in full, with arms and legs that move. Everyone else is a simple figure.</summary>
    public const int MostInFull = 400;

    /// <summary>How far out the view can be (the camera's size, in metres) before nobody is drawn in full.</summary>
    public const float FullWithin = 75f;

    /// <summary>The most full figures made in one frame, so that zooming in on a big crowd does not stall the game.</summary>
    private const int MadePerFrame = 40;

    private readonly Dictionary<int, PersonView> _views = new();
    private readonly List<Simulation.People.PersonOnSite> _people = new();
    private readonly List<float> _distances = new();
    private MultiMeshInstance3D _bodies = null!;
    private MultiMeshInstance3D _heads = null!;
    private MultiMeshInstance3D _hair = null!;
    private float[] _bodyBuffer = System.Array.Empty<float>();
    private float[] _headBuffer = System.Array.Empty<float>();
    private float[] _hairBuffer = System.Array.Empty<float>();
    private Vector3[] _shown = System.Array.Empty<Vector3>();
    private int[] _shownStamp = System.Array.Empty<int>();
    private Color[] _topColours = System.Array.Empty<Color>();
    private Color[] _skinColours = System.Array.Empty<Color>();
    private Color[] _hairColours = System.Array.Empty<Color>();
    private float[] _heights = System.Array.Empty<float>();
    private int _stamp;
    private readonly Dictionary<int, (Need Need, MeshInstance3D Node)> _bubbles = new();
    private readonly HashSet<int> _seen = new();
    private readonly List<int> _gone = new();
    private FestivalGame _game = null!;
    private double _time;
    private int _cheerOutOfTen = 3;

    /// <summary>How many thought bubbles are showing.</summary>
    public int BubbleCount => _bubbles.Count;

    /// <summary>How many people are being drawn, in full or as simple figures.</summary>
    public int Count => _views.Count + Simple;

    /// <summary>How many people are drawn in full.</summary>
    public int InFull => _views.Count;

    /// <summary>How many people are drawn as simple figures, because the crowd is big or the view is far out.</summary>
    public int Simple { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        _bodies = Simple3D("Bodies", new CapsuleMesh { Radius = 0.21f, Height = 1.16f, RadialSegments = 8, Rings = 3 });
        _heads = Simple3D("Heads", new SphereMesh { Radius = 0.2f, Height = 0.4f, RadialSegments = 10, Rings = 5 });
        _hair = Simple3D("Hair", new SphereMesh { Radius = 0.21f, Height = 0.21f, RadialSegments = 10, Rings = 3, IsHemisphere = true });
        Sync(0, snap: true);
    }

    private MultiMeshInstance3D Simple3D(string name, Mesh mesh)
    {
        var node = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh },
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1f, MetallicSpecular = 0.05f },
            Visible = false,
        };
        AddChild(node);
        return node;
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

    /// <summary>Makes room to remember where each person was last drawn and what colours they are, by their number.</summary>
    private void Remember(int id, Simulation.People.PersonLook look)
    {
        if (id >= _shown.Length)
        {
            var size = System.Math.Max(256, System.Math.Max(id + 1, _shown.Length * 2));
            System.Array.Resize(ref _shown, size);
            System.Array.Resize(ref _shownStamp, size);
            System.Array.Resize(ref _topColours, size);
            System.Array.Resize(ref _skinColours, size);
            System.Array.Resize(ref _hairColours, size);
            System.Array.Resize(ref _heights, size);
        }

        if (_heights[id] == 0)
        {
            _topColours[id] = new Color(look.Poncho ?? look.TopColour).SrgbToLinear();
            _skinColours[id] = new Color(look.Skin).SrgbToLinear();
            _hairColours[id] = new Color(look.Hat == Simulation.People.Hat.None ? look.HairColour : look.HatColour).SrgbToLinear();
            _heights[id] = (float)look.Height;
        }
    }

    private static void Put(float[] buffer, int index, Basis basis, Vector3 origin, Color colour)
    {
        var o = index * 16;
        (buffer[o], buffer[o + 1], buffer[o + 2], buffer[o + 3]) = (basis.X.X, basis.Y.X, basis.Z.X, origin.X);
        (buffer[o + 4], buffer[o + 5], buffer[o + 6], buffer[o + 7]) = (basis.X.Y, basis.Y.Y, basis.Z.Y, origin.Y);
        (buffer[o + 8], buffer[o + 9], buffer[o + 10], buffer[o + 11]) = (basis.X.Z, basis.Y.Z, basis.Z.Z, origin.Z);
        (buffer[o + 12], buffer[o + 13], buffer[o + 14], buffer[o + 15]) = (colour.R, colour.G, colour.B, 1f);
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
        var camera = GetViewport()?.GetCamera3D();
        var cameraYaw = camera?.GlobalRotation.Y ?? 0f;
        _people.Clear();
        _people.AddRange(_game.Crowd.People);

        // A village crowd is all drawn in full. In a big one, only the people nearest the middle of
        // a close view are; everyone else is a simple figure in their own colours.
        var everyone = _people.Count <= MostInFull;
        var reach = float.MaxValue;
        var focus = Vector3.Zero;
        if (!everyone)
        {
            reach = 0;
            if (camera is not null && camera.Size <= FullWithin)
            {
                var origin = camera.GlobalPosition;
                var forward = -camera.GlobalBasis.Z;
                focus = Mathf.Abs(forward.Y) > 0.001f ? origin + (forward * (-origin.Y / forward.Y)) : origin;
                reach = camera.Size * 1.1f;
                _distances.Clear();
                foreach (var person in _people)
                {
                    var away = new Vector2((float)person.X - focus.X, (float)person.Y - focus.Z).Length();
                    if (away < reach)
                    {
                        _distances.Add(away);
                    }
                }

                if (_distances.Count > MostInFull)
                {
                    _distances.Sort();
                    reach = _distances[MostInFull];
                }
            }
        }

        _stamp++;
        var simple = 0;
        var made = 0;
        if (!everyone && _bodyBuffer.Length < _people.Count * 16)
        {
            var size = Mathf.NearestPo2(_people.Count) * 16;
            _bodyBuffer = new float[size];
            _headBuffer = new float[size];
            _hairBuffer = new float[size];
        }

        _seen.Clear();
        foreach (var person in _people)
        {
            // Someone unwell lies on the ground, or on a bed if they have been taken to the first aid tent.
            var lift = !person.Unwell ? 0f
                : _game.Site.StallAt(Simulation.World.FieldMap.TileAt(person.X, person.Y))?.Kind == StallKind.FirstAidTent ? 0.62f : 0.14f;
            var tilt = person.Unwell ? -Mathf.Pi / 2 : 0f;
            var target = new Vector3((float)person.X, lift, (float)person.Y);
            var heading = (float)person.Heading;
            var has = _views.TryGetValue(person.Id, out var view);
            var near = everyone || new Vector2(target.X - focus.X, target.Z - focus.Z).Length() < (has ? reach * 1.15f : reach);
            if (near && !has && (snap || everyone || made < MadePerFrame))
            {
                made++;
                view = PersonView.Build(person.Look);
                view.Position = !everyone && person.Id < _shownStamp.Length && _shownStamp[person.Id] == _stamp - 1 ? _shown[person.Id] : target;
                view.Rotation = new Vector3(tilt, heading, 0);
                _views[person.Id] = view;
                AddChild(view);
                has = true;
            }

            if (near && has)
            {
                _seen.Add(person.Id);
                view!.Position = view.Position.Lerp(target, glide);
                view.Rotation = new Vector3(tilt, Mathf.LerpAngle(view.Rotation.Y, heading, glide), 0);

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

                if (!everyone)
                {
                    Remember(person.Id, person.Look);
                    _shown[person.Id] = view.Position;
                    _shownStamp[person.Id] = _stamp;
                }

                continue;
            }

            // A simple figure: a body in the colour of their top or poncho, a head, and hair or a hat.
            Remember(person.Id, person.Look);
            var at = _shownStamp[person.Id] == _stamp - 1 ? _shown[person.Id].Lerp(target, glide) : target;
            _shown[person.Id] = at;
            _shownStamp[person.Id] = _stamp;
            var tall = _heights[person.Id];
            var basis = person.Unwell ? new Basis(Vector3.Right, tilt).Scaled(Vector3.One * tall) : Basis.FromScale(Vector3.One * tall);
            Put(_bodyBuffer, simple, basis, at + (basis * new Vector3(0, 0.58f, 0)), _topColours[person.Id]);
            Put(_headBuffer, simple, basis, at + (basis * new Vector3(0, 1.33f, 0)), _skinColours[person.Id]);
            Put(_hairBuffer, simple, basis, at + (basis * new Vector3(0, 1.39f, 0)), _hairColours[person.Id]);
            simple++;
        }

        Simple = simple;
        foreach (var (node, buffer) in new[] { (_bodies, _bodyBuffer), (_heads, _headBuffer), (_hair, _hairBuffer) })
        {
            node.Visible = simple > 0;
            if (simple > 0)
            {
                if (node.Multimesh.InstanceCount * 16 != buffer.Length)
                {
                    node.Multimesh.InstanceCount = buffer.Length / 16;
                }

                node.Multimesh.Buffer = buffer;
                node.Multimesh.VisibleInstanceCount = simple;
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
