using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.Core;

/// <summary>
/// Films a band playing to its music, run with:
/// godot --path game --write-movie FILE.avi --fixed-fps 30 -- --band-shots=DIR (needs a display).
/// A rock band plays: the whole band, then the drummer, the singer and the guitarist close up; the
/// end of a song, the gap and the start of the next; then the end of the set, with the song fading,
/// the crowd clapping and cheering the band off, and drifting away. Then a DJ. Songs in the acts'
/// music folders are played; a still of each shot is saved in DIR.
/// </summary>
public partial class BandShots : Node
{
    private const string Band = "Little Ferrets";
    private const string Dj = "DJ Magpie";

    private readonly string _directory;
    private readonly List<(string Name, int Frames, Action<FestivalScene> Start)> _shots = new();
    private int _shot = -1;
    private int _frame;
    private string _act = "";
    private bool _ready;
    private Vector3 _stage;
    private Camera3D? _film;

    public BandShots(string directory)
    {
        _directory = directory;
        _shots.Add(("1_the_whole_band", 180, f => Front(f, null, 9f, 40)));
        _shots.Add(("2_the_drummer", 180, f => Front(f, BandRole.Drummer, 3.2f, null)));
        _shots.Add(("3_the_singer", 150, f => Front(f, BandRole.Singer, 2.2f, null)));
        _shots.Add(("4_the_guitarist", 120, f => Front(f, BandRole.Guitarist, 2.6f, null)));
        _shots.Add(("5_end_of_a_song", 330, f =>
        {
            Look(f, null, 15f, null);
            f.Music.SkipToEnd(4.5);
        }));
        _shots.Add(("6_end_of_the_set", 1200, f =>
        {
            Look(f, null, 22f, null);
            var game = f.Game;
            var set = game.Show.Playing(game.Now)!;
            while (game.Now < set.End - TimeSpan.FromSeconds(80))
            {
                game.Clock.Step();
            }

            f.Crowd.Snap();
        }));
        _shots.Add(("7_the_dj", 180, f => Front(f, BandRole.Dj, 3f, 40)));
    }

