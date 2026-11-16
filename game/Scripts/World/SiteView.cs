using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Draws what the player has built: stages, fences and gates. Redrawn whenever the layout changes.</summary>
public partial class SiteView : Node3D
{
    private const float T = (float)FieldMap.TileMetres;

    /// <summary>How far a cubicle door swings open, in degrees.</summary>
    private const float DoorOpenDegrees = -105f;
    private const float DoorHoldSeconds = 0.7f;
    private const float DoorSwingSpeed = 420f;
    private readonly System.Collections.Generic.Dictionary<Node3D, float> _doorHeld = new();

    private readonly System.Collections.Generic.List<(int Stall, Node3D[] Doors)> _toilets = new();
    private readonly System.Collections.Generic.List<(PlacedStall Bin, Node3D Rubbish)> _bins = new();
    private readonly System.Collections.Generic.List<Node3D> _shutBars = new();

    /// <summary>True when the poles across the gates are showing, because the gates are shut.</summary>
    public bool GatesLookShut => _shutBars.Count > 0 && _shutBars[0].Visible;
    private FestivalGame _game = null!;
    private bool _stale;

    /// <summary>How many stages are drawn.</summary>
    public int StageCount { get; private set; }

    /// <summary>How many gates are drawn.</summary>
    public int GateCount { get; private set; }

    /// <summary>How many stalls are drawn.</summary>
    public int StallCount { get; private set; }

    /// <summary>How many seats, sinks and sails are drawn.</summary>
    public int ComfortCount { get; private set; }

    /// <summary>How many decorations and lines of bunting are drawn.</summary>
    public int DecorCount { get; private set; }

    private readonly System.Collections.Generic.List<OmniLight3D> _glows = new();

    /// <summary>How many signs are drawn.</summary>
    public int SignCount { get; private set; }

    /// <summary>How many queue lanes have their barriers drawn.</summary>
    /// <summary>How many crowd areas are drawn.</summary>
    public int AreaCount { get; private set; }

    public int LaneCount { get; private set; }

    /// <summary>How many straight runs of fence are drawn.</summary>
    public int FenceRunCount { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        game.Site.Changed += MarkStale;
        game.Site.SignWritten += MarkStale;
        Rebuild();
    }

    public override void _ExitTree()
    {
        _game.Site.Changed -= MarkStale;
        _game.Site.SignWritten -= MarkStale;
    }

    private void MarkStale() => _stale = true;

    public override void _Process(double delta)
    {
        if (_stale)
        {
            Rebuild();
        }

        Doors((float)delta, snap: false);
        TurnRides((float)delta, snap: false);

        // Fire pits glow as the daylight goes.
        if (_glows.Count > 0)
        {
            var energy = 3.4f * (float)System.Math.Clamp(1.2 - (_game.Light * 1.5), 0, 1);
            foreach (var glow in _glows)
            {
                glow.LightEnergy = energy;
            }
        }
    }

    /// <summary>One ride as it is drawn: its model, its seats, and how long it has spent going round (in seconds as watched at 1x).</summary>
    private sealed class RideOnSite
    {
        public RideOnSite(int stall, PlacedStall placed, Node3D node, System.Collections.Generic.List<Node3D> seats)
        {
            (Stall, Placed, Node, Seats) = (stall, placed, node, seats);
        }

        public int Stall { get; }

        public PlacedStall Placed { get; }

        public Node3D Node { get; }

        public System.Collections.Generic.List<Node3D> Seats { get; }

        public float Turned { get; set; }

        public float Shown { get; set; }

        public bool Running { get; set; }
    }

    private readonly System.Collections.Generic.List<RideOnSite> _rides = new();
    private readonly System.Collections.Generic.Dictionary<(int, int), float> _turning = new();
    private System.DateTime _ridesAt;

    /// <summary>How many rides are going round with people on them just now.</summary>
    public int RidesRunning => _rides.Count(r => r.Running);

