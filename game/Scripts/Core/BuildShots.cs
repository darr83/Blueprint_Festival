using System;
using BlueprintFestival.Simulation.People;
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
        "1_stages", "2_fences", "3_gates", "4_first_year_field", "5_first_year_stage", "6_first_year_gate",
        "7_trailer_year_field", "8_later_year_field", "9_later_year_stage", "10_later_year_gate",
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private FestivalScene? _festival;
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
        Callable.From(() => GameManager.Instance.StartNewGame(null, new DateTime(2027, 7, 17, 13, 0, 0))).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_festival is null)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady())
            {
                _festival = f;
                f.Game.Clock.SetSpeed(Simulation.Core.GameSpeed.Paused);
                f.SetHudShown(false);
                Next();
            }

            return;
        }

        _frame++;
        if (_frame == 12)
        {
            var path = $"{_directory}/{Names[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[build] {path}");
            Next();
        }
    }

    private void Next()
    {
        _scene++;
        _frame = 0;
        _set?.Free();
        _set = null;
        if (_scene >= Names.Length)
        {
            GD.Print("[build] Done.");
            GetTree().Quit(0);
            return;
        }

        var festival = _festival!;
        var name = festival.Game.Name;
        var field = festival.Game.Field.Field;
        var gap = festival.Game.Field.Gap;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);
        var stageAt = new Vector3(centre.X + 4, 0, (field.Y * 0.5f) + 7f);
        var gateAt = new Vector3((gap.X + (gap.Width / 2f)) * 0.5f, 0, gap.Bottom * 0.5f);
        _set = new Node3D { Name = "Set" };
        festival.AddChild(_set);

        switch (_scene)
        {
            case 0:
            {
                // A stand turned to face the camera.
                _set.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var x = -11.5f;
                foreach (var kind in Enum.GetValues<StageKind>())
                {
                    var (w, d) = SiteProps.StageSize(kind);
                    var stage = SiteProps.Stage(kind, name);
                    stage.Position = new Vector3(x + (w / 2), 0, -d / 2);
                    _set.AddChild(stage);
                    Label(SiteProps.StageName(kind), new Vector3(x + (w / 2), 0, 4.4f), 0.011f);
                    People(3, new Vector3(x + (w / 2) - 1.2f, 0, 2.4f), 1.2f, 0, 180, 40 + (int)kind);
                    x += w + 3.2f;
                }

                festival.Camera.LookAt(centre.X + 0.6f, centre.Z + 0.6f, 21f);
                break;
            }

            case 1:
            {
                _set.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var x = -15.4f;
                foreach (var kind in Enum.GetValues<FenceKind>())
                {
                    _set.AddChild(SiteProps.Fence(kind, new Vector3(x, 0, 0), new Vector3(x + 7f, 0, 0)));
                    Label(SiteProps.FenceName(kind), new Vector3(x + 3.5f, 0, 2.6f), 0.008f);
                    People(2, new Vector3(x + 2.6f, 0, -0.9f), 1.6f, 0, 0, 60 + (int)kind);
                    x += 8f;
                }

                festival.Camera.LookAt(centre.X + 0.6f, centre.Z + 0.6f, 19f);
                break;
            }

            case 2:
            {
                _set.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var village = SiteProps.Gate(GateKind.VillageGate, name);
                village.Position = new Vector3(-7f, 0, 0);
                _set.AddChild(village);
                Label("Village gate", new Vector3(-7f, 0, 6.2f), 0.009f);
                People(4, new Vector3(-7f, 0, 1.6f), 0, 1.0f, 180, 70);
                var arch = SiteProps.Gate(GateKind.ArchGate, name);
                arch.Position = new Vector3(5f, 0, -2f);
                _set.AddChild(arch);
                Label("Arch gate", new Vector3(5f, 0, 6.2f), 0.009f);
                People(4, new Vector3(3.4f, 0, 0f), 0, 1.0f, 180, 80);
                People(4, new Vector3(6.6f, 0, 0.4f), 0, 1.0f, 180, 90);
                festival.Camera.LookAt(centre.X + 0.9f, centre.Z + 0.9f, 17f);
                break;
            }

            case 3:
                LayOut(StageKind.Bandstand, FenceKind.Rope, GateKind.VillageGate, stageAt, gateAt, name, 110);
                festival.Camera.ShowWholeField();
                break;
            case 4:
                LayOut(StageKind.Bandstand, FenceKind.Rope, GateKind.VillageGate, stageAt, gateAt, name, 110);
                festival.Camera.LookAt(stageAt.X + 1.5f, stageAt.Z + 6.5f, 24f);
                break;
            case 5:
                LayOut(StageKind.Bandstand, FenceKind.Rope, GateKind.VillageGate, stageAt, gateAt, name, 110);
                festival.Camera.LookAt(gateAt.X + 1.5f, gateAt.Z + 1.5f, 17f);
                break;
            case 6:
                LayOut(StageKind.FarmTrailer, FenceKind.Paling, GateKind.VillageGate, stageAt, gateAt, name, 70);
                festival.Camera.LookAt(stageAt.X + 1.5f, stageAt.Z + 6.5f, 24f);
                break;
            case 7:
                LayOut(StageKind.HiredStage, FenceKind.SteelMesh, GateKind.ArchGate, stageAt, gateAt, name, 190);
                festival.Camera.ShowWholeField();
                break;
            case 8:
                LayOut(StageKind.HiredStage, FenceKind.SteelMesh, GateKind.ArchGate, stageAt, gateAt, name, 190);
                festival.Camera.LookAt(stageAt.X + 1.5f, stageAt.Z + 7.5f, 27f);
                break;
            case 9:
                LayOut(StageKind.HiredStage, FenceKind.SteelMesh, GateKind.ArchGate, stageAt, gateAt, name, 190);
                festival.Camera.LookAt(gateAt.X + 2f, gateAt.Z + 2.5f, 20f);
                break;
        }

        festival.Camera.Snap();
    }

    /// <summary>The field set out for a show: a stage at the north end, a fenced-off patch behind it, a gate in the gap and a crowd.</summary>
    private void LayOut(StageKind stageKind, FenceKind fenceKind, GateKind gateKind, Vector3 stageAt, Vector3 gateAt, string name, int crowd)
    {
        var (w, d) = SiteProps.StageSize(stageKind);
        var stage = SiteProps.Stage(stageKind, name);
        stage.Position = stageAt;
        _set!.AddChild(stage);

        // Backstage: fenced off behind and beside the stage.
        var b = new MeshBuilder();
        var left = stageAt.X - (w / 2) - 5f;
        var right = stageAt.X + (w / 2) + 5f;
        var back = stageAt.Z - (d / 2) - 3.5f;
        var front = stageAt.Z + (d / 2) - 0.6f;
        SiteProps.AddFence(b, fenceKind, new Vector3(left, 0, front), new Vector3(stageAt.X - (w / 2) - 1.2f, 0, front));
        SiteProps.AddFence(b, fenceKind, new Vector3(stageAt.X + (w / 2) + 1.2f, 0, front), new Vector3(right, 0, front));
        SiteProps.AddFence(b, fenceKind, new Vector3(left, 0, back), new Vector3(left, 0, front));
        SiteProps.AddFence(b, fenceKind, new Vector3(right, 0, back), new Vector3(right, 0, front));
        SiteProps.AddFence(b, fenceKind, new Vector3(left, 0, back), new Vector3(right, 0, back));
        if (stageKind == StageKind.HiredStage)
        {
            // A barrier across the front of a tall stage keeps a gap for stewards.
            SiteProps.AddFence(b, FenceKind.CrowdBarrier, new Vector3(stageAt.X - (w / 2) - 1.4f, 0, stageAt.Z + (d / 2) + 1.6f), new Vector3(stageAt.X + (w / 2) + 1.4f, 0, stageAt.Z + (d / 2) + 1.6f));
        }

        // The gate, with fence closing the rest of the gap.
        var gate = SiteProps.Gate(gateKind, name);
        var gateZ = gateAt.Z + (gateKind == GateKind.ArchGate ? 1.1f : 0.2f);
        gate.Position = new Vector3(gateAt.X, 0, gateZ);
        _set.AddChild(gate);
        if (gateKind == GateKind.VillageGate)
        {
            SiteProps.AddFence(b, fenceKind, new Vector3(gateAt.X - 3f, 0, gateZ), new Vector3(gateAt.X - 2.1f, 0, gateZ));
            SiteProps.AddFence(b, fenceKind, new Vector3(gateAt.X + 2.1f, 0, gateZ), new Vector3(gateAt.X + 3f, 0, gateZ));
        }

        _set.AddChild(b.BuildNode("Fences"));

        // The crowd in front of the stage, a few people about the field, and a queue at the gate.
        var random = new Random(31);
        var frontRow = stageAt.Z + (d / 2) + (stageKind == StageKind.HiredStage ? 2.4f : 1.8f);
        for (var i = 0; i < crowd; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(3000 + i));
            if (i < crowd * 0.72)
            {
                var deep = Math.Pow(random.NextDouble(), 0.75) * (6 + (crowd / 18.0));
                var across = (random.NextDouble() - 0.5) * (w + 3 + (deep * 1.1));
                person.Position = new Vector3(stageAt.X + (float)across, 0, frontRow + (float)deep);
                person.RotationDegrees = new Vector3(0, 180 + ((float)random.NextDouble() * 24) - 12, 0);
                person.SetPose(random.NextDouble() < 0.35 ? Pose.Cheering : Pose.Standing);
            }
            else
            {
                person.Position = new Vector3(
                    stageAt.X + (((float)random.NextDouble() - 0.5f) * 50f),
                    0,
                    stageAt.Z + 14f + ((float)random.NextDouble() * 16f));
                person.RotationDegrees = new Vector3(0, (float)random.NextDouble() * 360, 0);
                person.SetPose(random.NextDouble() < 0.6 ? Pose.Walking : Pose.Standing, (float)random.NextDouble());
            }

            _set.AddChild(person);
        }

        var lanes = gateKind == GateKind.ArchGate ? new[] { -1.4f, 1.4f } : new[] { 0.3f };
        var seed = 5000;
        foreach (var lane in lanes)
        {
            for (var i = 0; i < 9; i++)
            {
                var person = PersonView.Build(LookGenerator.FestivalGoer(seed++));
                person.Position = new Vector3(gateAt.X + lane + (((i * 7) % 3) - 1) * 0.12f, 0, gateZ + 0.9f + (i * 0.75f));
                person.RotationDegrees = new Vector3(0, 180, 0);
                person.SetPose(Pose.Standing);
                _set.AddChild(person);
            }
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
