using System.Collections.Generic;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>What the results screen shows.</summary>
public sealed record ResultsView(int Year, string Festival, int Came, int Expected, int Happy, int WentHome, IReadOnlyList<(string What, int Amount)> In,
    IReadOnlyList<(string What, int Amount)> Out, int LitterCleared, int GrassWorn, string Verdict, int LicenceNow, int LicenceNext,
    int Treated = 0, int Untreated = 0, int TroubleSorted = 0, int TroubleIgnored = 0, int CrushInjuries = 0, int PulledOut = 0, int FieldRepair = 0, string Weather = "")
{
    /// <summary>What the player did about crushes: announcements, security sent in, acts cancelled, and people hurt in a crush left alone.</summary>
    public (int Announcements, int SecuritySent, int ActsCancelled, int HurtLeftAlone) CrushChoices { get; init; }
}

/// <summary>
/// The screens of the festival year that sit over the field: planning before, results after.
/// They only show what they are given; the rules decide what that is.
/// </summary>
public static partial class YearPanels
{
    /// <summary>What was done about crushes, in words, or null if nothing had to be.</summary>
    public static string? CrushLine((int Announcements, int SecuritySent, int ActsCancelled, int HurtLeftAlone) c)
    {
        var parts = new List<string>();
        if (c.Announcements > 0)
        {
            parts.Add(c.Announcements == 1 ? "the music stopped once for an announcement" : $"the music stopped {c.Announcements} times for announcements");
        }

        if (c.SecuritySent > 0)
        {
            parts.Add(c.SecuritySent == 1 ? "security sent in once" : $"security sent in {c.SecuritySent} times");
        }

        if (c.ActsCancelled > 0)
        {
            parts.Add(c.ActsCancelled == 1 ? "one act cancelled" : $"{c.ActsCancelled} acts cancelled");
        }

        if (c.HurtLeftAlone > 0)
        {
            parts.Add($"{c.HurtLeftAlone} hurt in a crush left alone");
        }

        return parts.Count == 0 ? null : "Crushes: " + string.Join(", ", parts) + ".";
    }

    /// <summary>A sheet of paper with bunting along the top, as wide as asked.</summary>
    internal static PanelContainer Card(int width)
    {
        var small = width < 500;
        return new Paper(small ? 22 : 34, small) { CustomMinimumSize = new Vector2(width, 0) };
    }

    internal static Label Section(string text) => UiStyle.Caps(text);

    /// <summary>The top of a sheet: a pink line of small capitals, the heading under it, and an ink rule.</summary>
    internal static void Masthead(Container box, string kicker, string heading, Control? right = null, int size = 40)
    {
        var row = new HBoxContainer();
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", -2);
        words.AddChild(UiStyle.Caps(kicker, pink: true, size: 13));
        words.AddChild(UiStyle.Heading(heading, size));
        row.AddChild(words);
        if (right is not null)
        {
            right.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
            row.AddChild(right);
        }

        box.AddChild(row);
        box.AddChild(UiStyle.Rule());
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

        tile.AddThemeStyleboxOverride("panel", UiStyle.Outlined(picked ? UiStyle.SunTint : UiStyle.Surface, 10, 12, picked ? UiStyle.NightInk : UiStyle.Border));
        return tile;
    }

    internal static HBoxContainer MoneyRow(string what, string amount, bool strong = false, bool muted = false)
    {
        var row = new HBoxContainer();
        var left = strong ? UiStyle.Heading(what, 19) : UiStyle.Body(what, muted, 15);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        row.AddChild(strong ? UiStyle.Heading(amount, 19) : UiStyle.Body(amount, muted, 15, bold: !muted));
        return row;
    }

    /// <summary>A big number in the poster font with small capitals under it.</summary>
    internal static VBoxContainer BigNumber(string number, string caption, Color? colour = null, int size = 52, string? name = null)
    {
        var cell = new VBoxContainer();
        cell.AddThemeConstantOverride("separation", -6);
        var big = UiStyle.Heading(number, size);
        if (name is not null)
        {
            big.Name = name;
        }

        if (colour is { } tint)
        {
            big.AddThemeColorOverride("font_color", tint);
        }

        cell.AddChild(big);
        cell.AddChild(UiStyle.Caps(caption));
        return cell;
    }

