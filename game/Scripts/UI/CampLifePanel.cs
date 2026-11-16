using System.Linq;
using BlueprintFestival.Simulation;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The camping field after the music, down the right of the screen. At night: how many campers are
/// up round the fires and how many asleep, how quiet, safe and warm the camp is, what has gone
/// wrong or been sorted, and buttons to send security or ask for quiet. In the morning: how many
/// have gone home, the shower and toilet queues, breakfast, and the tents left behind.
/// </summary>
public partial class CampLifePanel : Paper
{
    public const int PanelWidth = 380;

    private static readonly Color Good = new("#1E9E52");
    private static readonly Color Fair = new("#E08A1E");
    private static readonly Color Bad = new("#D9483B");

    private FestivalGame _game = null!;
    private VBoxContainer _box = null!;
    private double _since = 1;
    private string _shown = "";
    private float? _buildBarTop;

    public CampLifePanel() : base(18, small: true)
    {
    }

    /// <summary>Where the build bar starts while it shows, so the panel stops above it.</summary>
    public float? BuildBarTop
    {
        get => _buildBarTop;
        set
        {
            if (_buildBarTop != value)
            {
                _buildBarTop = value;
                Place();
            }
        }
    }

    /// <summary>The buttons showing, by their words, for the smoke test.</summary>
    public string[] Buttons => _box.FindChildren("*", nameof(Button), true, false).OfType<Button>().Select(b => b.Text).ToArray();

