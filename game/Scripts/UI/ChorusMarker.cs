using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The chorus marker, opened from the main menu: pick an artist and one of their songs, play it,
/// and tap C (or the big button) when a chorus starts and again when it ends. Each chorus is saved
/// straight away in a small ".chorus" file beside the song, and in the festival the crowd sings
/// along in those choruses. Songs with no choruses marked get no sing-along.
/// </summary>
public partial class ChorusMarker : Control
{
    private const float SkipSeconds = 5f;

    private readonly List<(string Artist, IReadOnlyList<SongFile> Songs)> _artists = [];
    private ItemList _artistList = null!;
    private ItemList _songList = null!;
    private Label _songTitle = null!;
    private Label _time = null!;
    private Label _saved = null!;
    private Button _play = null!;
    private Button _mark = null!;
    private Button _undo = null!;
    private Button _clear = null!;
    private CheckBox _crowd = null!;
    private VBoxContainer _chorusRows = null!;
    private Timeline _timeline = null!;
    private AudioStreamPlayer _player = null!;
    private AudioStreamPlayer _crowdPlayer = null!;
    private string _artist = "";
    private SongFile? _song;
    private ChorusMarking? _marking;
    private float _pausedAt;

    /// <summary>Called when the marker is closed.</summary>
    public System.Action? Closed { get; set; }

    /// <summary>How many artists have songs to mark. Used by the launch test.</summary>
    public int ArtistsWithSongs => _artists.Count;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiStyle.Theme;
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(new ColorRect { Color = UiStyle.Ground, AnchorRight = 1, AnchorBottom = 1 });
        AddChild(new Bunting { AnchorRight = 1, OffsetBottom = 34, FlagWidth = 52 });

        _player = new AudioStreamPlayer { Name = "Song", Bus = AudioServer.GetBusIndex(MusicPlayer.Bus) >= 0 ? MusicPlayer.Bus : "Master" };
        AddChild(_player);
        _player.Finished += () => _pausedAt = 0;
        _crowdPlayer = new AudioStreamPlayer { Name = "Crowd", Bus = _player.Bus, VolumeDb = -80 };
        AddChild(_crowdPlayer);

        var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        foreach (var side in new[] { "left", "right" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 48);
        }

