using System.Collections.Generic;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>What the results screen shows.</summary>
public sealed record ResultsView(int Year, string Festival, int Came, int Expected, int Happy, int WentHome, IReadOnlyList<(string What, int Amount)> In,
    IReadOnlyList<(string What, int Amount)> Out, int LitterCleared, int GrassWorn, string Verdict, int LicenceNow, int LicenceNext,
    int Treated = 0, int Untreated = 0, int TroubleSorted = 0, int TroubleIgnored = 0, int CrushInjuries = 0, int PulledOut = 0, int FieldRepair = 0, string Weather = "");

/// <summary>
/// The screens of the festival year that sit over the field: planning before, results after.
/// They only show what they are given; the rules decide what that is.
/// </summary>
public static partial class YearPanels
{
    internal static PanelContainer Card(int width)
    {
        var panel = new PanelContainer { Theme = UiStyle.Theme, CustomMinimumSize = new Vector2(width, 0) };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 28, radius: 20));
        return panel;
    }

    internal static Label Section(string text)
    {
        var label = UiStyle.Body(text.ToUpperInvariant(), muted: true, size: 13);
        return label;
    }

    internal static PanelContainer Tile(bool picked, int width, System.Action? pressed = null)
    {
        var tile = new PanelContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        if (pressed is not null)
        {
            tile.MouseFilter = Control.MouseFilterEnum.Stop;
            tile.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            tile.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    pressed();
                }
            };
        }

        var box = UiStyle.Box(picked ? UiStyle.PinkTint : UiStyle.Surface, 12, picked ? UiStyle.StagePink : UiStyle.Border, 14);
        if (picked)
        {
            box.BorderWidthBottom = box.BorderWidthTop = box.BorderWidthLeft = box.BorderWidthRight = 2;
        }

        tile.AddThemeStyleboxOverride("panel", box);
        return tile;
    }

    internal static HBoxContainer MoneyRow(string what, string amount, bool strong = false, bool muted = false)
    {
        var row = new HBoxContainer();
        var left = strong ? UiStyle.Heading(what, 18) : UiStyle.Body(what, muted, 15);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        row.AddChild(strong ? UiStyle.Heading(amount, 18) : UiStyle.Body(amount, muted, 15));
        return row;
    }

    /// <summary>The results screen: how the day went, the money, the field, and what next year's licence is.</summary>
    public static Control Results(ResultsView view, System.Action? next = null, bool closed = false)
    {
        var panel = Card(760);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 7);
        panel.AddChild(box);

        // Kept tight, so the whole panel and its button fit on a 900 pixel high screen.
        box.AddChild(UiStyle.Heading($"Year {view.Year} results", 28));
        box.AddChild(UiStyle.Body(view.Weather.Length > 0 ? $"{view.Festival}  ·  {view.Weather}" : view.Festival, muted: true, size: 15));

        var takings = 0;
        foreach (var (_, amount) in view.In)
        {
            takings += amount;
        }

        var costs = 0;
        foreach (var (_, amount) in view.Out)
        {
            costs += amount;
        }

        var profit = takings - costs;
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        foreach (var (big, small) in new[]
        {
            ($"{view.Came}", $"came, of {view.Expected} expected"),
            ($"{view.Happy}%", view.WentHome > 0 ? $"happy. {view.WentHome} went home early" : "happy"),
            ($"{(profit < 0 ? "-" : "")}£{System.Math.Abs(profit):N0}", profit >= 0 ? "profit" : "loss"),
        })
        {
            var tile = Tile(false, 200);
            var inner = new VBoxContainer();
            inner.AddChild(UiStyle.Heading(big, 30));
            inner.AddChild(UiStyle.Body(small, muted: true, size: 14));
            tile.AddChild(inner);
            top.AddChild(tile);
        }

        box.AddChild(top);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 36);
        foreach (var (title, lines, total) in new[] { ("Money in", view.In, takings), ("Money out", view.Out, costs) })
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 5);
            column.AddChild(Section(title));
            foreach (var (what, amount) in lines)
            {
                column.AddChild(MoneyRow(what, $"£{amount:N0}"));
            }

            column.AddChild(new HSeparator());
            column.AddChild(MoneyRow("Total", $"£{total:N0}", strong: true));
            columns.AddChild(column);
        }

        box.AddChild(columns);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        box.AddChild(Section("The field afterwards"));
        box.AddChild(MoneyRow("Litter cleared", $"{view.LitterCleared}%"));
        box.AddChild(MoneyRow("Grass worn out", view.FieldRepair > 0 ? $"{view.GrassWorn}%. The farmer charged £{view.FieldRepair:N0} to put it right" : $"{view.GrassWorn}%"));
        box.AddChild(Section("People looked after"));
        var crush = (view.CrushInjuries > 0 ? $", {view.CrushInjuries} hurt in a crush" : "") + (view.PulledOut > 0 ? $", {view.PulledOut} lifted out of a crush in time" : "");
        box.AddChild(MoneyRow("Taken ill or hurt", (view.Untreated > 0 ? $"{view.Treated} treated, {view.Untreated} left with no help" : $"{view.Treated} treated") + crush));
        box.AddChild(MoneyRow("Trouble", view.TroubleIgnored > 0 ? $"{view.TroubleSorted} sorted out, {view.TroubleIgnored} nobody dealt with" : $"{view.TroubleSorted} sorted out"));

        var verdict = new PanelContainer();
        var grew = view.LicenceNext > view.LicenceNow;
        verdict.AddThemeStyleboxOverride("panel", UiStyle.Box(grew ? UiStyle.PinkTint : UiStyle.Ground, 12, grew ? UiStyle.StagePink : UiStyle.Border, 16));
        var words = new VBoxContainer();
        words.AddChild(Section("The parish council says"));
        var said = UiStyle.Body(view.Verdict, size: 16);
        said.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.AddChild(said);
        words.AddChild(UiStyle.Heading(closed ? "The festival has closed" : grew ? $"Next year: licence for up to {view.LicenceNext:N0} people" : $"Next year: licence stays at {view.LicenceNext:N0} people", 19));
        verdict.AddChild(words);
        box.AddChild(verdict);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddChild(UiStyle.Button(closed ? "Back to the menu" : $"Plan year {view.Year + 1}", next ?? (() => { })));
        box.AddChild(buttons);
        return panel;
    }

    /// <summary>The small panel shown while building. In a year that sells ahead it is build week, and each day sells the last tickets.</summary>
    public static Control BuildDay(int expected, int bank, System.Action? open = null, string forecast = "", BuildWeekView? week = null,
        System.Action? nextDay = null, System.Action<int>? price = null, TravelView? travel = null, System.Action<int>? parking = null, System.Action<int>? buses = null)
    {
        var panel = Card(360);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading(week is null ? "Build day" : week.Day is null ? "Build week: ready" : $"Build week: {week.Day}", 22));
        var words = UiStyle.Body(week is null
            ? "Friday. Nobody is here yet and the clock is stopped. Put up what you need, then open the gates."
            : "Nobody is here yet and the clock is stopped. Put up what you need. Ending each day sells that day's tickets.", muted: true, size: 14);
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(words);
        if (forecast.Length > 0)
        {
            var weather = new PanelContainer();
            weather.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Ground, 10, UiStyle.Border, 12));
            var lines = new VBoxContainer();
            lines.AddChild(Section(week is null ? "Tomorrow's weather" : "Saturday's weather"));
            var said = UiStyle.Body(forecast, size: 15);
            said.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            said.CustomMinimumSize = new Vector2(270, 0);
            lines.AddChild(said);
            weather.AddChild(lines);
            box.AddChild(weather);
        }

        if (week is null)
        {
            box.AddChild(MoneyRow("Expected tomorrow", $"about {expected}"));
        }
        else
        {
            box.AddChild(MoneyRow("Tickets sold", week.Sold >= week.Licence ? $"{week.Sold:N0}, sold out" : $"{week.Sold:N0} of {week.Licence:N0}"));
            if (week.Day is not null && week.Sold < week.Licence)
            {
                box.AddChild(MoneyRow("Likely to sell today", $"about {week.Forecast:N0}", muted: true));
                box.AddChild(MoneyRow("Adverts today", $"£{week.AdCost:N0}", muted: true));
                box.AddChild(PriceRow(week.Price, week.Fair, price));
            }
        }

        if (travel is not null)
        {
            box.AddChild(Travel(travel, parking, buses, wide: false));
        }

        box.AddChild(MoneyRow("Left to build with", $"£{bank:N0}"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        if (week?.Day is not null)
        {
            box.AddChild(UiStyle.Button($"End {week.Day}", nextDay ?? (() => { }), BlueprintFestival.Simulation.Sound.Sounds.Coins));
            box.AddChild(UiStyle.SecondaryButton("Skip the rest and open the gates", open ?? (() => { })));
        }
        else
        {
            box.AddChild(UiStyle.Button("Open the gates", open ?? (() => { })));
        }

        return panel;
    }

    /// <summary>The small panel shown the morning after, while the field is tidied.</summary>
    public static Control Tidy(int litterLeft, int litterAtStart, int scoutsFee, System.Action? pay = null, System.Action? done = null)
    {
        var panel = Card(360);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading("The morning after", 22));
        var words = UiStyle.Body("The farmer wants his field back as he lent it. Click litter to pick it up, or pay for help.", muted: true, size: 14);
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(words);
        box.AddChild(MoneyRow("Litter left", $"{litterLeft} of {litterAtStart}"));
        var bar = new ProgressBar { Value = 100.0 * (litterAtStart - litterLeft) / System.Math.Max(1, litterAtStart), ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(new Color("#1E9E52"), 7));
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(UiStyle.Border, 7));
        box.AddChild(bar);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        if (litterLeft > 0)
        {
            box.AddChild(UiStyle.SecondaryButton($"Pay the scouts £{scoutsFee} to finish", pay ?? (() => { })));
        }

        box.AddChild(UiStyle.Button("Hand the field back", done ?? (() => { })));
        return panel;
    }
}
