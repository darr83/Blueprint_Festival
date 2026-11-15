using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Audio;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.Sound;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The firework mixer. Opened from the Fireworks group of the build bar: the day's shows along the
/// top, each at a time the player picks; the eight kinds of firework to drag onto a 30-second
/// timeline in three rows; what the crowd will think of the show; and whether the launch pad's
/// safe zone is fenced off and clear.
/// </summary>
public partial class FireworksPanel : PanelContainer
{
    private FestivalGame _game = null!;
    private FireworksView _view = null!;
    private BuildTool _tool = null!;
    private VBoxContainer _box = null!;
    private VBoxContainer _upper = null!;
    private VBoxContainer _lower = null!;
    private FireworkTimeline _timeline = null!;
    private string _shown = "";
    private double _sinceRefresh;

    /// <summary>Which show is open on the mixer, as its place in the day's list, or -1.</summary>
    public int ShowIndex { get; private set; } = -1;

    /// <summary>Which firework of the open show is picked, or -1.</summary>
    public int Picked { get; private set; } = -1;

    /// <summary>What the crowd will think of the open show, as shown.</summary>
    public ShowRating? Rating { get; private set; }

    /// <summary>The safety line as shown: fenced and clear, no fence, or called off and why.</summary>
    public string SafetyText { get; private set; } = "";

    /// <summary>Asks the camera to look at the launch pad. Set by the scene.</summary>
    public Action? LookAtPad { get; set; }

    public static Color ColourOf(FireworkKind kind) => kind switch
    {
        FireworkKind.Fountain => new Color("#F5B72A"),
        FireworkKind.GroundMine => new Color("#EC4F7F"),
        FireworkKind.RomanCandle => new Color("#7C5CD6"),
        FireworkKind.CrackleCake => new Color("#E08A2E"),
        FireworkKind.Rocket => new Color("#2F8FD8"),
        FireworkKind.PeonyShell => new Color("#D9367A"),
        FireworkKind.GoldWillow => new Color("#B98A1B"),
        _ => new Color("#26232B"),
    };