        margin.AddThemeConstantOverride("margin_top", 56);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        AddChild(margin);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 10);
        margin.AddChild(page);

        page.AddChild(UiStyle.Caps("Sing-along", pink: true, size: 13));
        page.AddChild(UiStyle.Heading("Mark the choruses", 44));
        var how = UiStyle.Body(
            "Pick a song and press Play. Tap C (or the pink button) as a chorus starts, and again as it ends. "
            + "Each chorus is saved beside the song straight away. Once a song's crowd has been made from its singing, the crowd sings along in those choruses. "
            + "Space plays and pauses, the arrow keys skip 5 seconds, Z takes back the last tap.",
            muted: true,
            size: 15);
        how.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        page.AddChild(how);
        page.AddChild(UiStyle.Rule());

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 28);
        page.AddChild(columns);

        // The artists with songs, and the chosen artist's songs.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);
        left.AddChild(UiStyle.Caps("Artists with songs"));
        _artistList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.1f };
        Paper(_artistList);
        _artistList.ItemSelected += i => ChooseArtist((int)i);
        left.AddChild(_artistList);
        left.AddChild(UiStyle.Caps("Songs"));
        _songList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        Paper(_songList);
        _songList.ItemSelected += i => ChooseSong((int)i);
        left.AddChild(_songList);

        // The song: its timeline with the choruses on it, the buttons, and the list of choruses.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 12);
        columns.AddChild(right);
        _songTitle = UiStyle.Heading("Choose a song", 30);
        right.AddChild(_songTitle);
        _timeline = new Timeline { CustomMinimumSize = new Vector2(0, 70), Seek = Seek };
        right.AddChild(_timeline);
        _time = UiStyle.Body("0:00.0 / 0:00.0", muted: true, size: 15);
        right.AddChild(_time);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        right.AddChild(buttons);
        buttons.AddChild(UiStyle.SecondaryButton("Back 5s", () => Skip(-SkipSeconds)));
        _play = UiStyle.Button("Play", PlayOrPause);
        _play.CustomMinimumSize = new Vector2(120, 0);
        buttons.AddChild(_play);
        buttons.AddChild(UiStyle.SecondaryButton("On 5s", () => Skip(SkipSeconds)));
        buttons.AddChild(new Control { CustomMinimumSize = new Vector2(16, 0) });
        _mark = UiStyle.Button("Chorus starts (C)", Tap);
        _mark.CustomMinimumSize = new Vector2(230, 0);
        buttons.AddChild(_mark);
        _undo = UiStyle.SecondaryButton("Undo (Z)", Undo);
        buttons.AddChild(_undo);

        _crowd = new CheckBox { Text = "Hear the crowd sing in the choruses", ButtonPressed = true, FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            _crowd.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        foreach (var colour in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
        {
            _crowd.AddThemeColorOverride(colour, UiStyle.TextDark);
        }

        right.AddChild(_crowd);

        right.AddChild(UiStyle.Caps("Choruses in this song"));
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        right.AddChild(scroll);
        _chorusRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _chorusRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_chorusRows);
        _saved = UiStyle.Body("", muted: true, size: 13);
        _saved.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(_saved);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 10);
        page.AddChild(bottom);
        _clear = UiStyle.SecondaryButton("Clear this song's choruses", Clear);
        bottom.AddChild(_clear);
        bottom.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        bottom.AddChild(UiStyle.Button("Back to the menu (Esc)", Close));

        Fill();
        Refresh();
    }

    /// <summary>A list drawn like the programme: white paper, a thin border, the chosen line in pink.</summary>
    private static void Paper(ItemList list)
    {
        list.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Surface, 12, UiStyle.Border, 8));
        list.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        list.AddThemeStyleboxOverride("selected", UiStyle.Box(UiStyle.PinkTint, 8));
        list.AddThemeStyleboxOverride("selected_focus", UiStyle.Box(UiStyle.PinkTint, 8));
        list.AddThemeStyleboxOverride("hovered", UiStyle.Box(UiStyle.Ground, 8));
        list.AddThemeStyleboxOverride("hovered_selected", UiStyle.Box(UiStyle.PinkTint, 8));
        list.AddThemeStyleboxOverride("hovered_selected_focus", UiStyle.Box(UiStyle.PinkTint, 8));
        list.AddThemeColorOverride("font_color", UiStyle.TextDark);
        list.AddThemeColorOverride("font_selected_color", UiStyle.TextDark);
        list.AddThemeColorOverride("font_hovered_color", UiStyle.TextDark);
        list.AddThemeColorOverride("font_hovered_selected_color", UiStyle.TextDark);
        list.AddThemeConstantOverride("v_separation", 8);
        list.AddThemeFontSizeOverride("font_size", 16);
    }

    /// <summary>Reads which artists have songs, and lists them.</summary>
    private void Fill()
    {
        _artists.Clear();
        _artistList.Clear();
        foreach (var artist in Roster.Artists.Select(a => a.Name))
        {
            var songs = MusicPlayer.SongsOf(artist);
            if (songs.Count > 0)
            {
                _artists.Add((artist, songs));
                var marked = songs.Count(s => ChorusLibrary.Of(artist, s).Any);
                _artistList.AddItem($"{artist}  ·  {songs.Count} song{(songs.Count == 1 ? "" : "s")}, {marked} marked");
            }
        }

        if (_artists.Count == 0)
        {
            _songTitle.Text = "No songs yet";
            _saved.Text = "Put songs in the game's Music folder, in a folder named after the artist, then open this again.";
            return;
        }

        _artistList.Select(0);
        ChooseArtist(0);
    }

    private void ChooseArtist(int index)
    {
        if (index < 0 || index >= _artists.Count)
        {
            return;
        }

        _artist = _artists[index].Artist;
        _songList.Clear();
        foreach (var song in _artists[index].Songs)
        {
            _songList.AddItem(SongLine(song));
        }

        _songList.Select(0);
        ChooseSong(0);
    }

    private string SongLine(SongFile song)
    {
        var count = ChorusLibrary.Of(_artist, song).All.Count;
        var vocals = ChorusLibrary.VocalsOf(_artist, song) is not null ? "  ·  crowd ready" : count > 0 ? "  ·  crowd not made yet" : "";
        return $"{song.Title}  ·  {(count == 0 ? "no choruses" : count == 1 ? "1 chorus" : $"{count} choruses")}{vocals}";
    }

    private void ChooseSong(int index)
    {
        var songs = _artists.FirstOrDefault(a => a.Artist == _artist).Songs;
        if (songs is null || index < 0 || index >= songs.Count)
        {
            return;
        }

        _player.Stop();
        _crowdPlayer.Stop();
        _pausedAt = 0;
        _song = songs[index];
        _marking = new ChorusMarking(ChorusLibrary.Of(_artist, _song));
        _player.Stream = ResourceLoader.Load($"{MusicPlayer.Folder}/{_artist}/{_song.File}") as AudioStream;
        _crowdPlayer.Stream = ChorusLibrary.VocalsOf(_artist, _song) is { } crowd ? ResourceLoader.Load(crowd) as AudioStream : null;
        _songTitle.Text = _song.Title;
        _saved.Text = _player.Stream is null ? "This song could not be opened." : "";
        Refresh();
    }

    private double Now => _player.Playing ? _player.GetPlaybackPosition() + AudioServer.GetTimeSinceLastMix() - AudioServer.GetOutputLatency() : _pausedAt;

    private double Length => _player.Stream?.GetLength() ?? 0;

    private void PlayOrPause()
    {
        if (_player.Stream is null)
        {
            return;
        }

        if (_player.Playing)
        {
            _pausedAt = _player.GetPlaybackPosition();
            _player.Stop();
            _crowdPlayer.Stop();
        }
        else
        {
            _player.Play(_pausedAt);
            if (_crowdPlayer.Stream is not null)
            {
                _crowdPlayer.Play(_pausedAt);
            }
        }

        Refresh();
    }

    private void Skip(float seconds) => Seek(Now + seconds);

    private void Seek(double seconds)
    {
        if (_player.Stream is null)
        {
            return;
        }

        var at = (float)System.Math.Clamp(seconds, 0, System.Math.Max(0, Length - 0.05));
        if (_player.Playing)
        {
            _player.Seek(at);
            if (_crowdPlayer.Playing)
            {
                _crowdPlayer.Seek(at);
            }
        }
        else
        {
            _pausedAt = at;
        }

        Refresh();
    }

    /// <summary>A tap: the start of a chorus, or the end of the one started.</summary>
    private void Tap()
    {
        if (_marking is null || _song is null || _player.Stream is null)
        {
            return;
        }

        if (_marking.Started is { } start && Now - start < Choruses.Shortest)
        {
            _saved.Text = $"A chorus needs to be at least {Choruses.Shortest:0} seconds long. Tap again when it ends.";
            return;
        }

        if (_marking.Tap(Now))
        {
            Save();
        }

        Refresh();
    }

    private void Undo()
    {
        if (_marking?.Undo() == true)
        {
            Save();
        }

        Refresh();
    }

    private void Clear()
    {
        if (_marking is null)
        {
            return;
        }

        _marking.Clear();
        Save();
        Refresh();
    }

    private void Remove(int index)
    {
        _marking?.Remove(index);
        Save();
        Refresh();
    }

    private void Save()
    {
        if (_marking is null || _song is null)
        {
            return;
        }

        try
        {
            var path = ChorusLibrary.Save(_artist, _song, _marking.Marks);
            _saved.Text = _marking.Marks.Any ? $"Saved in {path}" : "No choruses: this song has no sing-along.";
        }
        catch (System.Exception e) when (e is System.IO.IOException or System.UnauthorizedAccessException)
        {
            _saved.Text = $"Could not save the choruses: {e.Message}";
        }

        var index = _songList.GetSelectedItems().FirstOrDefault();
        if (index >= 0 && index < _songList.ItemCount)
        {
            _songList.SetItemText(index, SongLine(_song));
        }
    }

    /// <summary>Shows the buttons, the times and the list of choruses as they are now.</summary>
    private void Refresh()
    {
        var has = _player.Stream is not null && _marking is not null;
        _play.Text = _player.Playing ? "Pause" : "Play";
        _play.Disabled = !has;
        _mark.Disabled = !has;
        _mark.Text = _marking?.Started is null ? "Chorus starts (C)" : "Chorus ends (C)";
        _undo.Disabled = _marking?.CanUndo != true;
        _clear.Disabled = _marking?.Marks.Any != true;
        _timeline.Length = Length;
        _timeline.Marks = _marking?.Marks ?? Choruses.None;
        _timeline.Started = _marking?.Started;

        foreach (var child in _chorusRows.GetChildren())
        {
            child.QueueFree();
        }

        var marks = _marking?.Marks.All ?? [];
        if (marks.Count == 0)
        {
            _chorusRows.AddChild(UiStyle.Body(_marking?.Started is { } s ? $"Chorus started at {Choruses.Clock(s)}: tap again where it ends." : "None marked yet.", muted: true, size: 15));
        }

        for (var i = 0; i < marks.Count; i++)
        {
            var index = i;
            var chorus = marks[i];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(UiStyle.Body($"Chorus {i + 1}", bold: true, size: 15));
            row.AddChild(UiStyle.Body($"{Choruses.Clock(chorus.Start)} to {Choruses.Clock(chorus.End)}  ({chorus.Length:0.0}s)", size: 15));
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
            row.AddChild(UiStyle.ChipButton("Hear it", () => Seek(chorus.Start - 2)));
            row.AddChild(UiStyle.ChipButton("Remove", () => Remove(index)));
            _chorusRows.AddChild(row);
        }
    }

    public override void _Process(double delta)
    {
        var now = Now;
        _timeline.At = now;
        _time.Text = $"{Choruses.Clock(now)} / {Choruses.Clock(Length)}";
        if (!_player.Playing && _play.Text == "Pause")
        {
            Refresh();
        }

        // A taste of how it will sound: the song's crowd file under it in the marked choruses, as loud as a full field.
        var level = _player.Playing && _crowd.ButtonPressed && _marking is not null ? _marking.Marks.SingingAt(now) * StageMusic.CrowdLevel : 0;
        _crowdPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Max((float)level, 0.0001f));
        if (_crowdPlayer.Playing && !_player.Playing)
        {
            _crowdPlayer.Stop();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.C:
                Tap();
                break;
            case Key.Space:
                PlayOrPause();
                break;
            case Key.Left:
                Skip(-SkipSeconds);
                break;
            case Key.Right:
                Skip(SkipSeconds);
                break;
            case Key.Z:
            case Key.Backspace:
                Undo();
                break;
            case Key.Escape:
                Close();
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void Close()
    {
        _player.Stop();
        _crowdPlayer.Stop();

        Closed?.Invoke();
        QueueFree();
    }

    /// <summary>The song drawn as a bar, with its choruses in pink and where it has got to; click it to jump there.</summary>
    private sealed partial class Timeline : Control
    {
        public double Length { get; set; }

        public double At { get; set; }

        public double? Started { get; set; }

        public Choruses Marks { get; set; } = Choruses.None;

        public System.Action<double>? Seek { get; set; }

        public override void _Ready() => MouseDefaultCursorShape = CursorShape.PointingHand;

        public override void _Process(double delta) => QueueRedraw();

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && Length > 0)
            {
                Seek?.Invoke(click.Position.X / Mathf.Max(1, Size.X) * Length);
                AcceptEvent();
            }
        }

        public override void _Draw()
        {
            var bar = new Rect2(0, 18, Size.X, Size.Y - 36);
            DrawStyleBox(UiStyle.Box(UiStyle.Surface, 10, UiStyle.Border), bar);
            if (Length <= 0)
            {
                return;
            }

            float X(double seconds) => (float)(System.Math.Clamp(seconds / Length, 0, 1) * Size.X);
            foreach (var chorus in Marks.All)
            {
                DrawRect(new Rect2(X(chorus.Start), bar.Position.Y + 3, Mathf.Max(2, X(chorus.End) - X(chorus.Start)), bar.Size.Y - 6), UiStyle.StagePink);
            }

            if (Started is { } start && At > start)
            {
                DrawRect(new Rect2(X(start), bar.Position.Y + 3, Mathf.Max(2, X(At) - X(start)), bar.Size.Y - 6), UiStyle.Sunflower);
            }

            // A tick every 30 seconds.
            for (var t = 30.0; t < Length; t += 30)
            {
                DrawLine(new Vector2(X(t), bar.End.Y), new Vector2(X(t), bar.End.Y + 6), UiStyle.TextMuted, 1);
            }

            var x = X(At);
            DrawLine(new Vector2(x, 6), new Vector2(x, Size.Y - 6), UiStyle.NightInk, 3);
            DrawCircle(new Vector2(x, 8), 6, UiStyle.NightInk);
        }
    }
}