    /// <summary>The rides that are going round just now, for the sound of them: what each is and where its middle is.</summary>
    public System.Collections.Generic.IEnumerable<(StallKind Kind, Vector3 At, bool Running, int Riders)> RideSounds =>
        _rides.Select(r => (r.Placed.Kind, r.Node.GlobalPosition, r.Running, _game.Crowd.Riding(r.Stall)));

    /// <summary>Where one seat of a ride is just now, or null if there is no such ride.</summary>
    public Transform3D? RideSeat(int stall, int seat)
    {
        foreach (var ride in _rides)
        {
            if (ride.Stall == stall && ride.Seats.Count > 0)
            {
                return ride.Seats[((seat % ride.Seats.Count) + ride.Seats.Count) % ride.Seats.Count].GlobalTransform;
            }
        }

        return null;
    }

    /// <summary>
    /// Turns each ride on from where it had got to. A ride goes round while the rules say it is
    /// running and somebody is on it, and stands still to let people off and on.
    /// </summary>
    public void TurnRides(float delta, bool snap)
    {
        var now = _game.Now;
        var passed = (float)System.Math.Clamp((now - _ridesAt).TotalSeconds, 0, 240) / (float)Simulation.Core.GameClock.GameSecondsPerRealSecond;
        _ridesAt = now;
        foreach (var ride in _rides)
        {
            ride.Running = _game.Phase == Phase.Day && !Rides.TurnAt(ride.Placed, now).Boarding && _game.Crowd.Riding(ride.Stall) > 0;
            if (ride.Running)
            {
                ride.Turned += passed;
                _turning[(ride.Placed.X, ride.Placed.Y)] = ride.Turned;
            }

            // The clock moves in whole seconds: ease after it so the ride goes round smoothly.
            var shown = snap ? ride.Turned : Mathf.Lerp(ride.Shown, ride.Turned, 1f - Mathf.Exp(-delta * 5f));
            if (shown != ride.Shown || snap)
            {
                ride.Shown = shown;
                SiteProps.TurnRide(ride.Node, ride.Placed.Kind, shown);
            }
        }
    }

    /// <summary>
    /// Swings each cubicle door open or shut and turns its sign red or green, to match what the
    /// rules say is happening there. The first cubicle in the rules is the last cabin in the model.
    /// </summary>
    public void Doors(float delta, bool snap)
    {
        var shut = _game.Phase == Phase.Day && _game.GatesShut;
        foreach (var bars in _shutBars)
        {
            bars.Visible = shut;
        }

        foreach (var (bin, rubbish) in _bins)
        {
            // A bin that wants emptying has bags heaped over the rim.
            rubbish.Visible = _game.BinFill(bin) >= FestivalGame.BinWantsEmptying;
        }

        foreach (var (stall, doors) in _toilets)
        {
            for (var i = 0; i < doors.Length; i++)
            {
                var look = _game.Crowd.Cubicle(stall, doors.Length - 1 - i);
                var door = doors[i];

                // At 4x and 8x a door is only open for a blink, so keep it open long enough to see.
                _doorHeld.TryGetValue(door, out var held);
                held = look == Simulation.People.CubicleLook.DoorOpen ? DoorHoldSeconds : Mathf.Max(0f, held - delta);
                _doorHeld[door] = held;

                var wanted = held > 0f ? DoorOpenDegrees : 0f;
                var now = door.RotationDegrees.Y;
                door.RotationDegrees = new Vector3(0, snap ? wanted : Mathf.MoveToward(now, wanted, delta * DoorSwingSpeed), 0);

                var engaged = look == Simulation.People.CubicleLook.Engaged;
                door.GetNode<Node3D>("Engaged").Visible = engaged;
                door.GetNode<Node3D>("Vacant").Visible = !engaged;
                var cabin = door.GetParent();
                var number = door.Name.ToString().Substring("Door".Length);
                cabin.GetNode<Node3D>($"Lamp{number}Engaged").Visible = engaged;
                cabin.GetNode<Node3D>($"Lamp{number}Vacant").Visible = !engaged;
            }
        }
    }