    public void Initialise(FestivalGame game, FireworksView view, BuildTool tool)
    {
        _game = game;
        _view = view;
        _tool = tool;
        Theme = UiStyle.Theme;
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 18, radius: 14));
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 10);
        AddChild(_box);
        _upper = new VBoxContainer();
        _upper.AddThemeConstantOverride("separation", 10);
        _box.AddChild(_upper);
        _timeline = new FireworkTimeline { Name = "Timeline", Visible = false };
        _box.AddChild(_timeline);
        _lower = new VBoxContainer();
        _lower.AddThemeConstantOverride("separation", 10);
        _box.AddChild(_lower);
        _timeline.Dropped += (kind, at) => AddFirework(kind, at);
        _timeline.Moved += (cue, at) =>
        {
            if (_game.MoveFirework(ShowIndex, cue, at))
            {
                // Only the timeline is drawn again while dragging; the rest follows when the mouse lets go.
                _timeline.QueueRedraw();
            }
        };
        _timeline.Picked += cue =>
        {
            Picked = cue;
            Refresh(force: true);
        };
        _timeline.Removed += cue => RemoveFirework(cue);
        _timeline.LetGo += () => Refresh(force: true);
        Visible = false;
        game.Site.Changed += () => _shown = "";
    }

    public void Open()
    {
        if (!_game.CanPlanFireworks)
        {
            return;
        }

        Visible = true;
        _view.ZoneShown = true;
        if (ShowIndex < 0 || ShowIndex >= _game.FireworkShows.Count)
        {
            ShowIndex = _game.FireworkShows.Count > 0 ? 0 : -1;
        }

        Picked = -1;
        Refresh(force: true);
    }

    public void Close()
    {
        Visible = false;
        _view.ZoneShown = false;
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>Adds a show at the next free time and opens it. False if no more fit.</summary>
    public bool AddShow()
    {
        var index = _game.NextShowMinute() is { } minute ? _game.AddFireworkShow(minute) : -1;
        if (index < 0)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return false;
        }

        ShowIndex = index;
        Picked = -1;
        Refresh(force: true);
        return true;
    }

    /// <summary>Opens one of the day's shows on the mixer.</summary>
    public void OpenShow(int index)
    {
        ShowIndex = index;
        Picked = -1;
        _timeline.Playhead = 0;
        Refresh(force: true);
    }

    /// <summary>Moves the open show a quarter of an hour earlier or later.</summary>
    public bool NudgeTime(int quarters)
    {
        if (ShowIndex < 0)
        {
            return false;
        }

        var minute = _game.FireworkShows[ShowIndex].Minute;
        for (var step = 1; step <= 8; step++)
        {
            var to = minute + (quarters * step * Fireworks.MinuteStep);
            if (to < Fireworks.EarliestMinute || to > Fireworks.LatestMinute)
            {
                break;
            }

            if (_game.MoveFireworkShow(ShowIndex, to) is var moved and >= 0)
            {
                ShowIndex = moved;
                Refresh(force: true);
                return true;
            }
        }

        SoundBoard.Cue(Sounds.Refuse);
        return false;
    }

    /// <summary>Buys a firework and puts it on the open show's timeline.</summary>
    public bool AddFirework(FireworkKind kind, double at)
    {
        if (ShowIndex < 0 && !AddShow())
        {
            return false;
        }

        var result = _game.AddFirework(ShowIndex, kind, at);
        SoundBoard.Cue(result.Ok ? Sounds.Click : Sounds.Refuse);
        if (result.Ok)
        {
            Picked = _game.FireworkShows[ShowIndex].Cues.Count - 1;
        }

        Refresh(force: true);
        return result.Ok;
    }

    public bool RemoveFirework(int cue)
    {
        var ok = _game.RemoveFirework(ShowIndex, cue);
        SoundBoard.Cue(ok ? Sounds.TakeDown : Sounds.Refuse);
        Picked = -1;
        Refresh(force: true);
        return ok;
    }

    public bool UseExample()
    {
        var ok = ShowIndex >= 0 && _game.UseExampleShow(ShowIndex).Ok;
        SoundBoard.Cue(ok ? Sounds.Click : Sounds.Refuse);
        Picked = -1;
        Refresh(force: true);
        return ok;
    }

    public bool CancelShow()
    {
        var ok = _game.RemoveFireworkShow(ShowIndex);
        SoundBoard.Cue(ok ? Sounds.TakeDown : Sounds.Refuse);
        ShowIndex = Math.Min(ShowIndex, _game.FireworkShows.Count - 1);
        Picked = -1;
        Refresh(force: true);
        return ok;
    }

    /// <summary>Lets the open show off now, as a try-out, with the camera on the pad. Nobody's mood changes.</summary>
    public void Watch()
    {
        if (ShowIndex < 0 || _game.Site.LaunchPad is null)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return;
        }

        LookAtPad?.Invoke();
        _view.Preview(_game.FireworkShows[ShowIndex]);
    }

    public bool SetFence(bool on)
    {
        var result = _game.FenceSafeZone(on);
        SoundBoard.Cue(result.Ok ? Sounds.BuildFence : Sounds.Refuse);
        Refresh(force: true);
        return result.Ok;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        if (!_game.CanPlanFireworks)
        {
            Close();
            return;
        }

        if (_view.Playing)
        {
            _timeline.Playhead = Math.Min(Fireworks.ShowSeconds, _view.ShowTime);
        }

        _sinceRefresh += delta;
        if (_sinceRefresh > 0.5)
        {
            Refresh();
        }

        var width = GetViewport().GetVisibleRect().Size.X;
        if (Size.Y > GetCombinedMinimumSize().Y + 1)
        {
            // Shrink back after a show with fewer rows, or after the notes wrap differently.
            ResetSize();
        }

        Position = new Vector2(22, UiStyle.TopClear + 8);
        _ = width;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.Keycode is Key.Delete or Key.Backspace && Picked >= 0)
            {
                RemoveFirework(Picked);
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.Escape)
            {
                Close();
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        // A click on the launch pad, with nothing picked on the build bar, opens the mixer.
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _tool.Mode == BuildMode.None
            && _game.CanPlanFireworks && _tool.TileUnderMouse() is { } tile && _game.Site.FireworksAt(tile))
        {
            Open();
            SoundBoard.Cue(Sounds.Tab);
            GetViewport().SetInputAsHandled();
        }
    }

    private static string Stars(int stars) => new string('★', stars) + new string('☆', 5 - stars);

    private static string Clock(int minute) => $"{minute / 60:00}:{minute % 60:00}";

    public void Refresh(bool force = false)
    {
        _sinceRefresh = 0;
        if (ShowIndex >= _game.FireworkShows.Count)
        {
            ShowIndex = _game.FireworkShows.Count - 1;
        }

        var shows = _game.FireworkShows;
        var pad = _game.Site.LaunchPad;
        var key = $"{ShowIndex}|{Picked}|{_game.Money}|{pad}|{string.Join(";", shows.Select(s => $"{s.Minute}:{string.Join(",", s.Cues)}"))}|{_game.FireworkResults.Count}|{_game.Site.WhatIsIn(pad?.Zone ?? default)}";
        if (!force && key == _shown)
        {
            return;
        }

        _shown = key;
        foreach (var holder in new[] { _upper, _lower })
        {
            foreach (var child in holder.GetChildren())
            {
                holder.RemoveChild(child);
                child.QueueFree();
            }
        }

        _timeline.Visible = ShowIndex >= 0;

        // The heading, and the day's shows as chips with their times.
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 0);
        titles.AddChild(UiStyle.Caps("Firework mixer", pink: true));
        titles.AddChild(UiStyle.Heading("Firework shows", 26));
        top.AddChild(titles);
        var close = UiStyle.SecondaryButton("Close", Close);
        close.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        top.AddChild(close);
        _upper.AddChild(top);

        var chips = new HBoxContainer();
        chips.AddThemeConstantOverride("separation", 8);
        chips.AddChild(UiStyle.Caps("Today's shows"));
        foreach (var (show, i) in shows.Select((s, i) => (s, i)))
        {
            var gone = _game.HasGoneOff(show);
            var chip = UiStyle.ChipButton($"{Clock(show.Minute)}  {(show.Cues.Count == 0 ? "empty" : Stars(_game.RateFireworks(i).Stars))}{(gone ? "  done" : "")}", () => OpenShow(i));
            chip.ToggleMode = true;
            chip.SetPressedNoSignal(i == ShowIndex);
            chip.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            chips.AddChild(chip);
        }

        var add = UiStyle.SecondaryButton("Add a show", () => AddShow());
        add.Disabled = _game.NextShowMinute() is null;
        add.TooltipText = $"Up to {Fireworks.MostShows} shows, from {Clock(Fireworks.EarliestMinute)} to {Clock(Fireworks.LatestMinute)}. It is dark from about half past nine.";
        add.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        chips.AddChild(add);
        _upper.AddChild(chips);

        if (ShowIndex < 0)
        {
            var none = UiStyle.Body("No shows planned yet. Add a show, then drag fireworks onto its timeline. You can have up to four in a day, at any time from 8pm.", muted: true, size: 14);
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            none.CustomMinimumSize = new Vector2(840, 0);
            _upper.AddChild(none);
            AddSafety();
            ResetSize();
            return;
        }

        var open = shows[ShowIndex];
        var locked = _game.HasGoneOff(open);

        // When it goes off, and what to do with it.
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(UiStyle.Caps("Goes off at"));
        var earlier = UiStyle.RoundButton("minus", () => NudgeTime(-1), 32);
        earlier.Disabled = locked;
        row.AddChild(earlier);
        var time = UiStyle.Heading(Clock(open.Minute), 24);
        time.CustomMinimumSize = new Vector2(76, 0);
        time.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(time);
        var later = UiStyle.RoundButton("plus", () => NudgeTime(1), 32);
        later.Disabled = locked;
        row.AddChild(later);
        var light = Simulation.Core.Daylight.At(open.Time);
        row.AddChild(UiStyle.Body(light > 0.25 ? "Still light: hard to see" : light > 0 ? "Getting dark" : "Dark", muted: true, size: 13));
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(UiStyle.Body($"{open.Cues.Count} fireworks · £{open.Cost:N0}", muted: true, size: 13));
        _upper.AddChild(row);
        row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var example = UiStyle.SecondaryButton("Example show", () => UseExample());
        example.Disabled = locked;
        example.TooltipText = "Swap this show for a five-star one made for the launch pad you have. You pay the difference.";
        row.AddChild(example);
        var watch = UiStyle.SecondaryButton("Watch it now", Watch);
        watch.Disabled = pad is null || open.Cues.Count == 0;
        watch.TooltipText = "Let it off now as a try-out, with the camera on the pad. Nobody's mood changes and it costs nothing.";
        row.AddChild(watch);
        var cancel = UiStyle.SecondaryButton("Cancel show", () => CancelShow());
        cancel.Disabled = locked;
        cancel.TooltipText = "Take the show off the day and get the money for its fireworks back.";
        row.AddChild(cancel);
        _upper.AddChild(row);

        // The fireworks to drag onto the timeline.
        var bigPad = pad?.Kind == LaunchPadKind.Big;
        var palette = new HBoxContainer();
        palette.AddThemeConstantOverride("separation", 6);
        foreach (var info in Fireworks.All)
        {
            var chip = new FireworkChip(info, ColourOf(info.Kind), !info.BigPadOnly || bigPad) { Disabled = locked };
            chip.Pressed += () => AddFirework(info.Kind, _timeline.Playhead);
            palette.AddChild(chip);
        }

        _upper.AddChild(palette);

        _timeline.Cues = open.Cues;
        _timeline.BigPad = bigPad;
        _timeline.Selected = Picked;
        _timeline.Locked = locked;
        _timeline.QueueRedraw();
        _timeline.Visible = true;

        if (Picked >= 0 && Picked < open.Cues.Count && !locked)
        {
            var cue = open.Cues[Picked];
            var picked = new HBoxContainer();
            picked.AddThemeConstantOverride("separation", 8);
            picked.AddChild(UiStyle.Body($"{Fireworks.Of(cue.Kind).Name} goes off at {cue.At:0.0} s", bold: true, size: 14));
            var back = UiStyle.RoundButton("minus", () => { if (_game.MoveFirework(ShowIndex, Picked, cue.At - Fireworks.Step)) { Refresh(force: true); } }, 28);
            var on = UiStyle.RoundButton("plus", () => { if (_game.MoveFirework(ShowIndex, Picked, cue.At + Fireworks.Step)) { Refresh(force: true); } }, 28);
            picked.AddChild(back);
            picked.AddChild(on);
            picked.AddChild(UiStyle.SecondaryButton("Remove", () => RemoveFirework(Picked)));
            picked.AddChild(UiStyle.Body("Drag it along the timeline, or right-click it to remove it.", muted: true, size: 12));
            _lower.AddChild(picked);
        }

        // What the crowd will think, and whether it is safe.
        Rating = _game.RateFireworks(ShowIndex);
        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 16);
        var notes = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        notes.AddThemeConstantOverride("separation", 2);
        notes.AddChild(UiStyle.Caps("What the crowd will think"));
        foreach (var note in Rating.Notes)
        {
            var line = UiStyle.Body($"{(note.Good ? "✓" : "✕")}  {note.Text}", size: 13);
            line.CustomMinimumSize = new Vector2(620, 0);
            line.AddThemeColorOverride("font_color", note.Good ? UiStyle.FirstAidGreen : new Color("#C2412D"));
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            notes.AddChild(line);
        }

        bottom.AddChild(notes);
        var stub = new PanelContainer { CustomMinimumSize = new Vector2(190, 0) };
        stub.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.PinkTint, 12, UiStyle.StagePink, 10, 2));
        var stubBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stubBox.AddThemeConstantOverride("separation", 0);
        var stars = UiStyle.Body(Stars(Rating.Stars), size: 20);
        stars.AddThemeColorOverride("font_color", UiStyle.Sunflower);
        stars.HorizontalAlignment = HorizontalAlignment.Center;
        stubBox.AddChild(stars);
        var boost = UiStyle.Heading($"+{Rating.Mood * 100:0}%", 30);
        boost.HorizontalAlignment = HorizontalAlignment.Center;
        stubBox.AddChild(boost);
        var happy = UiStyle.Caps("happy, if it's safe and dark");
        happy.HorizontalAlignment = HorizontalAlignment.Center;
        stubBox.AddChild(happy);
        stub.AddChild(stubBox);
        bottom.AddChild(stub);
        _lower.AddChild(bottom);

        if (locked && _game.FireworkResults.FirstOrDefault(r => r.Minute == open.Minute) is { } result)
        {
            var done = UiStyle.Body($"This show has gone off. {result.Why}", size: 14, bold: true);
            done.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _lower.AddChild(done);
        }

        AddSafety();
        ResetSize();
    }

    /// <summary>The safety line: is there a pad, is its zone clear, is it fenced; with the fence button.</summary>
    private void AddSafety()
    {
        var pad = _game.Site.LaunchPad;
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var (outcome, why) = ShowIndex >= 0 ? _game.SafetyCheck(_game.FireworkShows[ShowIndex]) : _game.SafetyCheck(new FireworkShow(0, new List<FireworkCue> { new(FireworkKind.Rocket, 0) }));
        SafetyText = outcome switch
        {
            FireworkOutcome.Safe => "Safe and ready. " + why,
            FireworkOutcome.NoFence => why,
            _ => "It won't go off. " + why,
        };
        var colour = outcome switch { FireworkOutcome.Safe => UiStyle.FirstAidGreen, FireworkOutcome.NoFence => new Color("#B8860B"), _ => new Color("#C2412D") };
        var line = UiStyle.Body(SafetyText, size: 14, bold: true);
        line.AddThemeColorOverride("font_color", colour);
        line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        line.CustomMinimumSize = new Vector2(600, 0);
        row.AddChild(line);
        if (pad is not null)
        {
            var fence = pad.Fenced
                ? UiStyle.SecondaryButton($"Take the fence down (+£{pad.FenceCost:N0})", () => SetFence(false))
                : UiStyle.Button($"Fence the safe zone (£{pad.FenceCost:N0})", () => SetFence(true));
            fence.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(fence);
        }

        _lower.AddChild(row);
    }
}

