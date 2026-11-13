using System;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Review pictures of the things the player builds, run with: godot --path game -- --build-shots=DIR
/// (needs a display). The stages, fences and gates side by side with people for scale, then the
/// field laid out as a first village festival and as a festival a few years on.
/// </summary>
public partial class BuildShots : Node
{
    private static readonly string[] Names =
    {
        "1_stages", "2_fences", "3_gates", "4_choosing_a_stage", "5_dragging_a_fence", "6_first_festival", "7_queue_at_the_gate",
        "8_hired_stage", "9_arch_gate_two_lanes", "10_no_way_in",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;
    private Node3D? _set;

    public BuildShots(string directory)
    {
        _directory = directory;
    }

    public BuildShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(NextScene).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_scene < 0 || _scene >= Names.Length)
        {
            return;
        }

        _frame++;
        if (!_ready)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                f.Game.Clock.SetSpeed(Simulation.Core.GameSpeed.Paused);
                _set = new Node3D { Name = "Set" };
                f.AddChild(_set);
                Arrange(f);
                f.Crowd.Snap();
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            var path = $"{_directory}/{Names[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[build] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        _ready = false;
        _frame = 0;
        if (_scene >= Names.Length)
        {
            GD.Print("[build] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null);
    }

    private static int StandingInQueue(FestivalScene festival) =>
        System.Linq.Enumerable.Count(festival.Game.Crowd.Parties, p => p.State == PartyState.Queuing && !p.IsWalking);

    private static void RunTo(FestivalScene festival, double hour)
    {
        var until = festival.Game.Now.Date.AddHours(hour);
        while (festival.Game.Now < until)
        {
            festival.Game.Clock.Step();
        }
    }

    /// <summary>Closes the gap in the hedge with fence and puts a gate in the middle of it.</summary>
    private static void GateInTheGap(FestivalScene festival, GateKind gate, FenceKind fence)
    {
        var game = festival.Game;
        var gap = game.Field.Gap;
        var y = gap.Y + 1;
        game.BuildFence(fence, new TilePoint(gap.X, y), new TilePoint(gap.Right - 1, y));
        var width = Catalogue.Of(gate).Width;
        game.BuildGate(gate, gap.X + ((gap.Width - width) / 2), y, false);
    }

    /// <summary>A stage at the north end with a fenced patch behind it for the acts.</summary>
    private static TileRect StageWithBackstage(FestivalScene festival, StageKind kind, FenceKind fence)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var info = Catalogue.Of(kind);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        var y = f.Y + 12;
        game.BuildStage(kind, x, y, Facing.South);
        var rect = new TileRect(x, y, info.Width, info.Depth);
        var left = rect.X - 8;
        var right = rect.Right + 7;
        var back = rect.Y - 7;
        var front = rect.Bottom - 2;
        game.BuildFence(fence, new TilePoint(left, back), new TilePoint(right, back));
        game.BuildFence(fence, new TilePoint(left, back), new TilePoint(left, front));
        game.BuildFence(fence, new TilePoint(right, back), new TilePoint(right, front));
        game.BuildFence(fence, new TilePoint(left, front), new TilePoint(rect.X - 1, front));
        game.BuildFence(fence, new TilePoint(rect.Right, front), new TilePoint(right, front));
        return rect;
    }

    private void Arrange(FestivalScene festival)
    {
        var name = festival.Game.Name;
        var field = festival.Game.Field.Field;
        var gap = festival.Game.Field.Gap;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);
        var gateAt = new Vector3((gap.X + (gap.Width / 2f)) * 0.5f, 0, gap.Bottom * 0.5f);
        festival.SetHudShown(_scene >= 3);