    public BandShots() : this("shots")
    {
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_directory);
        Callable.From(NextShot).CallDeferred();
    }

    public override void _Process(double delta)
    {
        if (_shot < 0 || _shot >= _shots.Count)
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

        if (GetTree().CurrentScene is not FestivalScene festival)
        {
            return;
        }

        var (name, frames, start) = _shots[_shot];
        if (_frame == 1)
        {
            start(festival);
            GD.Print($"[band] {name} starts at frame {Engine.GetFramesDrawn() + 1}");
        }

        if (_frame == 1 + (frames / 2) || (name.StartsWith("6", StringComparison.Ordinal) && _frame % 150 == 0))
        {
            GetViewport().GetTexture().GetImage().SavePng($"{_directory}/{name}_{_frame:0000}.png");
        }

        if (_frame % 30 == 0)
        {
            var beat = festival.Acts.Beat;
            GD.Print($"[band] {name} f{_frame}: {beat.Moment}, beat {beat.Beat:0.00}, voice {beat.Voice:0.00}, song '{festival.Music.SongTitle}', "
                + $"{festival.Game.Crowd.Watching} watching, {festival.Crowd.Dancers} dancing, {festival.Crowd.Applauding} applauding, applause {festival.Crowd.ApplauseLevel:0.00}, {festival.Game.Now:HH:mm:ss}");
        }

        if (_frame == 1 + frames)
        {
            NextShot();
        }
    }

    /// <summary>
    /// Films from the front of the stage, at about head height, under the roof: a person in the act
    /// filling the picture, or the whole band. Distance is how far back the camera is, in metres.
    /// </summary>
    private void Front(FestivalScene f, BandRole? role, float distance, double? skipTo)
    {
        var member = role is null ? null : f.Acts.Band.FirstOrDefault(m => m.Role == role);
        var at = member is not null ? member.Node.GlobalPosition + new Vector3(0, role == BandRole.Drummer ? 0.9f : 1.15f, 0) : _stage + new Vector3(0, -0.2f, -1.4f);
        if (_film is null)
        {
            _film = new Camera3D { Name = "Film", Fov = 38f };
            f.AddChild(_film);
        }

        // The stage faces south (+Z): stand a little to one side and slightly above.
        var from = role switch
        {
            // From above, to see the sticks come down on the drums.
            BandRole.Drummer => at + new Vector3(distance * 0.3f, distance * 0.75f, distance * 0.8f),
            BandRole.Singer => at + new Vector3(distance * 0.55f, distance * 0.12f, distance),
            _ => at + new Vector3(distance * 0.35f, distance * 0.22f, distance),
        };
        _film.LookAtFromPosition(from, at, Vector3.Up);
        _film.Current = true;
        f.SetHudShown(false);
        if (skipTo is { } seconds)
        {
            f.Music.SkipTo(seconds);
        }
    }

    private void Look(FestivalScene f, BandRole? role, float size, double? skipTo)
    {
        if (_film is not null)
        {
            _film.Current = false;
        }

        f.SetHudShown(true);
        var member = role is null ? null : f.Acts.Band.FirstOrDefault(m => m.Role == role);
        var at = member is not null ? member.Node.GlobalPosition + new Vector3(0, 0.9f, 0) : _stage;
        // A point up on the stage looks right when the camera is aimed at the ground behind it.
        var yaw = Mathf.DegToRad(f.Camera.Yaw);
        var back = Mathf.Cos(Mathf.DegToRad(CameraRig.PitchDegrees)) / Mathf.Sin(Mathf.DegToRad(CameraRig.PitchDegrees)) * at.Y;
        f.Camera.LookCloser(at.X - (Mathf.Sin(yaw) * back), at.Z - (Mathf.Cos(yaw) * back), size);
        f.Camera.Snap();
        if (skipTo is { } seconds)
        {
            f.Music.SkipTo(seconds);
        }
    }

    private void NextShot()
    {
        _shot++;
        var only = OS.GetEnvironment("SHOTS_ONLY");
        while (only.Length > 0 && _shot < _shots.Count && !_shots[_shot].Name.Contains(only, StringComparison.Ordinal))
        {
            _shot++;
        }

        _frame = 0;
        if (_shot >= _shots.Count)
        {
            GD.Print("[band] Done.");
            GetTree().Quit(0);
            return;
        }

        var act = _shots[_shot].Name.Contains("dj", StringComparison.Ordinal) ? Dj : Band;
        if (act != _act)
        {
            _act = act;
            _ready = false;
            _film = null;
            GameManager.Instance.StartNewGame(null, null, 9);
        }
        else
        {
            _frame = 0;
        }
    }

    private void Arrange(FestivalScene festival)
    {
        var game = festival.Game;
        var f = game.Field.Field;
        var info = Catalogue.Of(StageKind.HiredStage);
        var x = f.X + ((f.Width - info.Width) / 2) + 8;
        game.BuildStage(StageKind.HiredStage, x, f.Y + 12, Facing.South);
        game.Show.Book(1, ActGenerator.Of(Roster.Named(_act)!));
        game.BuildStall(StallKind.Burgers, f.X + 7, f.Y + 30, Facing.East);
        game.BuildStall(StallKind.Bar, f.X + 7, f.Y + 48, Facing.East);
        game.BuildStall(StallKind.Toilets, f.X + 26, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.Toilets, f.X + 36, f.Y + 5, Facing.South);
        game.BuildStall(StallKind.WaterPoint, f.X + 46, f.Y + 5, Facing.South);
        var until = game.Now.Date.AddHours(16.4);
        while (game.Now < until)
        {
            game.Clock.Step();
        }

        game.Clock.SetSpeed(GameSpeed.Normal);
        festival.Crowd.Snap();
        festival.SetHudShown(true);
        var middle = SiteView.Middle(new TileRect(x, f.Y + 12, info.Width, info.Depth));
        _stage = middle + new Vector3(0, 1.6f, 1.5f);
        GD.Print($"[band] {_act}: {festival.TopBar.ShowText}, {game.Crowd.Watching} watching");
    }
}