/// <summary>A firework on the mixer: click it to drop it at the pink line, or drag it onto the timeline.</summary>
public partial class FireworkChip : Button
{
    public FireworkChip()
    {
    }

    public FireworkChip(FireworkInfo info, Color colour, bool fits)
    {
        Kind = info.Kind;
        Text = $"{info.Name}\n£{info.Cost} · {info.Seconds:0.#} s";
        TooltipText = $"{info.About}{(info.BigPadOnly ? "\nNeeds the big launch pad." : "")}\nDrag it onto the timeline, or click to drop it at the pink line.";
        ThemeTypeVariation = UiStyle.Secondary;
        FocusMode = FocusModeEnum.None;
        MouseDefaultCursorShape = CursorShape.Drag;
        CustomMinimumSize = new Vector2(102, 46);
        AddThemeFontSizeOverride("font_size", 12);
        AddThemeColorOverride("font_color", fits ? UiStyle.TextDark : UiStyle.TextMuted);
        var style = UiStyle.Box(UiStyle.Surface, 10, fits ? colour : UiStyle.Border, 6, 2);
        style.BorderWidthLeft = 8;
        AddThemeStyleboxOverride("normal", style);
        var hover = (StyleBoxFlat)style.Duplicate();
        hover.BgColor = UiStyle.SunTint;
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", hover);
        Colour = colour;
    }

