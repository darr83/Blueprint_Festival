using System.Collections.Generic;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>One set on a stage: when it is, and who is booked for it, if anyone.</summary>
public sealed record SlotView(int Set, string Time, string? Name, string Detail, int Stars, int Fee)
{
    /// <summary>True if the set can start an hour earlier.</summary>
    public bool CanEarlier { get; init; }

    /// <summary>True if the set can start an hour later.</summary>
    public bool CanLater { get; init; }
}

/// <summary>One artist who could be booked for the set that is picked.</summary>
public sealed record ArtistView(string Name, string Detail, int Stars, int Fee, string Note, bool Here);

/// <summary>What the planning screen shows in January.</summary>
public sealed record PlanView(int Year, string Festival, string Date, int Licence, int Bank, IReadOnlyList<SlotView> Main, IReadOnlyList<SlotView>? Second,
    int Picked, string PickedName, IReadOnlyList<ArtistView> Artists, int Price, int FairPrice, int Pull, int Fees, int Acts, int ToPay,
    string Forecast = "", string Field = "", int Rent = 0,
    bool Camping = false, int CampingCost = 0, int Campers = 0, int CampingFee = 0, TravelView? Travel = null);

/// <summary>What the planning screen can do.</summary>
public sealed record PlanActions(System.Action? Book = null, System.Action<int>? PickSlot = null, System.Action<string?>? PickArtist = null,
    System.Action? ToggleSecond = null, System.Action<int>? Price = null, System.Action? Camping = null, System.Action? Fields = null,
    System.Action<int>? Parking = null, System.Action<int>? Buses = null, System.Action<int, int>? MoveSet = null);

/// <summary>One way of advertising, and whether it is switched on.</summary>
public sealed record AdvertView(string Id, string Name, int Cost, int Reach, string Reaches, bool On);

/// <summary>What the ticket sales screen shows.</summary>
public sealed record SalesView(string Festival, int Licence, int Bank, int Turn, string? Next, int Sold, int Taken, int AdSpend, int Reach, int AdCost,
    int Forecast, int Price, int FairPrice, int Pull, IReadOnlyList<AdvertView> Adverts, IReadOnlyList<(string When, int Tickets, int Price, int AdCost, int Vips, int VipPrice)> Log,
    IReadOnlyList<int> SoldAfter, TravelView? Travel = null, VipView? Vip = null);

/// <summary>The VIP tickets: how many are sold of how many, at what price, what the line-up makes them worth, and what the next turn would likely sell.</summary>
public sealed record VipView(int Sold, int OnSale, int Price, int Fair, int Forecast, int Taken);

/// <summary>What the build panel shows in build week. Day is null once every day is sold.</summary>
public sealed record BuildWeekView(string? Day, int Sold, int Licence, int Forecast, int AdCost, int Price, int Fair, TravelView? Travel = null, VipView? Vip = null);

/// <summary>Tickets sold over the year as a line that climbs from January to the gates.</summary>
public partial class SalesChart : Control
{
    /// <summary>Tickets sold after each turn so far, starting with none.</summary>
    public IReadOnlyList<int> SoldAfter { get; set; } = new[] { 0 };

    public int Licence { get; set; } = 200;

    /// <summary>How much of the newest turn is drawn yet, from 0 to 1, while the month runs.</summary>
    public float Shown { get; set; } = 1;

    /// <summary>Where along the year a turn ends: six months, then seven days sharing the last seventh.</summary>
    public static float Along(int turn) => turn <= 6 ? turn : 6 + ((turn - 6) / 7f);

    /// <summary>How many tickets the line has reached as drawn.</summary>
    public float SoldShown => SoldAfter.Count < 2 ? SoldAfter[0] : Mathf.Lerp(SoldAfter[^2], SoldAfter[^1], Shown);

