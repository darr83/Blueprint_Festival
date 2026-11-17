using System;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Review pictures of the stars' stage looks, run with: godot --path game -- --star-shots=DIR
/// (needs a display). A woman and a man for each kind of music, from the front and the back, then
/// closer up, and a five-star headliner beside a one-star local act.
/// </summary>
public partial class StarShots : Node
{
    private static readonly string[] Names = { "1_every_genre_front", "2_every_genre_back", "3_country_rock_metal", "4_pop_electronic_hip_hop", "5_star_or_local", "6_indie_punk_folk", "7_reggae_soul_disco" };

    private readonly string _directory;
    private int _scene = -1;
    private int _frame;
    private FestivalScene? _festival;
    private Node3D? _set;

    public StarShots(string directory)
    {
        _directory = directory;
    }

    public StarShots() : this("shots")
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
            GD.Print($"[stars] {path}");
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
            GD.Print("[stars] Done.");
            GetTree().Quit(0);
            return;
        }

        var festival = _festival!;
        var field = festival.Game.Field.Field;
        var centre = new Vector3((field.X + (field.Width / 2f)) * 0.5f, 0, (field.Y + (field.Height / 2f)) * 0.5f);
        _set = new Node3D { Name = "Set", Position = centre, RotationDegrees = new Vector3(0, 45, 0) };
        festival.AddChild(_set);

        var genres = Enum.GetValues<Genre>();
        switch (_scene)
        {
            case 0:
            case 1:
                Rows(genres, _scene == 1, 1.5f);
                festival.Camera.LookCloser(centre.X - 0.75f, centre.Z - 0.75f, 6.2f);
                break;
            case 2:
                Rows(new[] { Genre.Country, Genre.Rock, Genre.Metal }, false, 1.25f);
                festival.Camera.LookCloser(centre.X - 0.55f, centre.Z - 0.55f, 2.9f);
                break;
            case 3:
                Rows(new[] { Genre.Pop, Genre.Electronic, Genre.HipHop }, false, 1.25f);
                festival.Camera.LookCloser(centre.X - 0.55f, centre.Z - 0.55f, 2.9f);
                break;
            case 4:
                StarOrLocal();
                festival.Camera.LookCloser(centre.X - 0.5f, centre.Z - 0.5f, 3.4f);
                break;
            case 5:
                Rows(new[] { Genre.Indie, Genre.Punk, Genre.Folk }, false, 1.25f);
                festival.Camera.LookCloser(centre.X - 0.55f, centre.Z - 0.55f, 2.9f);
                break;
            case 6:
                Rows(new[] { Genre.Reggae, Genre.Soul, Genre.Disco }, false, 1.25f);
                festival.Camera.LookCloser(centre.X - 0.55f, centre.Z - 0.55f, 2.9f);
                break;
        }

        festival.Camera.Snap();
    }

    /// <summary>A row of women in front and a row of men behind, one of each per genre.</summary>
    private void Rows(Genre[] genres, bool fromBehind, float gap)
    {
        for (var g = 0; g < genres.Length; g++)
        {
            var x = (g - ((genres.Length - 1) / 2f)) * gap;
            var role = genres[g] == Genre.Electronic ? BandRole.Dj : BandRole.Singer;
            Place(StarLooks.For(genres[g], role, true, 3, 11 + g), x - 0.3f, -0.4f, fromBehind, null);
            Place(StarLooks.For(genres[g], role == BandRole.Dj ? role : BandRole.Guitarist, false, 3, 23 + g), x + 0.3f, -0.4f, fromBehind, null);
            Label(Roster.NameOf(genres[g]), x, 0.35f);
        }
    }

    /// <summary>The same kind of act as a local turn and as a headliner: the headliner wears more shine.</summary>
    private void StarOrLocal()
    {
        var genres = new[] { Genre.Pop, Genre.Rock, Genre.HipHop };
        for (var g = 0; g < genres.Length; g++)
        {
            var x = (g - 1) * 1.9f;
            Place(StarLooks.For(genres[g], BandRole.Singer, g != 1, 1, 40 + g), x - 0.4f, 0, false, null);
            Place(StarLooks.For(genres[g], BandRole.Singer, g != 1, 5, 40 + g), x + 0.4f, 0, false, null);
            Label($"{Roster.NameOf(genres[g])}: 1 star, 5 stars", x, 0.45f);
        }
    }

    private void Place(PersonLook look, float x, float z, bool fromBehind, Pose? pose)
    {
        var person = PersonView.Build(look);
        person.Position = new Vector3(x, 0, z);
        person.RotationDegrees = new Vector3(0, fromBehind ? 180 : 0, 0);
        person.SetPose(pose ?? Pose.Standing);
        _set!.AddChild(person);
    }

    private void Label(string text, float x, float z) =>
        _set!.AddChild(new Label3D
        {
            Text = text,
            FontSize = 36,
            PixelSize = 0.0042f,
            OutlineSize = 10,
            OutlineModulate = new Color(0.1f, 0.2f, 0.1f, 0.9f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Position = new Vector3(x, 0.02f, z),
        });
}