    public FireworkKind Kind { get; }

    public Color Colour { get; }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Disabled)
        {
            return default;
        }

        var preview = new Label { Text = Fireworks.Of(Kind).Name };
        preview.AddThemeColorOverride("font_color", Colors.White);
        var holder = new PanelContainer();
        holder.AddThemeStyleboxOverride("panel", UiStyle.Box(Colour, 8, null, 6));
        holder.AddChild(preview);
        SetDragPreview(holder);
        return new Godot.Collections.Dictionary { ["firework"] = (int)Kind };
    }
}

/// <summary>
/// The mixer's timeline: 30 seconds across, in three rows (high sky, mid air, ground). Fireworks
/// dropped on it go in their own row at the second they were dropped; a firework can be dragged
/// along, clicked to pick it, or right-clicked to take it off.
/// </summary>
public partial class FireworkTimeline : Control
{
    private const float LabelWidth = 92;
    private const float RulerHeight = 22;
    private const float RowHeight = 28;
    private readonly List<(Rect2 Rect, int Cue)> _hits = new();
    private int _dragging = -1;
    private double _grabOffset;
    private bool _moved;

    public IReadOnlyList<FireworkCue> Cues { get; set; } = Array.Empty<FireworkCue>();

    public bool BigPad { get; set; }