    public override void _Draw()
    {
        var font = UiStyle.SansSemi;
        const float left = 58, right = 10, top = 10, bottom = 26;
        var w = Size.X - left - right;
        var h = Size.Y - top - bottom;
        Vector2 At(float along, float sold) => new(left + (w * along / 7), top + h - (h * sold / Mathf.Max(1, Licence)));
        for (var i = 0; i <= 4; i++)
        {
            var sold = Licence * i / 4f;
            if (i < 4)
            {
                DrawLine(At(0, sold), At(7, sold), UiStyle.Border, 1);
            }

            DrawString(font, At(0, sold) + new Vector2(-54, 5), $"{sold:N0}", HorizontalAlignment.Right, 48, 12, UiStyle.TextMuted);
        }

        // Every ticket there is to sell, as a dashed pink line across the top.
        for (var x = 0f; x < w; x += 12)
        {
            DrawLine(At(0, Licence) + new Vector2(x, 0), At(0, Licence) + new Vector2(Mathf.Min(x + 6, w), 0), new Color(UiStyle.StagePink, 0.55f), 2);
        }

        var names = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "July" };
        for (var i = 0; i < names.Length; i++)
        {
            DrawString(font, new Vector2(left + (w * i / 7), Size.Y - 6), names[i], HorizontalAlignment.Center, w / 7, 12, UiStyle.TextMuted);
        }

        var points = new List<Vector2>();
        for (var i = 0; i < SoldAfter.Count; i++)
        {
            var last = i == SoldAfter.Count - 1 && i > 0;
            var along = last ? Mathf.Lerp(Along(i - 1), Along(i), Shown) : Along(i);
            points.Add(At(along, last ? SoldShown : SoldAfter[i]));
        }

        if (points.Count > 1)
        {
            // Tinted under the line, one strip a turn, so the shape never folds over itself.
            for (var i = 1; i < points.Count; i++)
            {
                var floor = At(0, 0).Y;
                if (points[i].X - points[i - 1].X > 0.5f)
                {
                    DrawColoredPolygon(new[] { points[i - 1], points[i], new Vector2(points[i].X, floor), new Vector2(points[i - 1].X, floor) }, new Color(UiStyle.StagePink, 0.12f));
                }
            }

            DrawPolyline(points.ToArray(), UiStyle.StagePink, 4, true);
        }

        DrawCircle(points[^1], 9, Colors.White);
        DrawCircle(points[^1], 7, UiStyle.NightInk);
    }
}

