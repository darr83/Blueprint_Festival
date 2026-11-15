using System;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Pictures of the seats, hand washing, shade and flooring, run with:
/// godot --path game -- --comfort-shots=DIR (needs a display).
/// </summary>
public partial class ComfortShots : Node
{
    private static readonly string[] Scenes =
    {
        "1_the_seats", "2_clean_and_comfortable", "3_the_flooring", "4_a_place_to_eat", "5_the_seating_marquee", "6_by_the_toilets",
    };

    private static readonly Carried[] Food = { Carried.Burger, Carried.Fries, Carried.Pizza, Carried.Noodles, Carried.HotDog, Carried.Cup, Carried.Coffee, Carried.IceCream, Carried.None, Carried.None };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private bool _ready;
    private int _people;

    public ComfortShots(string directory)
    {
        _directory = directory;
    }

    public ComfortShots() : this("shots")
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
                Arrange(f);
                _ready = true;
                _frame = 0;
            }

            return;
        }

        if (_frame == 60)
        {
            var path = $"{_directory}/{Scenes[_scene]}.png";
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"[comfort] {path}");
            NextScene();
        }
    }

    private void NextScene()
    {
        _scene++;
        while (_scene < Scenes.Length && System.Environment.GetEnvironmentVariable("SHOTS_ONLY") is { Length: > 0 } only && !Scenes[_scene].Contains(only))
        {
            _scene++;
        }

        _ready = false;
        _frame = 0;
        if (_scene >= Scenes.Length)
        {
            GD.Print("[comfort] Done.");
            GetTree().Quit(0);
            return;
        }

        GameManager.Instance.StartNewGame(null, null, 9);
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        game.SetWeather(DayWeather.Of(WeatherKind.Fine, 3));
        if (_scene == 3)
        {
            var x = f.X + 30;
            foreach (var kind in new[] { StallKind.Burgers, StallKind.Pizza, StallKind.Noodles })
            {
                game.BuildStall(kind, x, f.Y + 14, Facing.South);
                x += Catalogue.Of(kind).Width + 6;
            }
        }
        else if (_scene == 5)
        {
            game.BuildStall(StallKind.ToiletBlock, f.X + 30, f.Y + 16, Facing.South);
        }

        var until = game.Now.Date.AddHours(_scene == 4 ? 15.5 : 13.5);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Paused);
        festival.SetHudShown(false);
        festival.Crowd.Visible = false;
        festival.Litter.Visible = false;
        const float t = (float)FieldMap.TileMetres;
        var left = (f.X * t) + 14;
        var top = (f.Y * t) + 12;
        _people = 0;
        switch (_scene)
        {
            case 0:
            {
                var kinds = new[] { ComfortKind.StrawBale, ComfortKind.Bench, ComfortKind.Deckchairs, ComfortKind.PicnicTable, ComfortKind.ParasolTable };
                var x = left;
                foreach (var kind in kinds)
                {
                    var info = Comforts.Of(kind);
                    var width = info.Width * t;
                    Put(festival, kind, x + (width / 2), top, 0, info.Seats.Length - (kind == ComfortKind.PicnicTable ? 1 : 0));
                    Label(festival, $"{info.Name}\n£{info.Cost}", x + (width / 2), top + 2.0f);
                    x += width + 1.6f;
                }

                festival.Camera.LookCloser(left + 6.9f, top + 0.8f, 9f);
                break;
            }

            case 1:
            {
                var kinds = new[] { ComfortKind.HandGel, ComfortKind.HandWash, ComfortKind.ChargingTent, ComfortKind.ShadeSail };
                var x = left;
                foreach (var kind in kinds)
                {
                    var info = Comforts.Of(kind);
                    var width = Math.Max(1.4f, info.Width * t);
                    Put(festival, kind, x + (width / 2), top, 0, 0);
                    Label(festival, $"{info.Name}\n£{info.Cost}", x + (width / 2), top + (info.Depth * t / 2) + 1.2f);
                    x += width + 1.8f;
                }

                Stand(festival, left + 0.7f, top + 0.6f, 180, Carried.None);
                Stand(festival, left + 3.7f, top + 0.9f, 180, Carried.None);
                Stand(festival, left + 4.6f, top + 0.9f, 180, Carried.None);
                Stand(festival, left + 8.6f, top + 2.0f, 180, Carried.None);
                foreach (var (dx, dz) in new[] { (-1.2f, -0.6f), (0.2f, 0.4f), (1.1f, -1.0f), (-0.3f, -1.4f), (0.9f, 1.2f) })
                {
                    Stand(festival, left + 14.2f + dx, top + dz, 30 + (dx * 80), Food[_people % Food.Length]);
                }

                festival.Camera.LookCloser(left + 8.2f, top + 0.5f, 10.5f);
                break;
            }

            case 2:
            {
                var names = new[] { "Wooden pallets\n£3 per 2 m", "Sand\n£5 per 2 m", "Rubber mats\n£6 per 2 m", "Artificial grass\n£8 per 2 m", "Boardwalk\n£9 per 2 m" };
                var b = new MeshBuilder();
                for (var kind = 0; kind < 5; kind++)
                {
                    for (var i = 0; i < 6; i++)
                    {
                        var wide = kind is 1 or 3 && i >= 3;
                        var x = left + (kind * 4.2f) + (wide ? 2 : 0);
                        var z = top - 4 + ((wide ? i - 3 : i) * 2f);
                        if (kind is 1 or 3 || i < 6)
                        {
                            SiteProps.Flooring(b, kind, x, z, 2f, (kind * 31) + (i * 17), false);
                        }
                    }

                    Label(festival, names[kind], left + (kind * 4.2f) + (kind is 1 or 3 ? 1 : 0), top + (kind is 1 or 3 ? 2.9f : 8.9f));
                }

                var floor = b.BuildNode("Flooring");
                festival.AddChild(floor);
                Put(festival, ComfortKind.Deckchairs, left + 5.2f, top - 1, 0, 3);
                Put(festival, ComfortKind.ParasolTable, left + 13.6f, top - 0.4f, 0, 3);
                Stand(festival, left, top + 3, 0, Carried.Cup, walking: true);
                Stand(festival, left + 8.4f, top - 2, 0, Carried.Pint, walking: true);
                Stand(festival, left + 8.5f, top + 4, 180, Carried.None, walking: true);
                Stand(festival, left + 16.8f, top + 1, 0, Carried.Fries, walking: true);
                Stand(festival, left + 16.6f, top + 5, 180, Carried.None, walking: true);
                festival.Camera.LookCloser(left + 8.4f, top + 1.6f, 14f);
                break;
            }

            case 3:
            {
                // A place to eat: food stalls along the back, tables in front of them, bins and a hand gel post.
                var cx = (f.X * t) + 23;
                var cz = (f.Y * t) + 15;
                var b = new MeshBuilder();
                for (var i = 0; i < 9; i++)
                {
                    for (var j = 0; j < 4; j++)
                    {
                        SiteProps.Flooring(b, 2, cx - 8 + (i * 2f), cz - 3 + (j * 2f), 2f, (i * 13) + j, false);
                    }
                }

                festival.AddChild(b.BuildNode("Flooring"));
                Put(festival, ComfortKind.PicnicTable, cx - 6, cz, 0, 5);
                Put(festival, ComfortKind.PicnicTable, cx - 3, cz, 0, 6);
                Put(festival, ComfortKind.PicnicTable, cx - 6, cz + 2.6f, 0, 4);
                Put(festival, ComfortKind.PicnicTable, cx - 3, cz + 2.6f, 0, 6);
                Put(festival, ComfortKind.ParasolTable, cx + 1, cz + 0.2f, 0, 4);
                Put(festival, ComfortKind.ParasolTable, cx + 4.4f, cz + 0.2f, 0, 3);
                Put(festival, ComfortKind.ParasolTable, cx + 1, cz + 3.2f, 0, 2);
                Put(festival, ComfortKind.ParasolTable, cx + 4.4f, cz + 3.2f, 0, 4);
                Put(festival, ComfortKind.Bench, cx + 8.6f, cz + 1.5f, -90, 2);
                Put(festival, ComfortKind.HandGel, cx - 8.6f, cz - 3.4f, 0, 0);
                Put(festival, ComfortKind.HandGel, cx + 7.4f, cz - 3.4f, 0, 0);
                var bin = SiteProps.Stall(StallKind.Bin);
                bin.Position = new Vector3(cx - 8.4f, 0, cz + 1.4f);
                festival.AddChild(bin);
                Stand(festival, cx - 1.2f, cz - 2.6f, 200, Carried.Burger, walking: true);
                Stand(festival, cx + 3, cz - 2.4f, 150, Carried.Pizza, walking: true);
                Stand(festival, cx + 6.4f, cz + 5.4f, 300, Carried.None, walking: true);
                festival.Camera.LookCloser(cx - 0.4f, cz + 0.4f, 12.5f);
                break;
            }

            case 4:
            {
                var cx = (f.X * t) + 22;
                var cz = (f.Y * t) + 14;
                game.SetWeather(DayWeather.Of(WeatherKind.Rain, 3));
                Put(festival, ComfortKind.SeatingMarquee, cx, cz, 0, 19);
                Label(festival, "Seating marquee\n£250", cx, cz + 4.6f);
                Stand(festival, cx + 5.5f, cz + 2, 250, Carried.Coffee, walking: true);
                festival.Camera.LookCloser(cx, cz + 0.4f, 9.5f);
                break;
            }

            default:
            {
                var cx = (f.X * t) + 21;
                var cz = (f.Y * t) + 12;
                var b = new MeshBuilder();
                for (var i = 0; i < 9; i++)
                {
                    SiteProps.Flooring(b, 0, cx - 6 + (i * 2f), cz - 1f, 2f, i * 7, false);
                }

                for (var i = 0; i < 5; i++)
                {
                    SiteProps.Flooring(b, 4, cx - 1, cz + 1f + (i * 2f), 2f, i * 5, false);
                }

                festival.AddChild(b.BuildNode("Flooring"));
                Put(festival, ComfortKind.HandWash, cx + 9, cz - 1.2f, 0, 0);
                Put(festival, ComfortKind.HandWash, cx + 11.4f, cz - 1.2f, 0, 0);
                Put(festival, ComfortKind.HandGel, cx - 7.6f, cz - 1, 0, 0);
                Put(festival, ComfortKind.Bench, cx + 3, cz + 4, 0, 2);
                Put(festival, ComfortKind.StrawBale, cx - 4, cz + 4, 0, 2);
                Put(festival, ComfortKind.StrawBale, cx - 5.4f, cz + 4.6f, 40, 1);
                Stand(festival, cx + 8.6f, cz - 0.5f, 180, Carried.None);
                Stand(festival, cx + 11.8f, cz - 0.5f, 180, Carried.None);
                Stand(festival, cx - 1, cz + 5, 180, Carried.None, walking: true);
                Stand(festival, cx + 2, cz - 1, 90, Carried.None, walking: true);
                Stand(festival, cx - 4, cz - 1, 270, Carried.None, walking: true);
                festival.Camera.LookCloser(cx + 2, cz + 1.4f, 12.5f);
                break;
            }
        }

        festival.Camera.Snap();
        GD.Print($"[comfort] {Scenes[_scene]}");
    }

    /// <summary>Stands a comfort in the field with people sitting on some of its seats.</summary>
    private void Put(FestivalScene festival, ComfortKind kind, float x, float z, float turn, int sitters)
    {
        var node = SiteProps.Comfort(kind);
        node.Position = new Vector3(x, 0, z);
        node.RotationDegrees = new Vector3(0, -turn, 0);
        festival.AddChild(node);
        var seats = Comforts.Of(kind).Seats;
        for (var i = 0; i < seats.Length && sitters > 0; i++)
        {
            if (seats.Length - i > sitters && ((i * 7) + _people) % 3 == 0)
            {
                continue;
            }

            sitters--;
            var seat = seats[i];
            var person = PersonView.Build(LookGenerator.FestivalGoer(300 + (_people * 13), _people % 6 == 5 ? AgeGroup.Child : _people % 4 == 0 ? AgeGroup.Older : AgeGroup.Adult));
            person.SeatHeight = (float)seat.Height;
            person.Position = new Vector3((float)seat.X, 0, (float)seat.Y);
            person.RotationDegrees = new Vector3(0, -(float)seat.Turn, 0);
            node.AddChild(person);
            person.Hold(kind is ComfortKind.Bench or ComfortKind.StrawBale or ComfortKind.Deckchairs ? (_people % 3 == 0 ? Carried.Cup : Carried.None) : Food[_people % Food.Length]);
            person.SetPose(Pose.Sitting);
            _people++;
        }
    }

    private void Stand(FestivalScene festival, float x, float z, float turn, Carried holding, bool walking = false)
    {
        var person = PersonView.Build(LookGenerator.FestivalGoer(900 + (_people * 17)));
        person.Position = new Vector3(x, 0, z);
        person.RotationDegrees = new Vector3(0, -turn, 0);
        festival.AddChild(person);
        person.Hold(holding);
        person.SetPose(walking ? Pose.Walking : Pose.Standing, 0.2f + ((_people % 5) * 0.12f));
        _people++;
    }

    private void Label(FestivalScene festival, string text, float x, float z)
    {
        festival.AddChild(new Label3D
        {
            Text = text,
            FontSize = 40,
            PixelSize = _scene == 2 ? 0.0085f : _scene == 1 ? 0.0065f : 0.0052f,
            OutlineSize = 12,
            OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(x, 0.05f, z),
        });
    }
}
