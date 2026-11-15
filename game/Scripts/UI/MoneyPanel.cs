using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The money panel: what has come in and gone out today, line by line, and how people feel.
/// Opened by clicking the money in the top bar.
/// </summary>
public partial class MoneyPanel : PanelContainer
{
    private FestivalGame _game = null!;
    private VBoxContainer _box = null!;
    private ScrollContainer _scroll = null!;

    /// <summary>How far above the bottom of the screen the panel stops when the build bar is not showing.</summary>
    private const int BottomClear = 96;

    /// <summary>
    /// Where the build bar starts, counted down from the top of the screen, while it shows. A long
    /// list scrolls inside the panel rather than running down over the bar.
    /// </summary>
    public float? BuildBarTop { get; set; }
    private double _sinceRefresh = 1;
    private string _shown = "";

    /// <summary>How many lines of income and spending are showing.</summary>
    public int LineCount { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Theme = UiStyle.Theme;
        CustomMinimumSize = new Vector2(330, 0);
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 20, radius: 12));
        _box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _box.AddThemeConstantOverride("separation", 6);
        _scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _scroll.AddChild(_box);
        AddChild(_scroll);
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 22);
        GrowHorizontal = GrowDirection.Begin;
        Position = new Vector2(Position.X, UiStyle.TopClear);
        Refresh();
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            Fit();
        }

        _sinceRefresh += delta;
        if (Visible && _sinceRefresh > 0.4)
        {
            Refresh();
        }
    }

    /// <summary>Makes the panel as tall as its lines, but no lower than the build bar or the view buttons.</summary>
    public void Fit()
    {
        var screen = GetViewportRect().Size;
        var bottom = screen.Y - BottomClear;
        if (BuildBarTop is { } bar)
        {
            bottom = Mathf.Min(bottom, bar - 10);
        }

        var frame = GetThemeStylebox("panel");
        var room = bottom - Position.Y - (frame?.GetMinimumSize().Y ?? 0);
        var want = Mathf.Clamp(_box.GetCombinedMinimumSize().Y, 0, Mathf.Max(160, room));
        if (!Mathf.IsEqualApprox(_scroll.CustomMinimumSize.Y, want))
        {
            _scroll.CustomMinimumSize = new Vector2(0, want);
            Size = new Vector2(Size.X, 0);
        }
    }

    public void Refresh()
    {
        _sinceRefresh = 0;
        var crowd = _game.Crowd;
        var key = $"{_game.Money}|{_game.TotalIncome}|{_game.TotalSpending}|{_game.Income.Count}|{_game.Spending.Count}|{crowd.Happiness}|{crowd.Content(Need.Food)}|{crowd.Content(Need.Drink)}|{crowd.Content(Need.Toilet)}|{crowd.ThroughGates}";
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

        LineCount = 0;
        _box.AddChild(UiStyle.Caps("The takings", pink: true));
        _box.AddChild(UiStyle.Heading("Today's money", 26));
        _box.AddChild(UiStyle.Caps("In"));
        foreach (var line in _game.Income)
        {
            var what = line.What != "Tickets" ? line.What : _game.SoldAhead ? $"Tickets  {_game.TicketsSold} sold" : $"Tickets  {crowd.ThroughGates} at £{_game.TicketPrice}";
            Row(what, $"£{line.Amount:N0}");
            LineCount++;
        }

        if (_game.Income.Count == 0)
        {
            Row("Nothing yet", "", muted: true);
        }

        _box.AddChild(new HSeparator());
        _box.AddChild(UiStyle.Caps("Out"));
        foreach (var line in _game.Spending)
        {
            Row(line.What, $"£{line.Amount:N0}");
            LineCount++;
        }

        if (_game.Spending.Count == 0)
        {
            Row("Nothing yet", "", muted: true);
        }

        _box.AddChild(new HSeparator());
        var net = _game.TotalIncome - _game.TotalSpending;
        Row(net >= 0 ? "Up so far" : "Down so far", $"£{System.Math.Abs(net):N0}", total: true);
        Row("In the bank", $"£{_game.Money:N0}");

        _box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        _box.AddChild(UiStyle.Caps("How people feel"));
        Bar("Happy", crowd.Happiness);
        Bar("Fed", crowd.Content(Need.Food));
        Bar("Watered", crowd.Content(Need.Drink));
        Bar("Toilets", crowd.Content(Need.Toilet));
        if (crowd.WentHomeUnhappy > 0)
        {
            Row($"{crowd.WentHomeUnhappy} went home unhappy", "", muted: true);
        }

        if (crowd.GaveUpQueuing > 0)
        {
            Row($"{crowd.GaveUpQueuing} gave up on a queue", "", muted: true);
        }
    }

    private void Row(string what, string amount, bool total = false, bool muted = false)
    {
        var row = new HBoxContainer();
        var left = total ? UiStyle.Heading(what, 17) : UiStyle.Body(what, muted, 15);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(left);
        row.AddChild(total ? UiStyle.Heading(amount, 17) : UiStyle.Body(amount, muted, 15));
        _box.AddChild(row);
    }

    /// <summary>A bar from 0 to 100: green when most people are fine, amber when many are not, red when most are not.</summary>
    private void Bar(string what, int percent)
    {
        var row = new HBoxContainer();
        var label = UiStyle.Body(what, size: 15);
        label.CustomMinimumSize = new Vector2(80, 0);
        row.AddChild(label);
        var colour = percent >= 70 ? UiStyle.FirstAidGreen : percent >= 40 ? new Color("#E8A317") : new Color("#D9483B");
        var bar = new ProgressBar { Value = percent, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(colour, 7));
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(UiStyle.Border, 7));
        row.AddChild(bar);
        var number = UiStyle.Body($"{percent}%", muted: true, size: 14);
        number.CustomMinimumSize = new Vector2(44, 0);
        number.HorizontalAlignment = HorizontalAlignment.Right;
        row.AddChild(number);
        _box.AddChild(row);
    }
}