    public bool Locked { get; set; }

    public int Selected { get; set; } = -1;

    /// <summary>The pink line: where a clicked firework goes, and how far a show being watched has got.</summary>
    public double Playhead
    {
        get => _playhead;
        set
        {
            _playhead = Math.Clamp(value, 0, Fireworks.ShowSeconds);
            QueueRedraw();
        }
    }

    private double _playhead;

    public event Action<FireworkKind, double>? Dropped;

    public event Action<int, double>? Moved;

    public event Action<int>? Picked;

    public event Action<int>? Removed;

    /// <summary>The mouse let go of a firework after dragging it.</summary>
    public event Action? LetGo;

    public FireworkTimeline()
    {
        CustomMinimumSize = new Vector2(850, 150);
        MouseFilter = MouseFilterEnum.Stop;
    }

    private float TrackWidth => Size.X - LabelWidth - 10;

    private float XOf(double second) => LabelWidth + (float)(second / Fireworks.ShowSeconds * TrackWidth);

    public double TimeAt(float x) => (x - LabelWidth) / TrackWidth * Fireworks.ShowSeconds;

    /// <summary>Which line of its row each firework sits on, so ones that overlap stack instead of hiding each other.</summary>
    private (int[] Line, int[] Lines) Stack()
    {
        var line = new int[Cues.Count];
        var lines = new int[3];
        foreach (var height in Enum.GetValues<FireworkHeight>())
        {
            var ends = new List<double>();
            foreach (var i in Enumerable.Range(0, Cues.Count).Where(i => Fireworks.Of(Cues[i].Kind).Height == height).OrderBy(i => Cues[i].At))
            {
                var r = ends.FindIndex(e => e <= Cues[i].At + 0.01);
                if (r < 0)
                {
                    r = ends.Count;
                    ends.Add(0);
                }

                ends[r] = Cues[i].Ends;
                line[i] = r;
            }

            lines[(int)height] = Math.Max(1, ends.Count);
        }

        return (line, lines);
    }

