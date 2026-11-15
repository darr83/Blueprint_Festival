using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// An entertainer's set times. Click a guitarist, pianist, drummer or DJ on the field (with nothing
/// picked on the build bar) and this opens down the left: a button for each hour from 10:00 to
/// 22:00, lit for the hours they play, which can be changed until each set starts. It also says
/// whether they are playing, how many are listening, and how many songs are in their folder.
/// </summary>
public partial class BuskPanel : PanelContainer
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private VBoxContainer _box = null!;
    private double _sinceRefresh;
    private string _shown = "";
    private readonly Dictionary<int, Button> _hours = new();

    /// <summary>The entertainer whose times are showing, or null.</summary>
    public StaffMember? Showing { get; private set; }

    /// <summary>The hour buttons showing, by hour. Used by the pictures and the smoke test.</summary>
    public IReadOnlyDictionary<int, Button> Hours => _hours;

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        CustomMinimumSize = new Vector2(400, 0);
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 20, radius: 12));
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 8);
        AddChild(_box);
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize, 22);
        Position = new Vector2(22, UiStyle.TopClear + 8);
    }

    /// <summary>Opens an entertainer's set times. True if it opened.</summary>
    public bool Open(StaffMember member)
    {
        if (!Busking.IsEntertainer(member.Kind) || !_game.Staff.Contains(member))
        {
            return false;
        }

        Showing = member;
        Visible = true;
        _shown = "";
        Refresh();
        return true;
    }

    public void Close()
    {
        Showing = null;
        Visible = false;
    }

    /// <summary>Adds or takes away the set at an hour, as a click on its button would.</summary>
    public bool Toggle(int hour)
    {
        if (Showing is not { } member)
        {
            return false;
        }

        var on = !member.Sets.Contains(hour);
        var changed = _game.SetBuskHour(member, hour, on);
        SoundBoard.Cue(changed ? Sounds.Click : Sounds.Refuse);
        Refresh();
        return changed;
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh > 0.5)
        {
            Refresh();
        }
    }

    /// <summary>A few words on what an entertainer is up to.</summary>
    public static string StatusOf(FestivalGame game, StaffMember member)
    {
        var now = game.BuskSetNow(member);
        if (now >= 0)
        {
            var listening = game.ListeningTo(member);
            var crowd = game.BuskCatchesOn(member, now) ? " This set has caught on: more people than usual are stopping." : "";
            return $"Playing now, until {now}:{Busking.SetMinutes:00}. {(listening == 1 ? "1 person is" : $"{listening} people are")} listening.{crowd}";
        }

        if (member.Sets.Count == 0)
        {
            return "No sets picked: they will not play.";
        }

        if (game.Phase != Phase.Day)
        {
            return $"{member.Sets.Count} {(member.Sets.Count == 1 ? "set" : "sets")} of {Busking.SetMinutes} minutes on the day.";
        }

        var hour = game.Now.Hour;
        var next = member.Sets.Where(h => h > hour || (h == hour && game.Now.Minute < Busking.SetMinutes)).Cast<int?>().FirstOrDefault();
        return next is { } h2 ? $"Having a break. Next set at {h2}:00." : "Finished for the day.";
    }

    public void Refresh()
    {
        _sinceRefresh = 0;
        if (Showing is not { } member || !_game.Staff.Contains(member))
        {
            Close();
            return;
        }

        var folder = Busking.MusicFolder(member.Kind);
        var songs = MusicPlayer.SongsOf(folder).Count;
        var status = StatusOf(_game, member);
        var key = $"{member.Id}|{string.Join(",", member.Sets)}|{status}|{_game.Phase}|{_game.Now.Hour}|{_game.BuskListeners}|{songs}";
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        foreach (var child in _box.GetChildren())
        {
            _box.RemoveChild(child);
            child.QueueFree();
        }

        _hours.Clear();
        var top = new HBoxContainer();
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(UiStyle.Caps("Entertainer", pink: true));
        titles.AddChild(UiStyle.Heading(Catalogue.Of(member.Kind).Name, 26));
        top.AddChild(titles);
        var close = UiStyle.SecondaryButton("Close", Close);
        close.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        top.AddChild(close);
        _box.AddChild(top);

        _box.AddChild(Wrapped($"Pick the hours they play. Each set is {Busking.SetMinutes} minutes from the hour, then a break. A set can be changed until it starts.", muted: true, size: 13));
        _box.AddChild(new HSeparator());

        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        for (var hour = Busking.FirstHour; hour <= Busking.LastHour; hour++)
        {
            var h = hour;
            var button = UiStyle.ChipButton($"{hour}:00", () => Toggle(h));
            button.ToggleMode = true;
            button.SetPressedNoSignal(member.Sets.Contains(hour));
            button.Disabled = !_game.CanChangeSet(hour);
            button.CustomMinimumSize = new Vector2(66, 34);
            button.TooltipText = member.Sets.Contains(hour) ? $"Plays {hour}:00 to {hour}:{Busking.SetMinutes:00}. Click to take this set away." : $"Click to have them play {hour}:00 to {hour}:{Busking.SetMinutes:00}.";
            grid.AddChild(button);
            _hours[hour] = button;
        }

        _box.AddChild(grid);
        _box.AddChild(new HSeparator());
        _box.AddChild(Wrapped(status, size: 14));
        if (_game.Phase != Phase.Build)
        {
            _box.AddChild(Wrapped($"Today {_game.BuskListeners:N0} people have stopped to listen to the entertainers.", muted: true, size: 13));
        }

        _box.AddChild(Wrapped(songs == 0
            ? $"No songs yet. Put some in the game's Music/{folder} folder; until then they play in silence."
            : $"{songs} {(songs == 1 ? "song" : "songs")} in Music/{folder}.", muted: true, size: 13));
        ResetSize();
    }

    private static Label Wrapped(string text, bool muted = false, int size = 16)
    {
        var label = UiStyle.Body(text, muted: muted, size: size);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(360, 0);
        return label;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && Visible)
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } || _tool.Mode != BuildMode.None
            || _game.Phase is not (Phase.Build or Phase.Day))
        {
            return;
        }

        if (_tool.TileUnderMouse() is { } tile && ClickOn(tile))
        {
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>
    /// What a click on the field does when nothing is picked on the build bar: on an entertainer's
    /// pitch it opens their set times, anywhere else it closes the panel. True if it opened.
    /// </summary>
    public bool ClickOn(TilePoint tile)
    {
        if (_game.Entertainers.FirstOrDefault(s => FestivalGame.PitchOf(s.Tile).Contains(tile)) is { } member && Open(member))
        {
            SoundBoard.Cue(Sounds.Tab);
            return true;
        }

        if (Visible)
        {
            Close();
        }

        return false;
    }
}
