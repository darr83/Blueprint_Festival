using BlueprintFestival.Simulation.Sound;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The event day panel, "The day so far": a tall panel down the right of the screen, so the field
/// stays in view, with tabs for an overview, the acts on every stage, how people feel, every
/// incident, and the day's money. It keeps itself up to date while the day runs. Opened from the
/// button under the festival's name, or the H key.
/// </summary>
public partial class DayPanel : Paper
{
    /// <summary>The tabs along the top of the panel.</summary>
    public enum Tab
    {
        Overview,
        Acts,
        People,
        Incidents,
        Money,
    }

    public const int PanelWidth = 440;

    /// <summary>How far above the bottom of the screen the panel stops, to leave the view buttons clear.</summary>
    private const int BottomClear = 96;

    private static readonly Color Amber = new("#E08A1E");
    private static readonly Color Bad = new("#D9483B");
    private static readonly Color AmberTint = new("#FDEBD3");
    private static readonly Color GoodTint = new("#E3F4EA");
    internal static readonly Color RainTint = new("#E9F0F8");

    private FestivalGame _game = null!;
    private Label _kicker = null!;
    private VBoxContainer _body = null!;
    private ScrollContainer _scroll = null!;
    private readonly Dictionary<Tab, Button> _tabs = new();
    private double _sinceRefresh = 1;
    private string _shown = "";

    public DayPanel() : base(18, small: true)
    {
    }

    /// <summary>The tab showing.</summary>
    public Tab Showing { get; private set; } = Tab.Overview;

    /// <summary>The report the panel is showing, for the smoke test.</summary>
    public DayReport? Report { get; private set; }

    /// <summary>How many rows (sets, incidents, money lines) the tab showing has, for the smoke test.</summary>
    public int RowCount { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Name = "DayPanel";
        CustomMinimumSize = new Vector2(PanelWidth, 0);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        AddChild(box);

        _kicker = UiStyle.Caps("", pink: true, size: 11);
        box.AddChild(_kicker);
        var top = new HBoxContainer();
        var heading = UiStyle.Heading("The day so far", 26);
        heading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(heading);
        var close = UiStyle.RoundButton("×", () => Visible = false, 34);
        close.TooltipText = "Close (H)";
        top.AddChild(close);
        box.AddChild(top);

        var tabs = new HFlowContainer();
        tabs.AddThemeConstantOverride("h_separation", 6);
        foreach (var tab in System.Enum.GetValues<Tab>())
        {
            var chip = UiStyle.ChipButton(tab.ToString(), () => Show(tab), Sounds.Tab);
            chip.ToggleMode = true;
            _tabs[tab] = chip;
            tabs.AddChild(chip);
        }

        box.AddChild(tabs);

        _scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6);
        // Room on the right for the scroll bar, so it never sits over the numbers.
        var room = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        room.AddThemeConstantOverride("margin_right", 14);
        room.AddChild(_body);
        _scroll.AddChild(room);
        box.AddChild(_scroll);