    public override void _Draw()
    {
        var (line, lines) = Stack();
        var height = RulerHeight + lines.Sum(n => (n * RowHeight) + 10) + 4;
        if (Math.Abs(CustomMinimumSize.Y - height) > 0.5f)
        {
            CustomMinimumSize = new Vector2(CustomMinimumSize.X, height);
        }

        var font = UiStyle.SansSemi;
        DrawStyleBox(UiStyle.Box(UiStyle.Ground, 12, UiStyle.Border, 0), new Rect2(Vector2.Zero, Size));
        for (var s = 0; s <= Fireworks.ShowSeconds; s++)
        {
            var x = XOf(s);
            DrawLine(new Vector2(x, RulerHeight), new Vector2(x, Size.Y - 4), s % 5 == 0 ? new Color("#DDD7E0") : new Color("#EEEAF0"));
            if (s % 5 == 0)
            {
                DrawString(font, new Vector2(x - 10, 16), $"{s}s", HorizontalAlignment.Center, 20, 11, UiStyle.TextMuted);
            }
        }

        // High sky at the top, the ground at the bottom.
        var tooMany = Fireworks.TooMany(Cues, BigPad);
        var tops = new float[3];
        var y = RulerHeight;
        foreach (var h in new[] { FireworkHeight.HighSky, FireworkHeight.MidAir, FireworkHeight.Ground })
        {
            tops[(int)h] = y + 5;
            DrawLine(new Vector2(8, y), new Vector2(Size.X - 8, y), new Color("#E8E4EA"));
            DrawString(font, new Vector2(10, y + 22), h switch { FireworkHeight.HighSky => "High sky", FireworkHeight.MidAir => "Mid air", _ => "Ground" }, HorizontalAlignment.Left, -1, 12, UiStyle.TextMuted);
            y += (lines[(int)h] * RowHeight) + 10;
        }

        _hits.Clear();
        for (var i = 0; i < Cues.Count; i++)
        {
            var cue = Cues[i];
            var info = Fireworks.Of(cue.Kind);
            var left = XOf(cue.At);
            var right = XOf(Math.Min(cue.Ends, Fireworks.ShowSeconds));
            var rect = new Rect2(left + 1, tops[(int)info.Height] + (line[i] * RowHeight), Math.Max(10, right - left - 2), RowHeight - 4);
            var colour = FireworksPanel.ColourOf(cue.Kind);
            var warn = tooMany.Contains(i) || (info.BigPadOnly && !BigPad);
            var box = UiStyle.Box(warn ? colour.Darkened(0.35f) : colour, 7, i == Selected ? UiStyle.NightInk : null, 0, i == Selected ? 2 : 0);
            DrawStyleBox(box, rect);
            if (warn)
            {
                // Stripes: this one is too much for the pad, or will stay in the box.
                for (var sx = rect.Position.X - rect.Size.Y; sx < rect.End.X; sx += 10)
                {
                    var from = new Vector2(Math.Max(rect.Position.X, sx), rect.End.Y - Math.Max(0, rect.Position.X - sx));
                    var to = new Vector2(Math.Min(rect.End.X, sx + rect.Size.Y), rect.Position.Y + Math.Max(0, (sx + rect.Size.Y) - rect.End.X));
                    DrawLine(from, to, new Color(1, 1, 1, 0.3f), 3);
                }
            }

            DrawString(font, rect.Position + new Vector2(6, 17), info.Name, HorizontalAlignment.Left, rect.Size.X - 8, 11, Colors.White);
            _hits.Add((rect, i));
        }

        var px = XOf(Playhead);
        DrawLine(new Vector2(px, 4), new Vector2(px, Size.Y - 4), UiStyle.StagePink, 2);
    }