    /// <summary>The turn, in degrees, that points a model built facing south the way it should face.</summary>
    public static float Turn(Facing facing) => facing switch { Facing.West => -90f, Facing.North => 180f, Facing.East => 90f, _ => 0f };

    /// <summary>The middle of a rectangle of tiles, in metres, on the ground.</summary>
    public static Vector3 Middle(TileRect r) => new((r.X + (r.Width / 2f)) * T, 0, (r.Y + (r.Height / 2f)) * T);

    /// <summary>The turn for a gate: its front faces the side people queue on.</summary>
    public static float Turn(SiteLayout site, PlacedGate gate) => Turn(site.FrontOf(gate));

    /// <summary>The turn that points a model built facing south along a step (x, y) on the map.</summary>
    public static float Turn((int X, int Y) front) => front.X > 0 ? 90f : front.X < 0 ? -90f : front.Y < 0 ? 180f : 0f;

    /// <summary>Adds a run of fence to a mesh: from the outer edge of its first tile to that of its last.</summary>
    public static void AddRun(MeshBuilder builder, FenceRun run)
    {
        var from = new Vector3((run.X + (run.Vertical ? 0.5f : 0f)) * T, 0, (run.Y + (run.Vertical ? 0f : 0.5f)) * T);
        var to = from + (run.Vertical ? new Vector3(0, 0, run.Length * T) : new Vector3(run.Length * T, 0, 0));
        SiteProps.AddFence(builder, run.Kind, from, to);
    }

    /// <summary>
    /// Adds one straight run of queue barrier down the middle of its tiles. Where a run across meets
    /// a run up and down, the two are joined at the corner instead of crossing or stopping short.
    /// </summary>
    public static void AddBarrier(MeshBuilder builder, SiteLayout site, int x, int y, int length, bool vertical, bool area = false)
    {
        bool Has(int tx, int ty) => area ? site.IsAreaBarrier(new TilePoint(tx, ty)) : site.IsBarrier(new TilePoint(tx, ty));
        Vector3 from, to;
        if (vertical)
        {
            from = new Vector3((x + 0.5f) * T, 0, (y - (Has(x, y - 1) ? 0.5f : 0f)) * T);
            to = new Vector3((x + 0.5f) * T, 0, (y + length + (Has(x, y + length) ? 0.5f : 0f)) * T);
        }
        else
        {
            bool Corner(int tx) => Has(tx, y - 1) || Has(tx, y + 1);
            from = new Vector3((x + (Corner(x) ? 0.5f : 0f)) * T, 0, (y + 0.5f) * T);
            to = new Vector3((x + length - (Corner(x + length - 1) ? 0.5f : 0f)) * T, 0, (y + 0.5f) * T);
        }

        SiteProps.AddFence(builder, FenceKind.CrowdBarrier, from, to);
    }

    /// <summary>
    /// Everything drawn at the last rebuild, by what it shows. A model is kept while the thing it
    /// shows is still there, so putting one thing down on a big site only builds that one model
    /// instead of every stage, stall, seat and sign again.
    /// </summary>
    private Dictionary<object, Node3D> _drawn = new();
    private Dictionary<object, Node3D> _drawing = new();

    /// <summary>How many models were made at the last rebuild, rather than kept: for the tests.</summary>
    public int MadeLastTime { get; private set; }

    /// <summary>The model drawn for something at the last rebuild if it is unchanged, otherwise a new one.</summary>
    private Node3D Keep(object key, System.Func<Node3D> make)
    {
        if (_drawing.ContainsKey(key))
        {
            // Two of the very same thing in the very same place cannot happen, but draw both if it does.
            key = (key, _drawing.Count);
        }

        if (!_drawn.Remove(key, out var node) || !IsInstanceValid(node))
        {
            node = make();
            AddChild(node);
            MadeLastTime++;
        }

        _drawing[key] = node;
        return node;
    }