    /// <summary>The panel's heading just now, for the smoke test.</summary>
    public string Title { get; private set; } = "";

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Name = "CampLifePanel";
        CustomMinimumSize = new Vector2(PanelWidth, 0);
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 6);
        AddChild(_box);
        Visible = false;
    }

    public override void _Ready()
    {
        Place();
        GetViewport().SizeChanged += Place;
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= Place;
    }

    private void Place()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var screen = GetViewportRect().Size;
        Position = new Vector2(screen.X - 22 - PanelWidth, UiStyle.TopClear);
        Size = new Vector2(PanelWidth, 0);
        var bottom = _buildBarTop is { } bar ? Mathf.Min(screen.Y - 96, bar - 10) : screen.Y - 96;
        if (Position.Y + Size.Y > bottom)
        {
            Position = new Vector2(Position.X, Mathf.Max(UiStyle.TopClear, bottom - Size.Y));
        }
    }

    public override void _Process(double delta)
    {
        _since += delta;
        if (!Visible || _since < 0.4)
        {
            return;
        }

        _since = 0;
        Refresh();
    }

    /// <summary>Fills the panel in again from the game.</summary>
    public void Refresh()
    {
        var g = _game;
        var key = $"{g.CampTime}|{g.Now:HH:mm}|{g.CampLog.Count}|{g.SecuritySentToCamp}|{g.QuietUntil}";
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

        if (g.CampTime == CampTime.Night)
        {
            Night();
        }
        else if (g.CampTime == CampTime.Morning)
        {
            Morning();
        }

        CallDeferred(MethodName.Place);
    }

    private void Night()
    {
        var g = _game;
        Title = "Camp life after the music";
        _box.AddChild(UiStyle.Caps("The camping field tonight", pink: true, size: 11));
        _box.AddChild(UiStyle.Heading(Title, 24));
        Row("In their tents", $"{g.NightCampers:N0}");
        Row("Still up round the fires", $"{g.CampersUp:N0}");
        Row("Asleep", $"{g.CampersAsleep:N0}");
        _box.AddChild(UiStyle.Rule(1));
        _box.AddChild(UiStyle.Caps("How the camp is sleeping", size: 11));
        Bar("Quiet", g.CampQuiet);
        Bar("Safe", g.CampSafe);
        Bar("Warm and dry", g.CampWarm);
        Log("Tonight so far");

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        var send = UiStyle.SecondaryButton(g.SecuritySentToCamp ? "Security sent" : "Send security", () => { g.SendSecurityToCamp(); _shown = ""; Refresh(); });
        send.Disabled = g.SecuritySentToCamp;
        send.TooltipText = "Send every member of security to walk the tents for the rest of the night. They stop thieves.";
        send.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        buttons.AddChild(send);
        var hush = UiStyle.SecondaryButton("Quiet please", () => { g.AskForQuiet(); _shown = ""; Refresh(); });
        hush.Disabled = g.QuietUntil is { } q && g.Now < q;
        hush.TooltipText = "Ask the whole camp to keep it down for an hour: fewer loud parties, and people turn in sooner.";
        hush.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        buttons.AddChild(hush);
        _box.AddChild(buttons);
        _box.AddChild(UiStyle.Button("Skip to the morning", () => { g.SkipCampNight(); _shown = ""; Refresh(); }));
    }

    private void Morning()
    {
        var g = _game;
        Title = "Campers wake up";
        _box.AddChild(UiStyle.Caps("The morning after", pink: true, size: 11));
        _box.AddChild(UiStyle.Heading(Title, 24));
        var about = UiStyle.Body("Campers want the toilets, a shower and breakfast, then pack up and go. Everyone is gone by noon, then you tidy.", muted: true, size: 13);
        about.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _box.AddChild(about);
        Row("Gone home", $"{g.CampersGoneHome:N0} of {g.NightCampers:N0}");
        Bar(null, g.NightCampers == 0 ? 1 : g.CampersGoneHome / (double)g.NightCampers, Colors.Teal);
        Row("Shower queue", g.ShowerQueueMinutes == 0 ? "no showers" : $"{g.ShowerQueueMinutes} min", g.ShowerQueueMinutes is 0 or > 20 ? Bad : g.ShowerQueueMinutes > 10 ? Fair : Good);
        Row("Toilet queue", g.ToiletQueueMinutes == 0 ? "no toilets" : $"{g.ToiletQueueMinutes} min", g.ToiletQueueMinutes is 0 or > 10 ? Bad : g.ToiletQueueMinutes > 5 ? Fair : Good);
        Row("Breakfast sold", $"£{g.BreakfastTakings:N0}");
        _box.AddChild(UiStyle.Rule(1));
        _box.AddChild(UiStyle.Caps("Left behind so far", size: 11));
        Row("Tents", g.TentsLeft == 0 ? "none" : $"{g.TentsLeft:N0} · £{FestivalGame.TentLeftCost} each to clear");
        var note = UiStyle.Body("Rain, a bad night or a bad day make more people give up and leave their tent.", muted: true, size: 13);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _box.AddChild(note);
        Log("This morning");
        _box.AddChild(UiStyle.Button("Skip to the tidy-up", () => { g.SkipCampMorning(); _shown = ""; Refresh(); }));
    }

    private void Row(string what, string value, Color? colour = null)
    {
        var row = new HBoxContainer();
        var left = UiStyle.Body(what, size: 14);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(left);
        var right = UiStyle.Body(value, size: 14, bold: true);
        if (colour is { } c)
        {
            right.AddThemeColorOverride("font_color", c);
        }

        row.AddChild(right);
        _box.AddChild(row);
    }

    private void Bar(string? what, double share, Color? colour = null)
    {
        share = Mathf.Clamp(share, 0, 1);
        var (word, tone) = share >= 0.8 ? ("Good", Good) : share >= 0.55 ? ("Fair", Fair) : ("Poor", Bad);
        if (what is not null)
        {
            Row(what, word, tone);
        }

        var bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001, Value = share, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(new Color("#ECE9EE"), 4, new Color("#ECE9EE")));
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(colour ?? tone, 4, colour ?? tone));
        _box.AddChild(bar);
    }

    private void Log(string heading)
    {
        var lines = _game.CampLog.Reverse().Take(6).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        _box.AddChild(UiStyle.Rule(1));
        _box.AddChild(UiStyle.Caps(heading, size: 11));
        foreach (var line in lines)
        {
            var text = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
            text.AddThemeFontSizeOverride("normal_font_size", 13);
            text.AddThemeColorOverride("default_color", UiStyle.NightInk);
            var tone = line.Tone switch { DayTone.Good => Good, DayTone.Bad => Bad, _ => UiStyle.TextMuted };
            var outcome = line.Outcome.Length > 0 ? $" · [color=#{tone.ToHtml(false)}]{line.Outcome}[/color]" : "";
            text.Text = $"[color=#{UiStyle.TextMuted.ToHtml(false)}]{line.At:HH:mm}[/color]  {line.Text}{outcome}";
            _box.AddChild(text);
        }
    }
}