    private int CueAt(Vector2 at)
    {
        for (var i = _hits.Count - 1; i >= 0; i--)
        {
            if (_hits[i].Rect.HasPoint(at))
            {
                return _hits[i].Cue;
            }
        }

        return -1;
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press:
            {
                var cue = Locked ? -1 : CueAt(press.Position);
                if (cue >= 0)
                {
                    _dragging = cue;
                    _grabOffset = TimeAt(press.Position.X) - Cues[cue].At;
                    _moved = false;
                    Selected = cue;
                    Picked?.Invoke(cue);
                }
                else if (press.Position.X >= LabelWidth)
                {
                    Playhead = TimeAt(press.Position.X);
                    Selected = -1;
                    Picked?.Invoke(-1);
                }

                AcceptEvent();
                break;
            }

            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }:
                if (_dragging >= 0 && _moved)
                {
                    LetGo?.Invoke();
                }

                _dragging = -1;
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } right when !Locked && CueAt(right.Position) is var cue and >= 0:
                Removed?.Invoke(cue);
                AcceptEvent();
                break;
            case InputEventMouseMotion motion when _dragging >= 0 && _dragging < Cues.Count:
            {
                var at = Fireworks.Clamp(Cues[_dragging].Kind, TimeAt(motion.Position.X) - _grabOffset);
                if (Math.Abs(at - Cues[_dragging].At) > 0.01)
                {
                    _moved = true;
                    Moved?.Invoke(_dragging, at);
                }

                AcceptEvent();
                break;
            }
        }

    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        !Locked && data.VariantType == Variant.Type.Dictionary && data.AsGodotDictionary().ContainsKey("firework");

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var kind = (FireworkKind)data.AsGodotDictionary()["firework"].AsInt32();
        Dropped?.Invoke(kind, Math.Max(0, TimeAt(atPosition.X) - 0.5));
    }
}