    /// <summary>The first sentence of some words, and the rest.</summary>
    public static (string First, string After) Headline(string words)
    {
        var stop = words.IndexOf(". ", System.StringComparison.Ordinal);
        return stop < 0 ? (words.TrimEnd('.'), "") : (words[..stop], words[(stop + 2)..]);
    }

    /// <summary>
    /// The results screen, laid out as the front page of the parish newsletter: the council's verdict as
    /// the headline, the day in four big numbers, then the money and how the field and the people fared.
    /// </summary>
    public static Control Results(ResultsView view, System.Action? next = null, bool closed = false)
    {
        var panel = Card(1040);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

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
        var grew = view.LicenceNext > view.LicenceNow;
        var (headline, rest) = Headline(view.Verdict);

        var kicker = UiStyle.Caps($"The parish newsletter  ·  Year {view.Year} results", pink: true, size: 13);
        kicker.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(kicker);
        var said = UiStyle.Heading($"\u201C{headline}\u201D", headline.Length > 44 ? 30 : headline.Length > 30 ? 38 : 46);
        said.HorizontalAlignment = HorizontalAlignment.Center;
        said.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        said.CustomMinimumSize = new Vector2(940, 0);
        box.AddChild(said);
        if (rest.Length > 0)
        {
            var more = UiStyle.Body(rest, size: 15);
            more.HorizontalAlignment = HorizontalAlignment.Center;
            more.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            more.CustomMinimumSize = new Vector2(940, 0);
            box.AddChild(more);
        }

        var where = UiStyle.Body(view.Weather.Length > 0 ? $"{view.Festival}  ·  {view.Weather}" : view.Festival, muted: true, size: 14);
        where.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(where);
        var licence = UiStyle.Heading(closed ? "The festival has closed" : grew ? $"Next year: licence for up to {view.LicenceNext:N0} people" : $"Next year: licence stays at {view.LicenceNext:N0} people", 20);
        licence.HorizontalAlignment = HorizontalAlignment.Center;
        if (grew)
        {
            licence.AddThemeColorOverride("font_color", UiStyle.StagePink);
        }

        box.AddChild(licence);
        box.AddChild(UiStyle.Rule());

        var top = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        top.AddThemeConstantOverride("separation", 70);
        foreach (var (big, small, colour) in new (string, string, Color?)[]
        {
            ($"{view.Came:N0}", $"came, of {view.Expected:N0}", null),
            ($"{view.Happy}%", view.WentHome > 0 ? $"happy  ·  {view.WentHome} left early" : "happy", view.Happy >= 70 ? UiStyle.FirstAidGreen : null),
            ($"{(profit < 0 ? "-" : "")}£{System.Math.Abs(profit):N0}", profit >= 0 ? "profit" : "loss", profit >= 0 ? UiStyle.StagePink : null),
            ($"{view.LitterCleared}%", "litter cleared", null),
        })
        {
            var cell = BigNumber(big, small, colour, 50);
            foreach (var child in cell.GetChildren())
            {
                ((Label)child).HorizontalAlignment = HorizontalAlignment.Center;
            }

            top.AddChild(cell);
        }

        box.AddChild(top);
        box.AddChild(UiStyle.Rule(1));

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 40);
        foreach (var (title, lines, total) in new[] { ("Money in", view.In, takings), ("Money out", view.Out, costs) })
        {
            var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            column.AddThemeConstantOverride("separation", 3);
            column.AddChild(Section(title));
            foreach (var (what, amount) in lines)
            {
                column.AddChild(MoneyRow(what, $"£{amount:N0}"));
            }

            column.AddChild(new HSeparator());
            column.AddChild(MoneyRow("Total", $"£{total:N0}", strong: true));
            columns.AddChild(column);
        }