        switch (_scene)
        {
            case 0:
            {
                // A stand turned to face the camera.
                _set!.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var x = -13.5f;
                foreach (var info in Catalogue.Stages)
                {
                    var w = info.Width / 2f;
                    var stage = SiteProps.Stage(info.Kind, name);
                    stage.Position = new Vector3(x + (w / 2), 0, -info.Depth / 4f);
                    _set.AddChild(stage);
                    Label($"{info.Name}  £{info.Cost:N0}", new Vector3(x + (w / 2), 0, 4.4f), 0.011f);
                    People(3, new Vector3(x + (w / 2) - 1.2f, 0, 2.4f), 1.2f, 0, 180, 40 + (int)info.Kind);
                    x += w + 2.2f;
                }

                festival.Camera.LookAt(centre.X + 0.6f, centre.Z + 0.6f, 22f);
                break;
            }

            case 1:
            {
                _set!.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var x = -15.4f;
                foreach (var info in Catalogue.Fences)
                {
                    _set.AddChild(SiteProps.Fence(info.Kind, new Vector3(x, 0, 0), new Vector3(x + 7f, 0, 0)));
                    Label($"{info.Name}  £{info.CostPerMetre} a metre", new Vector3(x + 3.5f, 0, 2.6f), 0.007f);
                    People(2, new Vector3(x + 2.6f, 0, -0.9f), 1.6f, 0, 0, 60 + (int)info.Kind);
                    x += 8f;
                }

                festival.Camera.LookAt(centre.X + 0.6f, centre.Z + 0.6f, 19f);
                break;
            }

            case 2:
            {
                _set!.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var village = SiteProps.Gate(GateKind.VillageGate, name);
                village.Position = new Vector3(-7f, 0, 0);
                _set.AddChild(village);
                Label("Village gate  £40", new Vector3(-7f, 0, 6.2f), 0.009f);
                People(4, new Vector3(-7f, 0, 1.6f), 0, 1.0f, 180, 70);
                var arch = SiteProps.Gate(GateKind.ArchGate, name);
                arch.Position = new Vector3(5f, 0, -2f);
                _set.AddChild(arch);
                Label("Arch gate  £600", new Vector3(5f, 0, 6.2f), 0.009f);
                People(4, new Vector3(3.5f, 0, 0f), 0, 1.0f, 180, 80);
                People(4, new Vector3(6.5f, 0, 0.4f), 0, 1.0f, 180, 90);
                festival.Camera.LookAt(centre.X + 0.9f, centre.Z + 0.9f, 17f);
                break;
            }

            case 3:
            {
                // The build bar open on Stages, with a bandstand following the pointer.
                festival.BuildBar.Toggle(BuildMode.Stage);
                festival.BuildBar.Choose("stageBandstand");
                festival.Build.ShowGhost(new TilePoint(field.X + 70, field.Y + 18));
                festival.Camera.LookAt(centre.X + 3, centre.Z - 2, 34f);
                break;
            }

            case 4:
            {
                var rect = StageWithBackstage(festival, StageKind.Bandstand, FenceKind.Paling);
                festival.BuildBar.Toggle(BuildMode.Fence);
                festival.BuildBar.Choose("fenceRope");
                festival.Build.ShowGhost(new TilePoint(rect.X - 30, rect.Bottom + 14));
                festival.Camera.LookAt((rect.X * 0.5f) - 2, (rect.Bottom * 0.5f) + 4, 30f);
                break;
            }

            case 5:
                StageWithBackstage(festival, StageKind.Bandstand, FenceKind.Rope);
                GateInTheGap(festival, GateKind.VillageGate, FenceKind.Paling);
                RunTo(festival, 9.5);
                festival.Camera.ShowWholeField();
                break;

            case 6:
                StageWithBackstage(festival, StageKind.Bandstand, FenceKind.Rope);
                GateInTheGap(festival, GateKind.VillageGate, FenceKind.Paling);
                RunTo(festival, 8.5);
                while (StandingInQueue(festival) < 4 && festival.Game.Now.Hour < 11)
                {
                    festival.Game.Clock.Step();
                }

                festival.Camera.LookAt(gateAt.X + 1.5f, gateAt.Z + 1.5f, 17f);
                break;

            case 7:
            {
                var rect = StageWithBackstage(festival, StageKind.HiredStage, FenceKind.SteelMesh);
                festival.Game.BuildFence(FenceKind.CrowdBarrier, new TilePoint(rect.X - 2, rect.Bottom + 3), new TilePoint(rect.Right + 1, rect.Bottom + 3));
                GateInTheGap(festival, GateKind.VillageGate, FenceKind.Rope);
                RunTo(festival, 12);
                festival.Camera.LookAt((rect.X + 12) * 0.5f + 1.5f, (rect.Bottom * 0.5f) + 5.5f, 30f);
                break;
            }

            case 8:
                festival.Game.BuildStage(StageKind.FarmTrailer, field.X + 60, field.Y + 10, Facing.South);
                GateInTheGap(festival, GateKind.ArchGate, FenceKind.Rope);
                RunTo(festival, 8.5);
                while (StandingInQueue(festival) < 3 && festival.Game.Now.Hour < 11)
                {
                    festival.Game.Clock.Step();
                }

                festival.Camera.LookAt(gateAt.X + 2f, gateAt.Z + 2.5f, 20f);
                break;

            case 9:
                // The gap fenced shut and no gate: people wait on the track.
                festival.Game.BuildFence(FenceKind.SteelMesh, new TilePoint(gap.X, gap.Y + 1), new TilePoint(gap.Right - 1, gap.Y + 1));
                RunTo(festival, 9.5);
                festival.Camera.LookAt(gateAt.X + 1.5f, gateAt.Z + 2.5f, 20f);
                break;
        }
    }

    private void People(int count, Vector3 first, float stepX, float stepZ, float facing, int seed)
    {
        for (var i = 0; i < count; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(seed + (i * 13), AgeGroup.Adult));
            person.Position = first + new Vector3(stepX * i, 0, stepZ * i);
            person.RotationDegrees = new Vector3(0, facing, 0);
            person.SetPose(Pose.Standing);
            _set!.AddChild(person);
        }
    }

    private void Label(string text, Vector3 at, float pixelSize)
    {
        _set!.AddChild(new Label3D
        {
            Text = text,
            FontSize = 48,
            PixelSize = pixelSize,
            OutlineSize = 12,
            OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = at + new Vector3(0, 0.02f, 0),
        });
    }
}
