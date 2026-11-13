using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Review pictures of the people, run with: godot --path game -- --people-shots=DIR (needs a display).
/// A named line-up from the front and the back, forty people straight from the generator, a wet-day
/// set, and a crowd standing in the field at the distance the game is played from.
/// </summary>
public partial class PeopleShots : Node
{
    private static readonly string[] Names = { "1_line_up_front", "2_line_up_back", "3_forty_at_random", "4_wet_day", "5_crowd_in_field", "6_crowd_closer" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private FestivalScene? _festival;
    private Node3D? _set;

    public PeopleShots(string directory)
    {
        _directory = directory;
    }

    public PeopleShots() : this("shots")
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
            GD.Print($"[people] {path}");
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
            GD.Print("[people] Done.");
            GetTree().Quit(0);
            return;
        }

        var festival = _festival!;
        var field = festival.Game.Field.Field;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);

        // A stand turned to face the camera: along its X is across the screen, +Z is towards the viewer.
        _set = new Node3D { Name = "Set", Position = centre, RotationDegrees = new Vector3(0, 45, 0) };
        festival.AddChild(_set);

        switch (_scene)
        {
            case 0:
                LineUp(false);
                festival.Camera.LookAt(centre.X + 1.0f, centre.Z + 1.0f, 7.6f);
                break;
            case 1:
                LineUp(true);
                festival.Camera.LookAt(centre.X + 1.0f, centre.Z + 1.0f, 7.6f);
                break;
            case 2:
                Grid(40, 10, raining: false);
                festival.Camera.LookAt(centre.X + 0.5f, centre.Z + 0.5f, 10.5f);
                break;
            case 3:
                Grid(20, 10, raining: true);
                festival.Camera.LookAt(centre.X + 0.5f, centre.Z + 0.5f, 8.2f);
                break;
            case 4:
                Crowd();
                festival.Camera.ShowWholeField();
                break;
            case 5:
                Crowd();
                festival.Camera.LookAt(centre.X + 9, centre.Z - 1, 24f);
                break;
        }

        festival.Camera.Snap();
    }

    private static PersonLook Adult(int seed, bool female) =>
        LookGenerator.FestivalGoer(seed, AgeGroup.Adult) with { Female = female, Beard = false, Sunglasses = false, Hat = Hat.None, Poncho = null, TopDetail = null, Build = 1, Height = female ? 0.96 : 1.02 };

    private void LineUp(bool fromBehind)
    {
        var s = LookGenerator.SkinTones;
        var rowOne = new (string Label, PersonLook Look, Pose Pose, float Stride)[]
        {
            ("T-shirt", Adult(1, false) with { Skin = s[1], Hair = HairStyle.Short, HairColour = "#6B4A2E", Top = TopStyle.TShirt, TopColour = "#E94F7B", TopDetail = "#F6B93B", Legs = LegStyle.Shorts, LegColour = "#3F5B8C", Feet = Footwear.Trainers, FeetColour = "#F2F2F2" }, Pose.Standing, 0),
            ("Summer dress", Adult(2, true) with { Skin = s[4], Hair = HairStyle.Long, HairColour = "#1E1A17", Top = TopStyle.Dress, TopColour = "#F6B93B", Legs = LegStyle.Skirt, LegColour = "#F6B93B", Feet = Footwear.Sandals, FeetColour = "#8A6A45" }, Pose.Standing, 0),
            ("Bucket hat", Adult(3, true) with { Skin = s[0], Hair = HairStyle.Bob, HairColour = "#C9A25A", Top = TopStyle.TShirt, TopColour = "#F7F3EA", Legs = LegStyle.Shorts, LegColour = "#5B7DB1", Feet = Footwear.Wellies, FeetColour = "#E94F7B", Hat = Hat.Bucket, HatColour = "#E94F7B" }, Pose.Standing, 0),
            ("Cowboy hat", Adult(4, false) with { Skin = s[3], Hair = HairStyle.Short, HairColour = "#2E2019", Top = TopStyle.Vest, TopColour = "#36B7B0", Legs = LegStyle.Shorts, LegColour = "#C9B38A", Feet = Footwear.Boots, FeetColour = "#5A4636", Hat = Hat.Cowboy, HatColour = "#D4B77A", Build = 1.15 }, Pose.Standing, 0),
            ("Cap, shades", Adult(5, false) with { Skin = s[5], Hair = HairStyle.Crop, HairColour = "#1E1A17", Top = TopStyle.Stripes, TopColour = "#F5F5F5", TopDetail = "#2F4B7C", Legs = LegStyle.Trousers, LegColour = "#3A3A3F", Feet = Footwear.Trainers, FeetColour = "#D9483B", Hat = Hat.Cap, HatColour = "#4FA8D8", Sunglasses = true }, Pose.Standing, 0),
            ("Teenager", LookGenerator.FestivalGoer(6, AgeGroup.Teenager) with { Female = false, Skin = s[2], Hair = HairStyle.Curly, HairColour = "#4A3426", Top = TopStyle.Hoodie, TopColour = "#C59BE0", TopDetail = null, Legs = LegStyle.Trousers, LegColour = "#2C3E57", Feet = Footwear.Trainers, FeetColour = "#F2F2F2", Hat = Hat.None, Sunglasses = false, Poncho = null, Height = 0.88, Build = 0.9 }, Pose.Standing, 0),
            ("Beard", LookGenerator.FestivalGoer(7, AgeGroup.Older) with { Female = false, Skin = s[1], Hair = HairStyle.Short, HairColour = "#BDBDBA", Beard = true, Top = TopStyle.LongSleeve, TopColour = "#3C8DAD", TopDetail = null, Legs = LegStyle.Trousers, LegColour = "#7A6A55", Feet = Footwear.Boots, FeetColour = "#5A4636", Hat = Hat.None, Sunglasses = false, Poncho = null, Height = 1.04, Build = 1.2 }, Pose.Standing, 0),
            ("Sun hat", LookGenerator.FestivalGoer(8, AgeGroup.Older) with { Female = true, Skin = s[0], Hair = HairStyle.Bob, HairColour = "#E4E2DC", Beard = false, Top = TopStyle.LongSleeve, TopColour = "#F2A7C3", TopDetail = null, Legs = LegStyle.Trousers, LegColour = "#E8E0D0", Feet = Footwear.Sandals, FeetColour = "#8A6A45", Hat = Hat.Sunhat, HatColour = "#E2C992", Sunglasses = false, Poncho = null, Height = 0.95, Build = 1.05 }, Pose.Standing, 0),
            ("Afro", Adult(9, true) with { Skin = s[5], Hair = HairStyle.Afro, HairColour = "#1E1A17", Top = TopStyle.Vest, TopColour = "#F2836B", Legs = LegStyle.Skirt, LegColour = "#2C3E57", Feet = Footwear.Trainers, FeetColour = "#F6B93B" }, Pose.Standing, 0),
            ("Child", LookGenerator.FestivalGoer(10, AgeGroup.Child) with { Female = false, Skin = s[3], Hair = HairStyle.Short, HairColour = "#2E2019", Top = TopStyle.Stripes, TopColour = "#FFE08A", TopDetail = "#D9483B", Legs = LegStyle.Shorts, LegColour = "#3F5B8C", Feet = Footwear.Trainers, FeetColour = "#F2F2F2", Hat = Hat.None, Poncho = null, Height = 0.66 }, Pose.Standing, 0),
            ("Small child", LookGenerator.FestivalGoer(11, AgeGroup.Child) with { Female = true, Skin = s[1], Hair = HairStyle.Ponytail, HairColour = "#B5542A", Top = TopStyle.Dress, TopColour = "#8FD3C8", TopDetail = null, Legs = LegStyle.Skirt, LegColour = "#8FD3C8", Feet = Footwear.Wellies, FeetColour = "#F6B93B", Hat = Hat.None, Poncho = null, Height = 0.57 }, Pose.Standing, 0),
        };

        var rowTwo = new (string Label, PersonLook Look, Pose Pose, float Stride)[]
        {
            ("Walking", Adult(21, true) with { Skin = s[2], Hair = HairStyle.Ponytail, HairColour = "#4A3426", Top = TopStyle.TShirt, TopColour = "#7FB7E6", Legs = LegStyle.Trousers, LegColour = "#3F5B8C", Feet = Footwear.Trainers, FeetColour = "#E94F7B" }, Pose.Walking, 0.25f),
            ("Walking", Adult(22, false) with { Skin = s[6], Hair = HairStyle.Bald, Beard = true, HairColour = "#1E1A17", Top = TopStyle.TShirt, TopColour = "#F6B93B", Legs = LegStyle.Shorts, LegColour = "#3A3A3F", Feet = Footwear.Wellies, FeetColour = "#2F6B4A" }, Pose.Walking, 0.75f),
            ("Cheering", Adult(23, true) with { Skin = s[0], Hair = HairStyle.Bun, HairColour = "#E94F7B", Top = TopStyle.Vest, TopColour = "#F5F5F5", Legs = LegStyle.Shorts, LegColour = "#8FA9C9", Feet = Footwear.Boots, FeetColour = "#2E2E33" }, Pose.Cheering, 0),
            ("Cheering", Adult(24, false) with { Skin = s[4], Hair = HairStyle.Long, HairColour = "#2E2019", Top = TopStyle.TShirt, TopColour = "#B8336A", TopDetail = "#FFE08A", Legs = LegStyle.Trousers, LegColour = "#5B7DB1", Feet = Footwear.Trainers, FeetColour = "#2E2E33", Sunglasses = true }, Pose.Cheering, 0),
            ("Rain poncho", Adult(25, true) with { Skin = s[3], Hair = HairStyle.Bob, Top = TopStyle.LongSleeve, TopColour = "#2F4B7C", Legs = LegStyle.Trousers, LegColour = "#3F5B8C", Feet = Footwear.Wellies, FeetColour = "#D9483B", Poncho = "#F6D84A" }, Pose.Standing, 0),
            ("Rain poncho", Adult(26, false) with { Skin = s[1], Beard = true, HairColour = "#6B4A2E", Top = TopStyle.Hoodie, TopColour = "#2F4B7C", Legs = LegStyle.Shorts, LegColour = "#3A3A3F", Feet = Footwear.Wellies, FeetColour = "#2C3E57", Poncho = "#6FC3E8", Build = 1.2 }, Pose.Walking, 0.25f),
            ("Rain poncho", LookGenerator.FestivalGoer(27, AgeGroup.Child) with { Skin = s[5], Top = TopStyle.TShirt, TopColour = "#F5F5F5", Legs = LegStyle.Trousers, LegColour = "#3F5B8C", Feet = Footwear.Wellies, FeetColour = "#F6B93B", Poncho = "#F29BBB", Hat = Hat.None, Height = 0.63 }, Pose.Cheering, 0),
            ("Broad build", Adult(28, false) with { Skin = s[2], Hair = HairStyle.Crop, HairColour = "#8E6A3E", Top = TopStyle.TShirt, TopColour = "#4FA8D8", Legs = LegStyle.Shorts, LegColour = "#7A6A55", Feet = Footwear.Sandals, FeetColour = "#8A6A45", Build = 1.25, Height = 1.06 }, Pose.Standing, 0),
            ("Slight build", Adult(29, true) with { Skin = s[6], Hair = HairStyle.Curly, HairColour = "#1E1A17", Top = TopStyle.Stripes, TopColour = "#F2A7C3", TopDetail = "#F7F3EA", Legs = LegStyle.Trousers, LegColour = "#E8E0D0", Feet = Footwear.Trainers, FeetColour = "#3F5B8C", Build = 0.85, Height = 0.93 }, Pose.Standing, 0),
            ("Dyed hair", Adult(30, false) with { Skin = s[0], Hair = HairStyle.Short, HairColour = "#4FB8D8", Top = TopStyle.Vest, TopColour = "#2F4B7C", Legs = LegStyle.Shorts, LegColour = "#C9B38A", Feet = Footwear.Boots, FeetColour = "#2E2E33" }, Pose.Standing, 0),
        };

        PlaceRow(rowOne, -1.5f, fromBehind);
        PlaceRow(rowTwo, 2.6f, fromBehind);
    }

    private void PlaceRow((string Label, PersonLook Look, Pose Pose, float Stride)[] row, float z, bool fromBehind)
    {
        const float gap = 1.16f;
        for (var i = 0; i < row.Length; i++)
        {
            var x = (i - ((row.Length - 1) / 2f)) * gap;
            var person = PersonView.Build(row[i].Look);
            person.Position = new Vector3(x, 0, z);
            person.RotationDegrees = new Vector3(0, fromBehind ? 180 : 0, 0);
            person.SetPose(row[i].Pose, row[i].Stride);
            _set!.AddChild(person);
            _set.AddChild(new Label3D
            {
                Text = row[i].Label,
                FontSize = 36,
                PixelSize = 0.0042f,
                OutlineSize = 10,
                OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(x, 0.02f, z + 0.95f),
            });
        }
    }

    private void Grid(int count, int perRow, bool raining)
    {
        var rows = (int)Math.Ceiling(count / (float)perRow);
        for (var i = 0; i < count; i++)
        {
            var column = i % perRow;
            var row = i / perRow;
            var person = PersonView.Build(LookGenerator.FestivalGoer(100 + i + (raining ? 500 : 0), raining));
            person.Position = new Vector3((column - ((perRow - 1) / 2f)) * 1.45f, 0, (row - ((rows - 1) / 2f)) * 3.4f);
            _set!.AddChild(person);
        }
    }

    /// <summary>
    /// A village festival's worth of people: most gathered at the east end where a stage would
    /// stand, the rest dotted about the field walking somewhere.
    /// </summary>
    private void Crowd()
    {
        _set!.RotationDegrees = Vector3.Zero;
        var random = new Random(77);
        var field = _festival!.Game.Field.Field;
        var people = new List<PersonView>();
        for (var i = 0; i < 150; i++)
        {
            var person = PersonView.Build(LookGenerator.FestivalGoer(1000 + i));
            Vector3 at;
            if (i < 95)
            {
                // Packed loosely in front of the stage, facing it (east).
                var deep = Math.Pow(random.NextDouble(), 0.8) * 13;
                var across = (random.NextDouble() - 0.5) * (9 + deep);
                at = new Vector3((field.Width * 0.25f) - 6 - (float)deep, 0, (float)across);
                person.RotationDegrees = new Vector3(0, 90 + ((float)random.NextDouble() * 24) - 12, 0);
                person.SetPose(random.NextDouble() < 0.35 ? Pose.Cheering : Pose.Standing);
            }
            else
            {
                at = new Vector3(((float)random.NextDouble() - 0.5f) * (field.Width * 0.5f - 3), 0, ((float)random.NextDouble() - 0.5f) * (field.Height * 0.5f - 3));
                person.RotationDegrees = new Vector3(0, (float)random.NextDouble() * 360, 0);
                person.SetPose(random.NextDouble() < 0.7 ? Pose.Walking : Pose.Standing, (float)random.NextDouble());
            }

            person.Position = at;
            people.Add(person);
            _set.AddChild(person);
        }
    }
}