        var after = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
        after.AddThemeConstantOverride("separation", 3);
        after.AddChild(Section("The field afterwards"));
        after.AddChild(Wrapped(view.FieldRepair > 0 ? $"Grass worn out: {view.GrassWorn}%. The farmer charged £{view.FieldRepair:N0} to put it right." : $"Grass worn out: {view.GrassWorn}%.", 290));
        after.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        after.AddChild(Section("People looked after"));
        var crush = (view.CrushInjuries > 0 ? $", {view.CrushInjuries} hurt in a crush" : "") + (view.PulledOut > 0 ? $", {view.PulledOut} lifted out of a crush in time" : "");
        after.AddChild(Wrapped("Taken ill or hurt: " + (view.Untreated > 0 ? $"{view.Treated} treated, {view.Untreated} left with no help" : $"{view.Treated} treated") + crush + ".", 290));
        if (CrushLine(view.CrushChoices) is { } choices)
        {
            after.AddChild(Wrapped(choices, 290));
        }

        after.AddChild(Wrapped("Trouble: " + (view.TroubleIgnored > 0 ? $"{view.TroubleSorted} sorted out, {view.TroubleIgnored} nobody dealt with" : $"{view.TroubleSorted} sorted out") + ".", 290));
        columns.AddChild(after);
        box.AddChild(columns);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddChild(UiStyle.Button(closed ? "Back to the menu" : $"Plan year {view.Year + 1}", next ?? (() => { })));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });
        box.AddChild(buttons);
        return panel;
    }

    internal static Label Wrapped(string text, int width, bool muted = false, int size = 14)
    {
        var label = UiStyle.Body(text, muted, size);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    /// <summary>The small panel shown while building. In a year that sells ahead it is build week, and each day sells the last tickets.</summary>
    public static Control BuildDay(int expected, int bank, System.Action? open = null, string forecast = "", BuildWeekView? week = null,
        System.Action? nextDay = null, System.Action<int>? price = null, TravelView? travel = null, System.Action<int>? parking = null, System.Action<int>? buses = null,
        System.Action<int>? vipPrice = null)
    {
        var panel = Card(360);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Caps(week is null ? "Friday" : "Build week", pink: true));
        box.AddChild(UiStyle.Heading(week is null ? "Build day" : week.Day ?? "Ready to open", 28));
        var words = UiStyle.Body(week is null
            ? "Friday. Nobody is here yet and the clock is stopped. Put up what you need, then start the day. It starts at 8am with the gates shut: people queue outside until you open them."
            : "Nobody is here yet and the clock is stopped. Put up what you need. Ending each day sells that day's tickets.", muted: true, size: 14);
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(words);
        if (forecast.Length > 0)
        {
            var weather = new PanelContainer();
            weather.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.SunTint, 8, null, 12));
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

            if (week.Vip is { } vip)
            {
                box.AddChild(MoneyRow("VIP tickets sold", vip.Sold >= vip.OnSale ? $"{vip.Sold:N0}, sold out" : $"{vip.Sold:N0} of {vip.OnSale:N0}"));
                if (week.Day is not null && vip.Sold < vip.OnSale)
                {
                    box.AddChild(PriceRow(vip.Price, vip.Fair, vipPrice, "VIP ticket price"));
                }
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
            box.AddChild(UiStyle.SecondaryButton("Skip the rest and start the day", open ?? (() => { })));
        }
        else
        {
            box.AddChild(UiStyle.Button("Start the day", open ?? (() => { })));
        }

        return panel;
    }

    /// <summary>The small panel shown the morning after, while the field is tidied.</summary>
    public static Control Tidy(int litterLeft, int litterAtStart, int scoutsFee, System.Action? pay = null, System.Action? done = null, int picked = 0, int binned = 0)
    {
        var panel = Card(360);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Caps("Sunday", pink: true));
        box.AddChild(UiStyle.Heading("The morning after", 28));
        var words = UiStyle.Body("The farmer wants his field back as he lent it. Click litter to pick it up, or pay for help.", muted: true, size: 14);
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(words);
        if (picked > 0)
        {
            box.AddChild(MoneyRow("Picked up by cleaners", $"{picked:N0}"));
        }

        if (binned > 0)
        {
            box.AddChild(MoneyRow("Put in the bins", $"{binned:N0}"));
        }

        box.AddChild(MoneyRow("Litter left", $"{litterLeft} of {litterAtStart}"));
        var bar = new ProgressBar { Value = 100.0 * (litterAtStart - litterLeft) / System.Math.Max(1, litterAtStart), ShowPercentage = false, CustomMinimumSize = new Vector2(0, 14) };
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(UiStyle.FirstAidGreen, 7));
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