    /// <summary>Redraws the site, keeping the models of everything that has not changed.</summary>
    public void Rebuild()
    {
        _stale = false;
        MadeLastTime = 0;
        _drawing.Clear();
        _toilets.Clear();
        _rides.Clear();
        _bins.Clear();
        _shutBars.Clear();
        _doorHeld.Clear();
        var site = _game.Site;
        foreach (var stage in site.Stages)
        {
            var node = Keep(("stage", stage), () => SiteProps.Stage(stage.Kind, _game.Name));
            node.Position = Middle(stage.Rect);
            node.RotationDegrees = new Vector3(0, Turn(stage.Facing), 0);
        }

        for (var i = 0; i < site.Stalls.Count; i++)
        {
            var stall = site.Stalls[i];

            // Only what shows matters: a new price on the menu board does not need a new model.
            var key = ("stall", stall.Kind, stall.X, stall.Y, stall.Facing, stall.VipOnly);
            var node = Keep(key, () =>
            {
                var made = SiteProps.Stall(stall.Kind);
                var spots = SiteProps.StaffSpots(stall.Kind);
                for (var j = 0; j < spots.Length; j++)
                {
                    var person = SiteProps.Stallholder((stall.X * 31) + (stall.Y * 7) + j);
                    person.Position = spots[j];
                    made.AddChild(person);
                }

                if (stall.VipOnly && !Catalogue.AlwaysVip(stall.Kind))
                {
                    var info = Catalogue.Of(stall.Kind);
                    made.AddChild(SiteProps.VipSign(info.Width * T, info.Depth * T));
                }

                return made;
            });
            node.Position = Middle(stall.Rect);
            node.RotationDegrees = new Vector3(0, Turn(stall.Facing), 0);
            if (Rides.IsRide(stall.Kind))
            {
                var seats = new System.Collections.Generic.List<Node3D>();
                SiteProps.RideSeats(node, seats);
                _turning.TryGetValue((stall.X, stall.Y), out var turned);
                _rides.Add(new RideOnSite(i, stall, node, seats) { Turned = turned, Shown = turned });
            }

            if (stall.Kind == StallKind.Bin)
            {
                _bins.Add((stall, node.GetNode<Node3D>("Rubbish")));
            }

            if (Catalogue.HasCabins(stall.Kind))
            {
                var doors = new Node3D[Catalogue.Of(stall.Kind).Servers];
                for (var d = 0; d < doors.Length; d++)
                {
                    doors[d] = node.FindChild($"Door{d}", true, false) as Node3D ?? throw new System.InvalidOperationException($"No Door{d} on the {stall.Kind}.");
                }

                _toilets.Add((i, doors));
            }
        }

        foreach (var comfort in site.Comforts)
        {
            var node = Keep(("comfort", comfort), () => SiteProps.Comfort(comfort.Kind));
            node.Position = Middle(comfort.Rect);
            node.RotationDegrees = new Vector3(0, Turn(comfort.Facing), 0);
        }

        ComfortCount = site.Comforts.Count;
        foreach (var sign in site.Signs)
        {
            // A sign is drawn again when its words change. The site map board shows the whole
            // site, so it is drawn again whenever anything changes.
            var key = sign.Kind == SignKind.MapBoard ? (object)("map", _drawing.Count) : ("sign", sign.Kind, sign.X, sign.Y, sign.Facing, sign.Colour, sign.Points, string.Join("\n", sign.Lines ?? new List<string>()));
            var node = Keep(key, () => SiteProps.Sign(sign, site));
            node.Position = Middle(sign.Rect);
            node.RotationDegrees = new Vector3(0, Turn(sign.Facing), 0);
        }

        SignCount = site.Signs.Count;
        _glows.Clear();
        foreach (var decor in site.Decorations)
        {
            var node = Keep(("decor", decor), () => SiteProps.Decoration(decor));
            node.Position = Middle(decor.Rect);
            node.RotationDegrees = new Vector3(0, Turn(decor.Facing), 0);
            if (node.GetNodeOrNull<OmniLight3D>("Glow") is { } glow)
            {
                _glows.Add(glow);
            }
        }

        foreach (var run in site.Bunting)
        {
            Keep(("bunting", run), () => SiteProps.BuntingBetween(run));
        }

        DecorCount = site.Decorations.Count + site.Bunting.Count;
        for (var i = 0; i < site.Gates.Count; i++)
        {
            var gate = site.Gates[i];
            var node = Keep(("gate", gate), () => SiteProps.Gate(gate.Kind, _game.Name));
            node.Position = Middle(gate.Rect);

            // Which way a gate faces depends on the fences round it, so it is turned again every time.
            node.RotationDegrees = new Vector3(0, Turn(site, gate), 0);
            _shutBars.Add(node.GetNode<Node3D>("Shut"));
        }

        var runs = site.FenceRuns();
        if (runs.Count > 0)
        {
            Keep(("fences", Signature(runs.Select(r => (r.X, r.Y, r.Length, r.Vertical, (int)r.Kind)))), () =>
            {
                var builder = new MeshBuilder();
                foreach (var run in runs)
                {
                    AddRun(builder, run);
                }

                return builder.BuildNode("Fences");
            });
        }

        // The barriers down both sides of every queue lane.
        var barriers = site.LaneBarriers;
        if (barriers.Count > 0)
        {
            var laneRuns = SiteLayout.RunsOf(barriers).ToList();
            Keep(("lane barriers", Signature(laneRuns.Select(r => (r.X, r.Y, r.Length, r.Vertical, 0)))), () =>
            {
                var builder = new MeshBuilder();
                foreach (var (x, y, length, vertical) in laneRuns)
                {
                    AddBarrier(builder, site, x, y, length, vertical);
                }

                return builder.BuildNode("QueueBarriers");
            });
        }

        // The barriers round the crowd areas, gaps left open.
        if (site.AreaBarriers.Count > 0)
        {
            var areaRuns = SiteLayout.RunsOf(site.AreaBarriers).ToList();
            Keep(("area barriers", Signature(areaRuns.Select(r => (r.X, r.Y, r.Length, r.Vertical, 0)))), () =>
            {
                var builder = new MeshBuilder();
                foreach (var (x, y, length, vertical) in areaRuns)
                {
                    AddBarrier(builder, site, x, y, length, vertical, area: true);
                }

                return builder.BuildNode("AreaBarriers");
            });
        }

        // Whatever was drawn last time and is not there now has been taken away.
        foreach (var gone in _drawn.Values)
        {
            if (IsInstanceValid(gone))
            {
                gone.QueueFree();
            }
        }

        _drawn.Clear();
        (_drawn, _drawing) = (_drawing, _drawn);

        AreaCount = site.Areas.Count;

        LaneCount = site.Lanes.Count;

        TurnRides(0, snap: true);
        Doors(0, snap: true);
        StageCount = site.Stages.Count;
        GateCount = site.Gates.Count;
        StallCount = site.Stalls.Count;
        FenceRunCount = runs.Count;
    }

    /// <summary>A string that is the same for two lists of runs only if they are the same runs, to tell whether a joined-up mesh needs building again.</summary>
    private static string Signature(IEnumerable<(int X, int Y, int Length, bool Vertical, int Kind)> runs)
    {
        var text = new System.Text.StringBuilder();
        foreach (var (x, y, length, vertical, kind) in runs)
        {
            text.Append(x).Append(',').Append(y).Append(',').Append(length).Append(vertical ? 'v' : 'h').Append(kind).Append(';');
        }

        return text.ToString();
    }
}
