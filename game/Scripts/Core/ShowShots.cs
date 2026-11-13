using System;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Mock-up pictures for the show, run with: godot --path game -- --show-shots=DIR (needs a display).
/// The band members in a line, then acts on each stage with a crowd watching, by day and at night.
/// </summary>
public partial class ShowShots : Node
{
    private static readonly (string Name, double Hour)[] Scenes =
    {
        ("1_the_band", 14), ("2_duo_on_the_trailer", 13), ("3_band_on_the_bandstand", 15.5), ("4_bandstand_closer", 15.5),
        ("5_hired_stage_at_dusk", 20.6), ("6_headline_set_at_night", 22), ("7_night_closer", 22), ("8_dj_set", 22),
    };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;
    private Node3D _set = null!;

    public ShowShots(string directory)
    {
        _directory = directory;
    }

    public ShowShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(NextScene).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_scene < 0 || _scene >= Scenes.Length)
        {
            return;
        }

        _frame++;
        if (!_ready)
        {
            if (GetTree().CurrentScene is FestivalScene f && f.IsNodeReady() && f.Game == GameManager.Instance.Game)
            {
                f.Game.Clock.SetSpeed(Simulation.Core.GameSpeed.Paused);
                f.SetHudShown(false);
                _set = new Node3D { Name = "Set" };
                f.AddChild(_set);
                Arrange(f);
                f.Camera.Snap();
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 20)
        {
            var path = $"{_directory}/{Scenes[_scene].Name}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[show] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[show] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, new DateTime(2027, 7, 17).AddHours(Scenes[_scene].Hour));
    }

    private void Arrange(FestivalScene festival)
    {
        var field = festival.Game.Field.Field;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);
        switch (_scene)
        {
            case 0:
            {
                _set.Position = centre;
                _set.RotationDegrees = new Vector3(0, 45, 0);
                var roles = Enum.GetValues<BandRole>();
                for (var i = 0; i < roles.Length; i++)
                {
                    var x = (i - ((roles.Length - 1) / 2f)) * 2.1f;
                    var performer = Performers.Build(roles[i], i + 1);
                    performer.Position = new Vector3(x, 0, 0);
                    _set.AddChild(performer);
                    _set.AddChild(new Label3D
                    {
                        Text = Performers.Name(roles[i]),
                        FontSize = 40,
                        PixelSize = 0.006f,
                        OutlineSize = 10,
                        OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
                        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                        NoDepthTest = true,
                        Position = new Vector3(x, 0.02f, 1.9f),
                    });
                }

                festival.Camera.LookAt(centre.X + 0.5f, centre.Z + 0.5f, 9.5f);
                break;
            }

            case 1:
            {
                var at = Stage(festival, StageKind.FarmTrailer);
                Act(at, 1.1f, -0.25f, (BandRole.Singer, -1.3f, 0.3f), (BandRole.Guitarist, 1.2f, 0.2f));
                Audience(at, 3.5f / 2, 60, 7, false);
                festival.Camera.LookAt(at.X + 1.5f, at.Z + 5f, 22f);
                break;
            }

            case 2:
            case 3:
            {
                var at = Stage(festival, StageKind.Bandstand);
                Act(at, 0.6f, -0.5f, (BandRole.Singer, 0f, 1.2f), (BandRole.Guitarist, -1.9f, 0.8f), (BandRole.Fiddler, 1.9f, 0.8f), (BandRole.Drummer, 0f, -1.3f));
                Audience(at, 5.5f / 2, 120, 9, false);
                if (_scene == 2)
                {
                    festival.Camera.ShowWholeField();
                }
                else
                {
                    festival.Camera.LookAt(at.X + 1.5f, at.Z + 5.5f, 22f);
                }

                break;
            }

            default:
            {
                var at = Stage(festival, StageKind.HiredStage);
                if (_scene == 7)
                {
                    Act(at, 1.3f, 0, (BandRole.Dj, 0f, 0.6f));
                }
                else
                {
                    Act(at, 1.3f, 0, (BandRole.Singer, 0f, 1.7f), (BandRole.Guitarist, -2.4f, 1.2f), (BandRole.Bassist, 2.4f, 1.2f), (BandRole.Keys, 3.0f, -0.6f), (BandRole.Drummer, -0.2f, -1.9f));
                }

                Audience(at, 3f, 190, 12, _scene >= 5);
                Lights(at, _scene >= 5 ? 1f : 0.45f, _scene == 7);
                if (_scene == 5)
                {
                    festival.Camera.ShowWholeField();
                }
                else
                {
                    festival.Camera.LookAt(at.X + 1.5f, at.Z + 7f, _scene == 4 ? 30f : 24f);
                }

                break;
            }
        }
    }

    /// <summary>Puts a stage up at the north end, through the game's own rules. Returns its middle in metres.</summary>
    private static Vector3 Stage(FestivalScene festival, StageKind kind)
    {
        var f = festival.Game.Field.Field;
        var info = Catalogue.Of(kind);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        festival.Game.BuildStage(kind, x, f.Y + 12, Facing.South);
        festival.Site.Rebuild();
        return SiteView.Middle(new TileRect(x, f.Y + 12, info.Width, info.Depth));
    }

    private void Act(Vector3 stage, float deck, float shift, params (BandRole Role, float X, float Z)[] members)
    {
        for (var i = 0; i < members.Length; i++)
        {
            var performer = Performers.Build(members[i].Role, i + 1 + (_scene * 3));
            performer.Position = stage + new Vector3(members[i].X, deck, members[i].Z + shift);
            _set.AddChild(performer);
        }
    }

    /// <summary>A crowd in front of the stage: tight at the front, looser further back, some with arms up.</summary>
    private void Audience(Vector3 stage, float halfDepth, int count, float halfWidth, bool night)
    {
        var random = new Random(41 + _scene);
        for (var i = 0; i < count; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(7000 + i));
            var deep = Math.Pow(random.NextDouble(), 1.3) * (5 + (count / 14.0));
            var across = (random.NextDouble() - 0.5) * 2 * (halfWidth * 0.55 + (deep * 0.55));
            person.Position = new Vector3(stage.X + (float)across, 0, stage.Z + halfDepth + 1.6f + (float)deep);
            person.RotationDegrees = new Vector3(0, 180 + ((float)random.NextDouble() * 20) - 10 + ((float)across * 2.5f), 0);
            person.SetPose(random.NextDouble() < (night ? 0.5 : 0.3) ? Pose.Cheering : Pose.Standing);
            _set.AddChild(person);
        }
    }

    /// <summary>Stage lights: coloured spots on the band, beams in the air and a wash over the front of the crowd.</summary>
    private void Lights(Vector3 stage, float strength, bool dj)
    {
        var colours = dj
            ? new[] { new Color("#7B4FE9"), new Color("#2FD7E8"), new Color("#E94F7B"), new Color("#2FD7E8"), new Color("#7B4FE9") }
            : new[] { new Color("#F6B93B"), new Color("#E94F7B"), new Color("#FFF1C9"), new Color("#E94F7B"), new Color("#F6B93B") };
        for (var i = 0; i < colours.Length; i++)
        {
            var x = (i - 2) * 1.9f;
            var from = stage + new Vector3(x, 6.3f, 2.6f);
            var spot = new SpotLight3D
            {
                LightColor = colours[i],
                LightEnergy = 9f * strength,
                SpotRange = 14f,
                SpotAngle = 32f,
                ShadowEnabled = false,
                Position = from,
            };
            _set.AddChild(spot);
            spot.LookAt(stage + new Vector3(x * 0.8f, 1.3f, 0.2f), Vector3.Up);

            // A beam through the haze, pointing out over the crowd.
            var beam = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 1.3f, Height = 11f, RadialSegments = 12, Rings = 1 },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                    AlbedoColor = new Color(colours[i], 0.4f * strength),
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            var tilt = new Vector3(52, (i - 2) * 16, 0);
            beam.RotationDegrees = tilt;
            beam.Position = from + (Basis.FromEuler(tilt * (Mathf.Pi / 180f)) * new Vector3(0, -5.5f, 0)) + new Vector3(0, 0.2f, 0.5f);
            _set.AddChild(beam);
        }

        _set.AddChild(new OmniLight3D { LightColor = dj ? new Color("#9C7BFF") : new Color("#FFD9A0"), LightEnergy = 5f * strength, OmniRange = 17f, Position = stage + new Vector3(0, 5.5f, 8f) });
        _set.AddChild(new OmniLight3D { LightColor = new Color("#FFE8C0"), LightEnergy = 3f * strength, OmniRange = 9f, Position = stage + new Vector3(0, 4.5f, 1f) });
    }
}
