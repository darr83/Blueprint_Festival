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
    private double _sinceRefresh = 1;
    private string _shown = "";

    /// <summary>How many lines of income and spending are showing.</summary>
    public int LineCount { get; private set; }

    public void Initialise(FestivalGame game)
    {
        _game = game;
        Theme = UiStyle.Theme;
        CustomMinimumSize = new Vector2(330, 0);
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 18));
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 6);
        AddChild(_box);
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 18);
        GrowHorizontal = GrowDirection.Begin;
        Position = new Vector2(Position.X, 62);
        Refresh();
    }

    public override void _Process(double delta)
    {
        _sinceRefresh += delta;
        if (Visible && _sinceRefresh > 0.4)
        {
            Refresh();
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
        _box.AddChild(UiStyle.Heading("Today's money", 20));
        Row("In", "", muted: true);
        foreach (var line in _game.Income)
        {
            var what = line.What == "Tickets" ? $"Tickets  {crowd.ThroughGates} at £{_game.TicketPrice}" : line.What;
            Row(what, $"£{line.Amount:N0}");
            LineCount++;
        }

        if (_game.Income.Count == 0)
        {
            Row("Nothing yet", "", muted: true);
        }

        _box.AddChild(new HSeparator());
        Row("Out", "", muted: true);
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
        _box.AddChild(UiStyle.Heading("How people feel", 17));
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
        var colour = percent >= 70 ? new Color("#1E9E52") : percent >= 40 ? new Color("#E8A317") : new Color("#D9483B");
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