public static partial class YearPanels
{
    internal static HBoxContainer Stars(int stars, int size = 11)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 3);
        for (var s = 0; s < 5; s++)
        {
            var dot = new Panel { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            dot.AddThemeStyleboxOverride("panel", UiStyle.Box(s < stars ? UiStyle.Sunflower : UiStyle.Border, size / 2));
            row.AddChild(dot);
        }

        return row;
    }

    /// <summary>A small round minus or plus button.</summary>
    internal static Button StepButton(bool up, System.Action? pressed)
    {
        var button = UiStyle.RoundButton(up ? "plus" : "minus", pressed ?? (() => { }), 30);
        button.Name = up ? "More" : "Less";
        var ring = UiStyle.Outlined(UiStyle.Surface, 999, 0);
        var over = UiStyle.Outlined(UiStyle.SunTint, 999, 0);
        button.AddThemeStyleboxOverride("normal", ring);
        button.AddThemeStyleboxOverride("hover", over);
        button.AddThemeStyleboxOverride("disabled", ring);
        button.Disabled = pressed is null;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return button;
    }

    /// <summary>What a price is, next to what the line-up is worth.</summary>
    internal static (string Words, Color Colour) PriceSaid(int price, int fair) =>
        price > fair * 1.25 ? ($"Dear: sells slowly. Fair is about £{fair}.", new Color("#B57D00"))
        : price < fair * 0.75 ? ($"A bargain: sells fast. Fair is about £{fair}.", UiStyle.StagePink)
        : ($"About right. Fair is about £{fair}.", UiStyle.FirstAidGreen);

    /// <summary>The ticket price with a button either side, and what the line-up is worth.</summary>
    internal static Control PriceRow(int price, int fair, System.Action<int>? change, string caption = "Ticket price")
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 2);
        column.AddChild(UiStyle.Caps(caption));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var label = UiStyle.Heading($"£{price}", 32);
        label.CustomMinimumSize = new Vector2(62, 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(StepButton(false, change is null ? null : () => change(-1)));
        row.AddChild(label);
        row.AddChild(StepButton(true, change is null ? null : () => change(1)));
        var (said, colour) = PriceSaid(price, fair);
        var words = UiStyle.Body(said, size: 13, bold: true);
        words.AddThemeColorOverride("font_color", colour);
        words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        words.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        words.CustomMinimumSize = new Vector2(130, 0);
        row.AddChild(words);
        column.AddChild(row);
        return column;
    }

    /// <summary>The ticket price printed on a ticket, with a number that matters on the stub.</summary>
    internal static Control PriceTicket(int price, int fair, System.Action<int>? change, string stubCaption, string stubNumber, string heading = "Ticket price")
    {
        var stub = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        stub.AddThemeConstantOverride("separation", -2);
        var caption = UiStyle.Caps(stubCaption);
        var number = UiStyle.Heading(stubNumber, 26);
        caption.HorizontalAlignment = number.HorizontalAlignment = HorizontalAlignment.Center;
        stub.AddChild(caption);
        stub.AddChild(number);
        return Ticket.With(PriceRow(price, fair, change, heading), stub);
    }

    /// <summary>One line of the running order: the time in pink, the act in the poster font, the music, the stars and the fee.</summary>
    private static Control SlotTile(SlotView slot, bool picked, System.Action<int>? pick, System.Action<int, int>? move = null)
    {
        var tile = Tile(picked, 400, pick is null ? null : () => pick(slot.Set));
        var line = UiStyle.Box(picked ? UiStyle.SunTint : UiStyle.Surface, picked ? 6 : 0);
        line.ContentMarginLeft = line.ContentMarginRight = 10;
        line.ContentMarginTop = 5;
        line.ContentMarginBottom = 6;
        if (!picked)
        {
            line.BorderColor = UiStyle.Border;
            line.BorderWidthBottom = 1;
        }

        tile.AddThemeStyleboxOverride("panel", line);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        var time = UiStyle.Heading(slot.Time, 18);
        time.AddThemeColorOverride("font_color", UiStyle.StagePink);
        time.CustomMinimumSize = new Vector2(56, 0);
        if (move is not null)
        {
            // An hour earlier or later; the other sets on the stage move along to make room.
            var times = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            times.AddThemeConstantOverride("separation", 2);
            times.AddChild(HourButton(false, slot.CanEarlier ? () => move(slot.Set, -1) : null));
            time.HorizontalAlignment = HorizontalAlignment.Center;
            times.AddChild(time);
            times.AddChild(HourButton(true, slot.CanLater ? () => move(slot.Set, 1) : null));
            row.AddChild(times);
        }
        else
        {
            row.AddChild(time);
        }
        var name = UiStyle.Heading(slot.Name ?? "Nobody yet", 21);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        if (slot.Name is null)
        {
            name.AddThemeColorOverride("font_color", new Color("#C5BFCA"));
        }

        row.AddChild(name);
        if (slot.Name is not null)
        {
            row.AddChild(UiStyle.Caps(slot.Detail, size: 11));
            row.AddChild(Stars(slot.Stars));
            var fee = UiStyle.Body($"£{slot.Fee:N0}", size: 14, bold: true);
            fee.CustomMinimumSize = new Vector2(54, 0);
            fee.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(fee);
        }

        tile.AddChild(row);
        return tile;
    }

    /// <summary>A small arrow that moves a set an hour earlier or later; greyed out when there is no room.</summary>
    private static Button HourButton(bool later, System.Action? pressed)
    {
        var button = UiStyle.RoundButton(later ? "›" : "‹", pressed ?? (() => { }), 24);
        button.Name = later ? "Later" : "Earlier";
        button.Disabled = pressed is null;
        button.TooltipText = pressed is null ? "No room to move this set" : later ? "Start an hour later" : "Start an hour earlier";
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return button;
    }

    /// <summary>A card to switch an extra on and off: a tick box, what it is, and a line about it.</summary>
    private static Control Extra(bool on, string what, string about, System.Action? pressed)
    {
        var tile = Tile(on, 250, pressed);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        var box = new Panel { CustomMinimumSize = new Vector2(20, 20), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeStyleboxOverride("panel", UiStyle.Outlined(on ? UiStyle.NightInk : UiStyle.Surface, 6, 0));
        row.AddChild(box);
        var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        words.AddChild(UiStyle.Body(what, size: 14, bold: true));
        var small = UiStyle.Body(about, muted: true, size: 12);
        small.ClipText = true;
        words.AddChild(small);
        row.AddChild(words);
        tile.AddChild(row);
        return tile;
    }

    /// <summary>
    /// The January planning screen, laid out as the festival's programme: the running order down the
    /// left with the ticket under it, and on the right the artists to choose from and what is paid today.
    /// </summary>
    public static Control Plan(PlanView view, PlanActions? does = null, float scroll = 0)
    {
        does ??= new PlanActions();
        var panel = Card(1220);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        var day = new VBoxContainer();
        day.AddThemeConstantOverride("separation", 0);
        var date = UiStyle.Heading(view.Date, 22);
        date.HorizontalAlignment = HorizontalAlignment.Right;
        day.AddChild(date);
        var where = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        where.AddThemeConstantOverride("separation", 10);
        where.AddChild(UiStyle.Body($"{(view.Field.Length > 0 ? view.Field : view.Festival)}  ·  up to {view.Licence:N0} people  ·  £{view.Bank:N0} in the bank", muted: true, size: 14));
        if (does.Fields is not null)
        {
            var fields = UiStyle.ChipButton("Change fields", does.Fields);
            fields.Name = "Change fields";
            where.AddChild(fields);
        }

        day.AddChild(where);
        Masthead(box, $"January  ·  {view.Festival}  ·  Year {view.Year}", "The running order", day);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 40);
        box.AddChild(columns);

        var bill = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        bill.AddThemeConstantOverride("separation", 2);
        columns.AddChild(bill);
        bill.AddChild(Section("Main stage"));
        foreach (var slot in view.Main)
        {
            bill.AddChild(SlotTile(slot, slot.Set == view.Picked, does.PickSlot, does.MoveSet));
        }

        bill.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var second = new HBoxContainer();
        var heading = Section("Second stage");
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        second.AddChild(heading);
        var toggle = UiStyle.ChipButton(view.Second is null ? "+  Add a second stage" : "Remove", does.ToggleSecond ?? (() => { }));
        second.AddChild(toggle);
        bill.AddChild(second);
        if (view.Second is null)
        {
            bill.AddChild(Wrapped("Four more sets, on a stage you put up in build week. It splits the crowd between two fronts.", 500, muted: true, size: 13));
        }
        else
        {
            foreach (var slot in view.Second)
            {
                bill.AddChild(SlotTile(slot, slot.Set == view.Picked, does.PickSlot, does.MoveSet));
            }
        }

        bill.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6), SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        bill.AddChild(PriceTicket(view.Price, view.FairPrice, does.Price, "Admit", $"{view.Licence:N0}"));
        var summary = UiStyle.Body($"{view.Acts} acts  ·  fees £{view.Fees:N0}  ·  line-up pull {view.Pull}%", muted: true, size: 13);
        summary.TooltipText = "Sets are an hour and a half and can start on any hour from 12:00 to 21:00 (the arrows by each time), with the next set on that stage on the next hour after it ends at the earliest. Any can be left empty, and the two stages can start and finish at different times. Every act helps sell tickets by its stars and fans, the last act on the main stage most of all. Four kinds of music or more bring a wider crowd.";
        summary.MouseFilter = Control.MouseFilterEnum.Stop;
        bill.AddChild(summary);

        var roster = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        roster.AddThemeConstantOverride("separation", 8);
        columns.AddChild(roster);
        var asking = new HBoxContainer();
        var who = Section($"Who plays {view.PickedName}?");
        who.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        asking.AddChild(who);
        var empty = UiStyle.ChipButton("Leave it empty", does.PickArtist is null ? () => { } : () => does.PickArtist(null));
        empty.TooltipText = "Nobody plays this set and nothing is paid.";
        asking.AddChild(empty);
        roster.AddChild(asking);
        var extras = view.Forecast.Length > 0 || view.CampingCost > 0;
        var room = 438 - (view.Travel is null ? 0 : 150) - (extras ? 62 : 0);
        var scroller = new ScrollContainer { CustomMinimumSize = new Vector2(590, room), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Name = "Artists" };
        roster.AddChild(scroller);
        var list = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("h_separation", 8);
        list.AddThemeConstantOverride("v_separation", 8);
        scroller.AddChild(list);
        foreach (var artist in view.Artists)
        {
            var name = artist.Name;
            var tile = Tile(artist.Here, 280, does.PickArtist is null ? null : () => does.PickArtist(name));
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 8);
            var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", 1);
            var called = UiStyle.Body(artist.Name, size: 15, bold: true);
            called.ClipText = true;
            words.AddChild(called);
            var booked = artist.Note.Length > 0;
            var detail = UiStyle.Body(booked ? artist.Note : artist.Detail, muted: !booked, size: 12, bold: booked);
            detail.ClipText = true;
            detail.TooltipText = artist.Detail;
            if (booked)
            {
                detail.AddThemeColorOverride("font_color", UiStyle.StagePink);
            }

            words.AddChild(detail);
            var under = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            under.AddChild(Stars(artist.Stars));
            words.AddChild(under);
            row.AddChild(words);
            var fee = UiStyle.Heading($"£{artist.Fee:N0}", 19);
            fee.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(fee);
            tile.AddChild(row);
            list.AddChild(tile);
        }

        if (scroll > 0)
        {
            scroller.Ready += () => scroller.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, (int)scroll);
        }

        if (view.Travel is not null)
        {
            roster.AddChild(Travel(view.Travel, does.Parking, does.Buses));
        }

        if (extras)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            roster.AddChild(row);
            if (view.Forecast.Length > 0)
            {
                var forecast = UiStyle.Body($"{view.Forecast}. A guess this far ahead. If it rains, a poncho stall will do good business.", muted: true, size: 13);
                forecast.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
                row.AddChild(forecast);
            }

            if (view.CampingCost > 0)
            {
                row.AddChild(Extra(view.Camping, view.Camping ? $"Camping field taken: £{view.CampingCost:N0}" : $"Take the camping field: £{view.CampingCost:N0}", $"About a third bring a tent, £{view.CampingFee} more each.", does.Camping));
            }
        }

        roster.AddChild(UiStyle.Rule(1));
        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 14);
        var sums = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        sums.AddThemeConstantOverride("separation", -2);
        var rent = view.Rent > 0 ? $"  ·  field rent £{view.Rent:N0}" : "";
        var short_ = view.ToPay > view.Bank;
        sums.AddChild(UiStyle.Body(short_ ? "There is not the money for all this."
            : $"To pay today: acts £{view.Fees:N0}{rent}{(view.Camping ? "  ·  camping field" : "")}", muted: true, size: 13));
        sums.AddChild(UiStyle.Heading($"£{view.Bank - view.ToPay:N0} left", 26));
        foot.AddChild(sums);
        var go = UiStyle.Button("Print the tickets", does.Book ?? (() => { }));
        go.TooltipText = "Book the acts and put tickets on sale";
        go.Disabled = short_;
        go.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        go.CustomMinimumSize = new Vector2(0, 50);
        foot.AddChild(go);
        roster.AddChild(foot);
        return panel;
    }

    /// <summary>
    /// The ticket sales screen from January to June: the month as the heading with the year in dots,
    /// the sales line, the adverts to switch on and off, the price on a ticket, and a button that runs the month.
    /// </summary>
    public static Control Sales(SalesView view, System.Action? run = null, System.Action<string>? toggleAdvert = null, System.Action<int>? price = null, bool busy = false,
        System.Action<int>? parking = null, System.Action<int>? buses = null, System.Action<int>? vipPrice = null)
    {
        var panel = Card(1220);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);
        var turn = view.Turn;

        var year = new HBoxContainer();
        year.AddThemeConstantOverride("separation", 6);
        var months = new[] { "January", "February", "March", "April", "May", "June", "July: build week" };
        for (var i = 0; i < months.Length; i++)
        {
            var now = i == Mathf.Min(turn, 6);
            var done = i < turn && !now;
            var dot = new PanelContainer { CustomMinimumSize = new Vector2(38, 38), TooltipText = months[i] + (i == 0 ? ": plan and book" : i == 6 ? "" : ": sell") };
            dot.AddThemeStyleboxOverride("panel", now ? UiStyle.Box(UiStyle.StagePink, 999) : done ? UiStyle.Box(UiStyle.NightInk, 999) : UiStyle.Outlined(UiStyle.Surface, 999, 0, new Color("#D9D3DC")));
            var letter = UiStyle.Body(months[i][..1], size: 15);
            letter.AddThemeFontOverride("font", UiStyle.SansBold);
            letter.AddThemeColorOverride("font_color", now || done ? Colors.White : new Color("#A8A1AD"));
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            dot.AddChild(letter);
            year.AddChild(dot);
        }

        Masthead(box, $"{view.Festival}  ·  Tickets on sale", view.Next ?? "Sold: on to build week", year, 44);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 40);
        box.AddChild(columns);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        left.AddThemeConstantOverride("separation", 10);
        columns.AddChild(left);
        var numbers = new HBoxContainer();
        numbers.AddThemeConstantOverride("separation", 34);
        numbers.AddChild(BigNumber($"{view.Sold:N0}", view.Sold >= view.Licence ? "sold. Sold out" : $"sold of {view.Licence:N0}", UiStyle.StagePink, 54, "Sold"));
        numbers.AddChild(BigNumber($"£{view.Taken:N0}", "taken so far", null, 54, "Taken"));
        left.AddChild(numbers);
        left.AddChild(new SalesChart { Name = "Chart", SoldAfter = view.SoldAfter, Licence = view.Licence, CustomMinimumSize = new Vector2(520, 250), Theme = UiStyle.Theme });
        var spent = UiStyle.Body($"£{view.AdSpend:N0} spent on adverts so far  ·  £{view.Bank:N0} in the bank", muted: true, size: 14);
        spent.Name = "Bank";
        left.AddChild(spent);
        if (view.Log.Count == 0)
        {
            left.AddChild(UiStyle.Body("Nothing sold yet. Tickets go on sale when you run January.", muted: true, size: 14));
        }

        for (var i = view.Log.Count - 1; i >= Mathf.Max(0, view.Log.Count - 3); i--)
        {
            var line = view.Log[i];
            var vips = line.Vips > 0 ? $"  ·  {line.Vips:N0} VIP at £{line.VipPrice}" : "";
            left.AddChild(MoneyRow(line.When, $"{line.Tickets:N0} tickets at £{line.Price}{vips}  ·  £{line.AdCost:N0} on adverts", muted: true));
        }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 12);
        columns.AddChild(right);
        var title = Section($"Put the word out  ·  reaches {view.Reach:N0}  ·  paid each month it is on");
        right.AddChild(title);
        var chips = new HFlowContainer { CustomMinimumSize = new Vector2(590, 0) };
        chips.AddThemeConstantOverride("h_separation", 8);
        chips.AddThemeConstantOverride("v_separation", 8);
        right.AddChild(chips);
        foreach (var advert in view.Adverts)
        {
            var id = advert.Id;
            var chip = UiStyle.ChipButton($"{advert.Name}   £{advert.Cost:N0}", toggleAdvert is null || busy ? () => { } : () => toggleAdvert(id), Sounds.Tab);
            chip.Name = $"Advert {id}";
            chip.ToggleMode = true;
            chip.ButtonPressed = advert.On;
            chip.Disabled = busy;
            if (busy)
            {
                var kept = chip.GetThemeStylebox(advert.On ? "pressed" : "normal");
                chip.AddThemeStyleboxOverride("disabled", kept);
                chip.AddThemeColorOverride("font_disabled_color", advert.On ? Colors.White : UiStyle.TextDark);
            }

            chip.TooltipText = $"{advert.Reaches}. Reaches {advert.Reach:N0} people for £{advert.Cost:N0} a month.";
            chips.AddChild(chip);
        }

        right.AddChild(PriceTicket(view.Price, view.FairPrice, busy ? null : price, "Likely", view.Sold >= view.Licence ? "0" : $"~{view.Forecast:N0}"));
        if (view.Vip is { } vip)
        {
            var ticket = PriceTicket(vip.Price, vip.Fair, busy ? null : vipPrice, $"VIP {vip.Sold:N0} of {vip.OnSale:N0}", vip.Sold >= vip.OnSale ? "Sold out" : $"~{vip.Forecast:N0}", "VIP ticket price  ·  gold lanyard");
            ticket.Name = "VipTicket";
            right.AddChild(ticket);
        }
        if (view.Travel is not null)
        {
            right.AddChild(Travel(view.Travel, busy ? null : parking, busy ? null : buses));
        }

        right.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        var short_ = view.AdCost > view.Bank;
        var said = UiStyle.Body(short_
            ? "There is not the money for these adverts. Switch some off."
            : view.Sold >= view.Licence ? "Every ticket is sold. The adverts have stopped."
            : $"Adverts this month £{view.AdCost:N0}  ·  line-up pull {view.Pull}%", muted: true, size: 14);
        said.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttons.AddChild(said);
        var go = UiStyle.Button(view.Next is null ? "On to build week" : busy ? $"{view.Next} is running" : $"Run {view.Next}", run ?? (() => { }), Sounds.Coins);
        go.Disabled = busy || short_;
        go.Name = "Run";
        go.CustomMinimumSize = new Vector2(0, 50);
        buttons.AddChild(go);
        right.AddChild(buttons);
        return panel;
    }
}
