using System.Collections.Generic;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>One act on offer as headliner: its name, what it is, its fee and how many of five stars of pull it has.</summary>
public sealed record ActOffer(string Name, string Kind, int Fee, int Stars);

/// <summary>One choice of ticket price and how many people it is expected to bring.</summary>
public sealed record TicketChoice(int Price, int Expected);

/// <summary>What the planning screen shows.</summary>
public sealed record PlanView(int Year, string Festival, string Date, int Licence, int Bank, IReadOnlyList<TicketChoice> Tickets, int TicketPicked,
    IReadOnlyList<ActOffer> Headliners, int HeadlinerPicked, IReadOnlyList<string> Support, int SupportFee);

/// <summary>What the results screen shows.</summary>
public sealed record ResultsView(int Year, string Festival, int Came, int Expected, int Happy, int WentHome, IReadOnlyList<(string What, int Amount)> In,
    IReadOnlyList<(string What, int Amount)> Out, int LitterCleared, int GrassWorn, string Verdict, int LicenceNow, int LicenceNext);

/// <summary>
/// The screens of the festival year that sit over the field: planning before, results after.
/// They only show what they are given; the rules decide what that is.
/// </summary>
public static class YearPanels
{
    private static PanelContainer Card(int width)
    {
        var panel = new PanelContainer { Theme = UiStyle.Theme, CustomMinimumSize = new Vector2(width, 0) };
        panel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 28, radius: 20));
        return panel;
    }

    private static Label Section(string text)
    {
        var label = UiStyle.Body(text.ToUpperInvariant(), muted: true, size: 13);
        return label;
    }

    private static PanelContainer Tile(bool picked, int width, System.Action? pressed = null)
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

    private static HBoxContainer MoneyRow(string what, string amount, bool strong = false, bool muted = false)
    {
        var row = new HBoxContainer();
        var left = strong ? UiStyle.Heading(what, 18) : UiStyle.Body(what, muted, 15);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        row.AddChild(strong ? UiStyle.Heading(amount, 18) : UiStyle.Body(amount, muted, 15));
        return row;
    }

    /// <summary>The planning screen: ticket price, headline act, and what is left to build with.</summary>
    public static Control Plan(PlanView view, System.Action? start = null, System.Action<int>? pickTicket = null, System.Action<int>? pickHeadliner = null)
    {
        var panel = Card(820);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        box.AddChild(UiStyle.Heading($"Plan year {view.Year}", 32));
        box.AddChild(UiStyle.Body($"{view.Festival}  ·  {view.Date}  ·  Licence for up to {view.Licence:N0} people  ·  £{view.Bank:N0} in the bank", muted: true, size: 15));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        box.AddChild(Section("Ticket price"));
        var tickets = new HBoxContainer();
        tickets.AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < view.Tickets.Count; i++)
        {
            var index = i;
            var tile = Tile(i == view.TicketPicked, 150, pickTicket is null ? null : () => pickTicket(index));
            var inner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            inner.AddChild(UiStyle.Heading($"£{view.Tickets[i].Price}", 24));
            inner.AddChild(UiStyle.Body($"About {view.Tickets[i].Expected} come", muted: true, size: 14));
            tile.AddChild(inner);
            tickets.AddChild(tile);
        }

        box.AddChild(tickets);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        box.AddChild(Section("Headline act"));
        var acts = new HBoxContainer();
        acts.AddThemeConstantOverride("separation", 10);
        for (var i = 0; i < view.Headliners.Count; i++)
        {
            var offer = view.Headliners[i];
            var index = i;
            var tile = Tile(i == view.HeadlinerPicked, 220, pickHeadliner is null ? null : () => pickHeadliner(index));
            var inner = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            inner.AddChild(UiStyle.Heading(offer.Name, 19));
            inner.AddChild(UiStyle.Body(offer.Kind, muted: true, size: 14));
            var pull = new HBoxContainer();
            pull.AddThemeConstantOverride("separation", 4);
            for (var s = 0; s < 5; s++)
            {
                var dot = new Panel { CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
                dot.AddThemeStyleboxOverride("panel", UiStyle.Box(s < offer.Stars ? UiStyle.Sunflower : UiStyle.Border, 7));
                pull.AddChild(dot);
            }

            pull.AddChild(UiStyle.Body("  pull", muted: true, size: 13));
            inner.AddChild(pull);
            inner.AddChild(UiStyle.Body($"Fee £{offer.Fee:N0}", size: 15));
            tile.AddChild(inner);
            acts.AddChild(tile);
        }

        box.AddChild(acts);
        var support = UiStyle.Body($"Also playing: {string.Join(", ", view.Support)}. Local acts, £{view.SupportFee} each.", muted: true, size: 14);
        support.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(support);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        box.AddChild(Section("Money"));
        var ticket = view.Tickets[view.TicketPicked];
        var fees = view.Headliners[view.HeadlinerPicked].Fee + (view.Support.Count * view.SupportFee);
        box.AddChild(MoneyRow("In the bank now", $"£{view.Bank:N0}"));
        box.AddChild(MoneyRow($"Act fees, paid today ({view.Support.Count + 1} acts)", $"-£{fees:N0}"));
        box.AddChild(new HSeparator());
        box.AddChild(MoneyRow("Left to build with", $"£{view.Bank - fees:N0}", strong: true));
        box.AddChild(MoneyRow($"Tickets on the day, if about {ticket.Expected} come", $"about £{ticket.Price * ticket.Expected:N0}", muted: true));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var go = UiStyle.Button("Start building", start ?? (() => { }));
        if (fees > view.Bank)
        {
            go.Disabled = true;
            buttons.AddChild(UiStyle.Body("There is not the money for these acts.  ", muted: true, size: 14));
        }

        buttons.AddChild(go);
        box.AddChild(buttons);
        return panel;
    }

    /// <summary>The results screen: how the day went, the money, the field, and what next year's licence is.</summary>
    public static Control Results(ResultsView view, System.Action? next = null, bool closed = false)
    {
        var panel = Card(760);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        box.AddChild(UiStyle.Heading($"Year {view.Year} results", 32));
        box.AddChild(UiStyle.Body(view.Festival, muted: true, size: 15));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

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
        box.AddChild(MoneyRow("Grass worn to mud", $"{view.GrassWorn}%"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

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
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddChild(UiStyle.Button(closed ? "Back to the menu" : $"Plan year {view.Year + 1}", next ?? (() => { })));
        box.AddChild(buttons);
        return panel;
    }

    /// <summary>The small panel shown on the build day, before the gates open.</summary>
    public static Control BuildDay(int expected, int bank, System.Action? open = null)
    {
        var panel = Card(360);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading("Build day", 22));
        var words = UiStyle.Body("Friday. Nobody is here yet and the clock is stopped. Put up what you need, then open the gates.", muted: true, size: 14);
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(300, 0);
        box.AddChild(words);
        box.AddChild(MoneyRow("Expected tomorrow", $"about {expected}"));
        box.AddChild(MoneyRow("Left to build with", $"£{bank:N0}"));
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        box.AddChild(UiStyle.Button("Open the gates", open ?? (() => { })));
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