        Visible = false;
        Place();
        GetViewport().SizeChanged += Place;
        Show(Tab.Overview);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Place;
    }

    /// <summary>Down the right of the screen, under the speed buttons, and clear of the view buttons.</summary>
    private void Place()
    {
        var screen = GetViewportRect().Size;
        Position = new Vector2(screen.X - 22 - PanelWidth, UiStyle.TopClear);
        Size = new Vector2(PanelWidth, Mathf.Max(320, screen.Y - UiStyle.TopClear - BottomClear));
    }

    /// <summary>Opens the panel on a tab, or moves to that tab if it is open.</summary>
    public void Show(Tab tab)
    {
        Showing = tab;
        foreach (var (each, chip) in _tabs)
        {
            chip.SetPressedNoSignal(each == tab);
        }

        _shown = "";
        _scroll.ScrollVertical = 0;
        Refresh();
    }

    public void Toggle()
    {
        Visible = !Visible;
        if (Visible)
        {
            Place();
            Refresh();
        }
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh > 0.5)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        _sinceRefresh = 0;
        var report = DayReport.Of(_game);
        Report = report;
        _kicker.Text = $"{report.Now:dddd HH:mm}  ·  {Weather()}";

        // Only lay the tab out again when something on it has changed, so it does not flicker.
        var key = string.Join("|", Showing, report.Now.ToString("HH:mm"), report.OnSite, report.Happy, report.Fed, report.Watered, report.Toilets, report.MoodLine.Count,
            string.Join(",", report.Log.Select(n => $"{n.Id}{n.Tone}{n.Outcome}")), string.Join(",", report.Stages.Select(s => s.Watching + string.Concat(s.Sets.Select(x => (int)x.State)))),
            report.TotalIn, report.TotalOut, report.Bank, report.BedsUsed, string.Join(",", report.WorthALook));
        if (key == _shown)
        {
            return;
        }

        _shown = key;
        foreach (var child in _body.GetChildren())
        {
            _body.RemoveChild(child);
            child.QueueFree();
        }

        RowCount = 0;
        switch (Showing)
        {
            case Tab.Overview:
                Overview(report);
                break;
            case Tab.Acts:
                Acts(report);
                break;
            case Tab.People:
                People(report);
                break;
            case Tab.Incidents:
                Incidents(report);
                break;
            case Tab.Money:
                Money(report);
                break;
        }
    }

    private string Weather() => _game.Weather.Wet && _game.RainNow > 0 ? $"{_game.Weather.Word} {_game.Weather.High}°C" : _game.Weather.Kind == WeatherKind.Hot ? $"Hot {_game.Weather.High}°C" : $"Dry {_game.Weather.High}°C";

    private void Overview(DayReport report)
    {
        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 6);
        tiles.AddChild(Tile($"{report.OnSite:N0}", report.Sold > 0 ? $"here, of {report.Sold:N0}" : "here", UiStyle.Ground));
        tiles.AddChild(Tile($"{report.Happy}%", Change(report.HappyChange), UiStyle.PinkTint));
        tiles.AddChild(Tile(Pounds(report.UpSoFar, signed: true), report.UpSoFar >= 0 ? "up so far" : "down so far", UiStyle.Ground, report.UpSoFar >= 0 ? UiStyle.FirstAidGreen : Bad));
        _body.AddChild(tiles);

        Section("On now");
        if (report.Stages.Count == 0)
        {
            Muted("No stage is booked.");
        }

        foreach (var stage in report.Stages)
        {
            var on = stage.Sets.FirstOrDefault(s => s.State == SetState.OnNow);
            var next = stage.Sets.FirstOrDefault(s => s.State == SetState.Next);
            var played = stage.Sets.Count(s => s.State is SetState.Played);
            var top = new HBoxContainer();
            var name = UiStyle.Body(stage.Name, size: 15, bold: true);
            top.AddChild(name);
            top.AddChild(UiStyle.Body(on is null ? "" : $"  {stage.Watching:N0} watching", muted: true, size: 13));
            top.AddChild(Spacer());
            top.AddChild(UiStyle.Body($"{played} of {stage.Sets.Count} played", muted: true, size: 13));
            _body.AddChild(top);
            var line = new HBoxContainer();
            var act = UiStyle.Body(on?.Act ?? "Between acts", size: 15, bold: on is not null);
            act.AddThemeColorOverride("font_color", on is null ? UiStyle.TextMuted : UiStyle.StagePink);
            line.AddChild(act);
            line.AddChild(UiStyle.Body(next is null ? "" : $"  then {next.Act} {next.Start:HH:mm}", muted: true, size: 13));
            _body.AddChild(line);
            _body.AddChild(UiStyle.Rule(1));
        }

        if (report.FireworksToCome.Count > 0)
        {
            Muted($"Fireworks at {string.Join(", ", report.FireworksToCome.Select(t => $"{t.Hours:00}:{t.Minutes:00}"))}");
        }

        Section("Happiness today");
        _body.AddChild(new MoodChart(report, _game.Day.Date, RainSpells(report)) { CustomMinimumSize = new Vector2(0, 74) });
        Needs(report);

        Section("Latest incidents");
        var latest = report.Log.Where(n => n.IsIncident).Take(3).ToList();
        if (latest.Count == 0)
        {
            Muted("Nothing has gone wrong yet.");
        }

        foreach (var note in latest)
        {
            NoteRow(note);
        }

        var aid = new HBoxContainer();
        aid.AddChild(UiStyle.Body($"Hurt {report.Hurt}  ·  ill {report.TakenIll}  ·  treated {report.Treated}", size: 14));
        aid.AddChild(Spacer());
        aid.AddChild(UiStyle.Body(report.Beds > 0 ? $"Beds {report.BedsUsed} of {report.Beds}" : "No first aid tent", muted: true, size: 13));
        _body.AddChild(aid);

        WorthALook(report);
    }

    private void Acts(DayReport report)
    {
        if (report.Stages.Count == 0)
        {
            Muted("No stage is booked.");
        }

        foreach (var stage in report.Stages)
        {
            var top = new HBoxContainer();
            top.AddChild(UiStyle.Heading(stage.Name, 18));
            top.AddChild(Spacer());
            top.AddChild(UiStyle.Body(stage.Watching > 0 ? $"{stage.Kind}  ·  {stage.Watching:N0} watching" : stage.Kind, muted: true, size: 13));
            _body.AddChild(top);
            foreach (var set in stage.Sets)
            {
                SetRow(set);
                RowCount++;
            }

            _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }

        if (report.FireworksToCome.Count > 0 || _game.FireworkResults.Count > 0)
        {
            Section("Fireworks");
            foreach (var result in _game.FireworkResults)
            {
                Muted($"{result.Minute / 60:00}:{result.Minute % 60:00}  {(result.Outcome == FireworkOutcome.CalledOff ? "called off" : $"{result.Stars} star show")}");
            }

            foreach (var time in report.FireworksToCome)
            {
                _body.AddChild(UiStyle.Body($"{time.Hours:00}:{time.Minutes:00}  still to come", size: 14));
            }
        }
    }

    private void People(DayReport report)
    {
        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 6);
        tiles.AddChild(Tile($"{report.Happy}%", Change(report.HappyChange), UiStyle.PinkTint));
        tiles.AddChild(Tile($"{report.OnSite:N0}", "on site now", UiStyle.Ground));
        tiles.AddChild(Tile($"{_game.Crowd.Queuing:N0}", "queuing", UiStyle.Ground));
        _body.AddChild(tiles);
        Section("Happiness through the day");
        _body.AddChild(new MoodChart(report, _game.Day.Date, RainSpells(report)) { CustomMinimumSize = new Vector2(0, 120) });
        Section("What people need");
        Needs(report);
        Section("Comings and goings");
        Line("Came through the gates", $"{_game.Crowd.ThroughGates:N0}");
        Line("Watching an act", $"{_game.Crowd.Watching:N0}");
        Line("At a stall", $"{_game.Crowd.AtStalls:N0}");
        Line("Gave up on a queue", $"{report.GaveUpQueuing:N0}");
        Line("Went home unhappy", $"{report.WentHomeUnhappy:N0}");
        WorthALook(report);
    }

    private void Incidents(DayReport report)
    {
        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 6);
        tiles.AddChild(Tile($"{report.Incidents}", report.OpenIncidents > 0 ? $"incidents, {report.OpenIncidents} open" : "incidents", UiStyle.Ground));
        tiles.AddChild(Tile($"{report.Hurt}", "hurt", report.Hurt > 0 ? AmberTint : UiStyle.Ground));
        tiles.AddChild(Tile($"{report.TakenIll}", "taken ill", UiStyle.Ground));
        _body.AddChild(tiles);

        Section("Injuries and first aid");
        Line("Hurt in a crush", $"{report.HurtInCrush}");
        Line("Hurt by fireworks", $"{report.HurtByFireworks}");
        Line("Treated", $"{report.Treated}");
        Line("Left untreated", $"{report.Untreated}", report.Untreated > 0 ? Bad : null);
        Line("Trouble sorted", $"{report.TroubleSorted}");
        Line("First aid beds in use", report.Beds > 0 ? $"{report.BedsUsed} of {report.Beds}" : "no tent");

        Section("Everything today");
        if (report.Log.Count == 0)
        {
            Muted("Nothing yet: the day is still to start.");
        }

        foreach (var note in report.Log)
        {
            NoteRow(note);
            RowCount++;
        }
    }

    private void Money(DayReport report)
    {
        var tiles = new HBoxContainer();
        tiles.AddThemeConstantOverride("separation", 6);
        tiles.AddChild(Tile(Pounds(report.UpSoFar, signed: true), report.UpSoFar >= 0 ? "up so far" : "down so far", UiStyle.Ground, report.UpSoFar >= 0 ? UiStyle.FirstAidGreen : Bad));
        tiles.AddChild(Tile(Pounds(report.Bank), "in the bank", UiStyle.Ground));
        _body.AddChild(tiles);

        Section("In");
        if (report.In.Count == 0)
        {
            Muted("Nothing yet");
        }

        foreach (var line in report.In)
        {
            Line(line.What == "Tickets" && _game.SoldAhead ? $"Tickets  {_game.TicketsSold:N0} sold" : line.What, Pounds(line.Amount));
            RowCount++;
        }

        Section("Out");
        foreach (var line in report.Out)
        {
            Line(line.What, $"−{Pounds(line.Amount)}");
            RowCount++;
        }

        _body.AddChild(UiStyle.Rule(1));
        var total = new HBoxContainer();
        total.AddChild(UiStyle.Heading(report.UpSoFar >= 0 ? "Up so far" : "Down so far", 18));
        total.AddChild(Spacer());
        var sum = UiStyle.Heading(Pounds(report.UpSoFar, signed: true), 18);
        sum.AddThemeColorOverride("font_color", report.UpSoFar >= 0 ? UiStyle.FirstAidGreen : Bad);
        total.AddChild(sum);
        _body.AddChild(total);
        Line("In the bank", Pounds(report.Bank));
    }

    private void SetRow(SetLine set)
    {
        var row = new PanelContainer();
        var on = set.State == SetState.OnNow;
        row.AddThemeStyleboxOverride("panel", on ? UiStyle.Box(UiStyle.PinkTint, 8, null, 6) : UiStyle.Box(new Color(1, 1, 1, 0), 8, null, 6));
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        row.AddChild(line);
        var faded = set.State is SetState.Played or SetState.Cancelled;
        line.AddChild(UiStyle.Body(set.Start.ToString("HH:mm"), muted: !on, size: 14, bold: true));
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        var name = UiStyle.Body(set.Headliner ? $"{set.Act}  ·  headliner" : set.Act, size: 15, bold: true);
        words.AddChild(name);
        words.AddChild(UiStyle.Body($"{Roster.NameOf(set.Genre)}  ·  {new string('★', set.Stars)}{new string('☆', 5 - set.Stars)}", muted: true, size: 12));
        line.AddChild(words);
        var (tag, colour, tint) = set.State switch
        {
            SetState.OnNow => ("On now", Colors.White, UiStyle.StagePink),
            SetState.Next => ("Next", new Color("#8A6200"), UiStyle.SunTint),
            SetState.Played => ("Played", UiStyle.TextMuted, new Color("#EEF0EC")),
            SetState.Cancelled => ("Cancelled", Bad, new Color("#FBE1E3")),
            _ => ("", UiStyle.TextMuted, new Color(1, 1, 1, 0)),
        };
        if (tag.Length > 0)
        {
            line.AddChild(Pill(tag, colour, tint));
        }

        if (faded)
        {
            row.Modulate = new Color(1, 1, 1, 0.6f);
        }

        _body.AddChild(row);
    }

    private void NoteRow(DayNote note)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(Badge(note.Kind));
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        var what = UiStyle.Body(note.What, size: 14, bold: true);
        what.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.AddChild(what);
        if (note.Outcome.Length > 0)
        {
            var outcome = UiStyle.Body(note.Outcome, size: 12);
            outcome.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            outcome.AddThemeColorOverride("font_color", note.Tone switch { DayTone.Bad => Bad, DayTone.Open => new Color("#A35F00"), _ => UiStyle.TextMuted });
            words.AddChild(outcome);
        }

        row.AddChild(words);
        row.AddChild(UiStyle.Body(note.At.ToString("HH:mm"), muted: true, size: 12));
        _body.AddChild(row);
        _body.AddChild(UiStyle.Rule(1));
    }

    /// <summary>A round badge for the kind of thing: a green cross for first aid, never red.</summary>
    private static Control Badge(DayNoteKind kind)
    {
        var (tint, text, ink) = kind switch
        {
            DayNoteKind.Ill or DayNoteKind.Hurt => (GoodTint, "+", UiStyle.FirstAidGreen),
            DayNoteKind.Crush or DayNoteKind.Choice or DayNoteKind.CalledOff => (AmberTint, "!", new Color("#A35F00")),
            DayNoteKind.Trouble or DayNoteKind.LostChild => (new Color("#EEEAF6"), "!", new Color("#5A4A9A")),
            DayNoteKind.Weather => (RainTint, "~", new Color("#5B7894")),
            DayNoteKind.Fireworks => (UiStyle.SunTint, "*", new Color("#8A6200")),
            _ => (UiStyle.Ground, "·", UiStyle.TextMuted),
        };
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(24, 24), SizeFlagsVertical = SizeFlags.ShrinkBegin };
        badge.AddThemeStyleboxOverride("panel", UiStyle.Box(tint, 12));
        var label = UiStyle.Body(text, size: 16, bold: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeColorOverride("font_color", ink);
        badge.AddChild(label);
        return badge;
    }

    private void Needs(DayReport report)
    {
        Bar("Fed", report.Fed);
        Bar("Watered", report.Watered);
        Bar("Toilets", report.Toilets);
    }

    private void Bar(string what, int percent)
    {
        var row = new HBoxContainer();
        var label = UiStyle.Body(what, size: 14);
        label.CustomMinimumSize = new Vector2(76, 0);
        row.AddChild(label);
        var colour = percent >= 70 ? UiStyle.FirstAidGreen : percent >= 40 ? Amber : Bad;
        var bar = new ProgressBar { Value = percent, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(colour, 5));
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(UiStyle.Border, 5));
        row.AddChild(bar);
        var number = UiStyle.Body($"{percent}%", muted: true, size: 13);
        number.CustomMinimumSize = new Vector2(44, 0);
        number.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(number);
        _body.AddChild(row);
    }

    private void WorthALook(DayReport report)
    {
        if (report.WorthALook.Count == 0)
        {
            return;
        }

        var note = new PanelContainer();
        note.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.SunTint, 10, new Color("#F3DE9A"), 10));
        var text = UiStyle.Body("Worth a look:  " + string.Join("  ·  ", report.WorthALook), size: 13);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        note.AddChild(text);
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });
        _body.AddChild(note);
    }

    private static Control Tile(string big, string small, Color tint, Color? ink = null)
    {
        var tile = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tile.AddThemeStyleboxOverride("panel", UiStyle.Box(tint, 10, null, 8));
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 0);
        var number = UiStyle.Heading(big, 20);
        if (ink is { } colour)
        {
            number.AddThemeColorOverride("font_color", colour);
        }

        box.AddChild(number);
        box.AddChild(UiStyle.Body(small, muted: true, size: 11));
        tile.AddChild(box);
        return tile;
    }

    private static Control Pill(string text, Color ink, Color tint)
    {
        var pill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var box = UiStyle.Box(tint, 99);
        box.ContentMarginLeft = box.ContentMarginRight = 8;
        box.ContentMarginTop = box.ContentMarginBottom = 2;
        pill.AddThemeStyleboxOverride("panel", box);
        var label = UiStyle.Body(text, size: 11, bold: true);
        label.AddThemeColorOverride("font_color", ink);
        pill.AddChild(label);
        return pill;
    }

    private void Section(string text)
    {
        _body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _body.AddChild(UiStyle.Caps(text, pink: true, size: 11));
    }

    private void Muted(string text)
    {
        var label = UiStyle.Body(text, muted: true, size: 13);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _body.AddChild(label);
    }

    private void Line(string what, string amount, Color? colour = null)
    {
        var row = new HBoxContainer();
        var left = UiStyle.Body(what, size: 14);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(left);
        var right = UiStyle.Body(amount, size: 14);
        if (colour is { } ink)
        {
            right.AddThemeColorOverride("font_color", ink);
        }

        row.AddChild(right);
        _body.AddChild(row);
    }

    private static Control Spacer() => new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };

    private static string Change(int change) => change == 0 ? "happy" : change > 0 ? $"happy, up {change}" : $"happy, down {-change}";

    private static string Pounds(int amount, bool signed = false) => signed
        ? $"{(amount >= 0 ? "+" : "−")}£{System.Math.Abs(amount):N0}"
        : $"£{amount:N0}";

    /// <summary>When it rained today, from the log: each spell from when it started to when it stopped (or now).</summary>
    private List<(System.DateTime From, System.DateTime To)> RainSpells(DayReport report)
    {
        var spells = new List<(System.DateTime, System.DateTime)>();
        System.DateTime? from = null;
        foreach (var note in report.Log.Where(n => n.Kind == DayNoteKind.Weather).OrderBy(n => n.At))
        {
            if (note.What == "The rain stopped")
            {
                if (from is { } start)
                {
                    spells.Add((start, note.At));
                }

                from = null;
            }
            else
            {
                from ??= note.At;
            }
        }

        if (from is { } still)
        {
            spells.Add((still, report.Now));
        }

        return spells;
    }
}

/// <summary>The crowd's happiness through the day as a pink line, from 11:00 to midnight, with rain shaded.</summary>
public partial class MoodChart : Control
{
    private const double FirstHour = 10;
    private const double LastHour = 24;
    private readonly IReadOnlyList<MoodPoint> _points;
    private readonly System.DateTime _day;
    private readonly List<(System.DateTime From, System.DateTime To)> _rain;

    public MoodChart()
    {
        _points = System.Array.Empty<MoodPoint>();
        _rain = new();
    }

    public MoodChart(DayReport report, System.DateTime day, List<(System.DateTime, System.DateTime)> rain)
    {
        (_points, _day, _rain) = (report.MoodLine, day, rain);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override void _Draw()
    {
        var font = UiStyle.Theme.DefaultFont;
        const float labels = 16;
        var w = Size.X;
        var h = Size.Y - labels;
        float X(System.DateTime t) => (float)(((t - _day).TotalHours - FirstHour) / (LastHour - FirstHour) * w);
        float Y(int happy) => h - (happy / 100f * h);

        DrawRect(new Rect2(0, 0, w, h), UiStyle.Ground);
        foreach (var (from, to) in _rain)
        {
            DrawRect(new Rect2(X(from), 0, Mathf.Max(2, X(to) - X(from)), h), DayPanel.RainTint);
        }

        for (var level = 25; level < 100; level += 25)
        {
            DrawDashedLine(new Vector2(0, Y(level)), new Vector2(w, Y(level)), UiStyle.Border, 1, 4);
        }

        for (var hour = 12; hour <= 22; hour += 2)
        {
            var x = X(_day.AddHours(hour));
            DrawString(font, new Vector2(x - 16, h + 13), $"{hour}:00", HorizontalAlignment.Center, 32, 10, UiStyle.TextMuted);
        }

        if (_points.Count == 0)
        {
            DrawString(font, new Vector2(8, h / 2), "Happiness shows here once people arrive", HorizontalAlignment.Left, -1, 12, UiStyle.TextMuted);
            return;
        }

        var line = _points.Select(p => new Vector2(X(p.At), Y(p.Happy))).ToArray();
        if (line.Length > 1)
        {
            DrawPolyline(line, UiStyle.StagePink, 2.5f, true);
        }

        DrawCircle(line[^1], 4, UiStyle.StagePink);
    }
}
