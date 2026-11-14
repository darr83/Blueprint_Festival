using System.Collections.Generic;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>One set on a stage: when it is, and who is booked for it, if anyone.</summary>
public sealed record SlotView(int Set, string Time, string? Name, string Detail, int Stars, int Fee);

/// <summary>One artist who could be booked for the set that is picked.</summary>
public sealed record ArtistView(string Name, string Detail, int Stars, int Fee, string Note, bool Here);

/// <summary>What the planning screen shows in January.</summary>
public sealed record PlanView(int Year, string Festival, string Date, int Licence, int Bank, IReadOnlyList<SlotView> Main, IReadOnlyList<SlotView>? Second,
    int Picked, string PickedName, IReadOnlyList<ArtistView> Artists, int Price, int FairPrice, int Pull, int Fees, int Acts, int ToPay,
    string Forecast = "", bool Ponchos = false, int PonchoCost = 0, string Field = "", int Rent = 0,
    bool Camping = false, int CampingCost = 0, int Campers = 0, int CampingFee = 0, TravelView? Travel = null);

/// <summary>What the planning screen can do.</summary>
public sealed record PlanActions(System.Action? Book = null, System.Action<int>? PickSlot = null, System.Action<string?>? PickArtist = null,
    System.Action? ToggleSecond = null, System.Action<int>? Price = null, System.Action? Ponchos = null, System.Action? Camping = null, System.Action? Fields = null,
    System.Action<int>? Parking = null, System.Action<int>? Buses = null);

/// <summary>One way of advertising, and whether it is switched on.</summary>
public sealed record AdvertView(string Id, string Name, int Cost, int Reach, string Reaches, bool On);

/// <summary>What the ticket sales screen shows.</summary>
public sealed record SalesView(string Festival, int Licence, int Bank, int Turn, string? Next, int Sold, int Taken, int AdSpend, int Reach, int AdCost,
    int Forecast, int Price, int FairPrice, int Pull, IReadOnlyList<AdvertView> Adverts, IReadOnlyList<(string When, int Tickets, int Price, int AdCost)> Log,
    IReadOnlyList<int> SoldAfter, TravelView? Travel = null);

/// <summary>What the build panel shows in build week. Day is null once every day is sold.</summary>
public sealed record BuildWeekView(string? Day, int Sold, int Licence, int Forecast, int AdCost, int Price, int Fair, TravelView? Travel = null);

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
        var font = GetThemeDefaultFont();
        const float left = 58, right = 10, top = 10, bottom = 26;
        var w = Size.X - left - right;
        var h = Size.Y - top - bottom;
        Vector2 At(float along, float sold) => new(left + (w * along / 7), top + h - (h * sold / Mathf.Max(1, Licence)));
        for (var i = 0; i <= 4; i++)
        {
            var sold = Licence * i / 4f;
            DrawLine(At(0, sold), At(7, sold), UiStyle.Border, 1);
            DrawString(font, At(0, sold) + new Vector2(-54, 5), $"{sold:N0}", HorizontalAlignment.Right, 48, 13, UiStyle.TextMuted);
        }

        var names = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "July" };
        for (var i = 0; i < names.Length; i++)
        {
            DrawString(font, new Vector2(left + (w * i / 7), Size.Y - 6), names[i], HorizontalAlignment.Center, w / 7, 13, UiStyle.TextMuted);
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
            DrawPolyline(points.ToArray(), UiStyle.StagePink, 4, true);
        }

        DrawCircle(points[^1], 7, UiStyle.Sunflower);
    }
}

public static partial class YearPanels
{
    internal static HBoxContainer Stars(int stars, int size = 12)
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

    /// <summary>The ticket price with a button either side, and what the line-up is worth.</summary>
    internal static Control PriceRow(int price, int fair, System.Action<int>? change)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var down = UiStyle.SecondaryButton("-", () => change?.Invoke(-1));
        var up = UiStyle.SecondaryButton("+", () => change?.Invoke(1));
        down.CustomMinimumSize = up.CustomMinimumSize = new Vector2(40, 0);
        var label = UiStyle.Heading($"£{price}", 24);
        label.CustomMinimumSize = new Vector2(56, 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(UiStyle.Body("Ticket", size: 15));
        row.AddChild(down);
        row.AddChild(label);
        row.AddChild(up);
        var said = price > fair * 1.25 ? "Dear. Sells slowly." : price < fair * 0.75 ? "A bargain. Sells fast." : "About right.";
        var words = UiStyle.Body($"{said} Fair is about £{fair}.", muted: true, size: 13);
        words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(words);
        return row;
    }

    private static Control SlotTile(SlotView slot, bool picked, System.Action<int>? pick)
    {
        var tile = Tile(picked, 400, pick is null ? null : () => pick(slot.Set));
        tile.AddThemeStyleboxOverride("panel", UiStyle.Box(picked ? UiStyle.PinkTint : UiStyle.Ground, 10, picked ? UiStyle.StagePink : UiStyle.Border, 9));
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(UiStyle.Body(slot.Time, muted: true, size: 14));
        var name = UiStyle.Body(slot.Name ?? "Empty", muted: slot.Name is null, size: 15);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        name.ClipText = true;
        row.AddChild(name);
        if (slot.Name is not null)
        {
            row.AddChild(UiStyle.Body(slot.Detail, muted: true, size: 13));
            row.AddChild(Stars(slot.Stars));
            var fee = UiStyle.Body($"£{slot.Fee:N0}", size: 14);
            fee.CustomMinimumSize = new Vector2(58, 0);
            fee.HorizontalAlignment = HorizontalAlignment.Right;
            row.AddChild(fee);
        }

        tile.AddChild(row);
        return tile;
    }

    /// <summary>The January planning screen: the stages and who plays each set, the price, and what is paid today.</summary>
    public static Control Plan(PlanView view, PlanActions? does = null, float scroll = 0)
    {
        does ??= new PlanActions();
        var panel = Card(1060);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading($"January: plan year {view.Year}", 26));
        var where = view.Field.Length > 0 ? $" on {view.Field}" : "";
        box.AddChild(UiStyle.Body($"{view.Festival}  ·  {view.Date}  ·  Licence for up to {view.Licence:N0} people{where}  ·  £{view.Bank:N0} in the bank", muted: true, size: 15));

        if (does.Fields is not null)
        {
            var fields = UiStyle.SecondaryButton("Change fields", does.Fields);
            fields.Name = "Change fields";
            fields.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            fields.AddThemeFontSizeOverride("font_size", 13);
            box.AddChild(fields);
        }

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 18);
        box.AddChild(columns);

        var bill = new VBoxContainer { CustomMinimumSize = new Vector2(470, 0) };
        bill.AddThemeConstantOverride("separation", 5);
        columns.AddChild(bill);
        bill.AddChild(Section("Main stage"));
        foreach (var slot in view.Main)
        {
            bill.AddChild(SlotTile(slot, slot.Set == view.Picked, does.PickSlot));
        }

        var second = new HBoxContainer();
        var heading = Section("Second stage");
        heading.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        second.AddChild(heading);
        var toggle = UiStyle.SecondaryButton(view.Second is null ? "Add a second stage" : "Remove", does.ToggleSecond ?? (() => { }));
        toggle.AddThemeFontSizeOverride("font_size", 13);
        second.AddChild(toggle);
        bill.AddChild(second);
        if (view.Second is null)
        {
            var none = UiStyle.Body("Four more sets on a second stage, which you put up in build week. It splits the crowd between two fronts.", muted: true, size: 13);
            none.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            none.CustomMinimumSize = new Vector2(440, 0);
            bill.AddChild(none);
        }
        else
        {
            foreach (var slot in view.Second)
            {
                bill.AddChild(SlotTile(slot, slot.Set == view.Picked, does.PickSlot));
            }
        }

        var summary = UiStyle.Body($"{view.Acts} acts  ·  fees £{view.Fees:N0}  ·  line-up pull {view.Pull}%  ·  worth about £{view.FairPrice} a ticket", size: 14);
        bill.AddChild(summary);
        var how = UiStyle.Body("Sets are two hours, from 14:00 to 22:00, and any can be left empty. Every act helps sell tickets by its stars and fans, the last act on the main stage most of all. Four kinds of music or more bring a wider crowd.", muted: true, size: 13);
        how.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        how.CustomMinimumSize = new Vector2(440, 0);
        bill.AddChild(how);

        var roster = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        roster.AddThemeConstantOverride("separation", 5);
        columns.AddChild(roster);
        roster.AddChild(Section($"Artists for {view.PickedName}"));
        var scroller = new ScrollContainer { CustomMinimumSize = new Vector2(500, view.Travel is null ? 420 : 262), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Name = "Artists" };
        roster.AddChild(scroller);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        scroller.AddChild(list);
        var empty = Tile(false, 470, does.PickArtist is null ? null : () => does.PickArtist(null));
        empty.AddThemeStyleboxOverride("panel", UiStyle.Box(UiStyle.Ground, 10, UiStyle.Border, 8));
        empty.AddChild(UiStyle.Body("Leave this set empty. Nobody plays and nothing is paid.", muted: true, size: 14));
        list.AddChild(empty);
        foreach (var artist in view.Artists)
        {
            var name = artist.Name;
            var tile = Tile(artist.Here, 470, does.PickArtist is null ? null : () => does.PickArtist(name));
            tile.AddThemeStyleboxOverride("panel", UiStyle.Box(artist.Here ? UiStyle.PinkTint : UiStyle.Surface, 10, artist.Here ? UiStyle.StagePink : UiStyle.Border, 8));
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 10);
            var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", 0);
            words.AddChild(UiStyle.Body(artist.Name, size: 15));
            words.AddChild(UiStyle.Body(artist.Note.Length > 0 ? $"{artist.Detail}  ·  {artist.Note}" : artist.Detail, muted: true, size: 12));
            row.AddChild(words);
            row.AddChild(Stars(artist.Stars));
            var fee = UiStyle.Body($"£{artist.Fee:N0}", size: 14);
            fee.CustomMinimumSize = new Vector2(62, 0);
            fee.HorizontalAlignment = HorizontalAlignment.Right;
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

        var extras = new HBoxContainer();
        extras.AddThemeConstantOverride("separation", 10);
        box.AddChild(extras);
        var price = Tile(false, 330);
        price.AddChild(PriceRow(view.Price, view.FairPrice, does.Price));
        extras.AddChild(price);
        if (view.Forecast.Length > 0)
        {
            var order = Tile(view.Ponchos, 330, does.Ponchos);
            var offer = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            offer.AddThemeConstantOverride("separation", 0);
            offer.AddChild(UiStyle.Body(view.Ponchos ? $"Rain ponchos ordered: £{view.PonchoCost:N0}" : $"Order rain ponchos: £{view.PonchoCost:N0}", size: 15));
            offer.AddChild(UiStyle.Body($"{view.Forecast}. A guess this far ahead.", muted: true, size: 12));
            order.AddChild(offer);
            extras.AddChild(order);
        }

        if (view.CampingCost > 0)
        {
            var camp = Tile(view.Camping, 330, does.Camping);
            var offer = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            offer.AddThemeConstantOverride("separation", 0);
            offer.AddChild(UiStyle.Body(view.Camping ? $"Camping field taken: £{view.CampingCost:N0}" : $"Take the camping field: £{view.CampingCost:N0}", size: 15));
            offer.AddChild(UiStyle.Body($"About a third bring a tent, £{view.CampingFee} more each.", muted: true, size: 12));
            camp.AddChild(offer);
            extras.AddChild(camp);
        }

        var rent = view.Rent > 0 ? $", field rent £{view.Rent:N0}" : "";
        box.AddChild(MoneyRow($"To pay today: acts £{view.Fees:N0}{rent}{(view.Ponchos ? ", ponchos" : "")}{(view.Camping ? ", camping field" : "")}", $"-£{view.ToPay:N0}"));
        box.AddChild(MoneyRow("Left for adverts and building", $"£{view.Bank - view.ToPay:N0}", strong: true));

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        var go = UiStyle.Button("Book the acts and put tickets on sale", does.Book ?? (() => { }));
        if (view.ToPay > view.Bank)
        {
            go.Disabled = true;
            buttons.AddChild(UiStyle.Body("There is not the money for all this.  ", muted: true, size: 14));
        }

        buttons.AddChild(go);
        box.AddChild(buttons);
        return panel;
    }

    /// <summary>
    /// The ticket sales screen from January to June: the year across the top, the sales line, the
    /// adverts to switch on and off, the price, and a button that runs the month.
    /// </summary>
    public static Control Sales(SalesView view, System.Action? run = null, System.Action<string>? toggleAdvert = null, System.Action<int>? price = null, bool busy = false,
        System.Action<int>? parking = null, System.Action<int>? buses = null)
    {
        var panel = Card(1060);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        box.AddChild(UiStyle.Heading(view.Next is null ? "Tickets" : $"Tickets on sale: {view.Next}", 26));
        var turn = view.Turn;

        var year = new HBoxContainer();
        year.AddThemeConstantOverride("separation", 6);
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul" };
        for (var i = 0; i < months.Length; i++)
        {
            var now = i == Mathf.Min(turn, 6);
            var tile = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var style = UiStyle.Box(now ? UiStyle.StagePink : UiStyle.Surface, 9, now ? UiStyle.StagePink : UiStyle.Border, 6);
            if (i < turn && !now)
            {
                style.BorderColor = UiStyle.Sunflower;
                style.BorderWidthBottom = 3;
            }

            tile.AddThemeStyleboxOverride("panel", style);
            var words = new VBoxContainer();
            words.AddThemeConstantOverride("separation", 0);
            var month = UiStyle.Body(months[i], size: 14);
            var what = UiStyle.Body(i == 0 ? "Plan and book" : i == 6 ? "Build week" : "Sell", muted: !now, size: 11);
            month.HorizontalAlignment = what.HorizontalAlignment = HorizontalAlignment.Center;
            if (now)
            {
                month.AddThemeColorOverride("font_color", Colors.White);
                what.AddThemeColorOverride("font_color", Colors.White);
            }

            words.AddChild(month);
            words.AddChild(what);
            tile.AddChild(words);
            year.AddChild(tile);
        }

        box.AddChild(year);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 18);
        box.AddChild(columns);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(500, 0) };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);
        var numbers = new HBoxContainer();
        numbers.AddThemeConstantOverride("separation", 8);
        foreach (var (big, small, name) in new[]
        {
            ($"{view.Sold:N0}", view.Sold >= view.Licence ? "sold. Sold out" : $"sold of {view.Licence:N0}", "Sold"),
            ($"£{view.Taken:N0}", "ticket money", "Taken"),
            ($"£{view.AdSpend:N0}", "spent on adverts", "Spent"),
            ($"£{view.Bank:N0}", "in the bank", "Bank"),
        })
        {
            var cell = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            cell.AddThemeConstantOverride("separation", 0);
            var number = UiStyle.Heading(big, 22);
            number.Name = name;
            cell.AddChild(number);
            cell.AddChild(UiStyle.Body(small, muted: true, size: 12));
            numbers.AddChild(cell);
        }

        left.AddChild(numbers);
        left.AddChild(new SalesChart { Name = "Chart", SoldAfter = view.SoldAfter, Licence = view.Licence, CustomMinimumSize = new Vector2(500, 230), Theme = UiStyle.Theme });
        left.AddChild(Section("The year so far"));
        if (view.Log.Count == 0)
        {
            left.AddChild(UiStyle.Body("Nothing sold yet. Tickets go on sale when you run January.", muted: true, size: 14));
        }

        for (var i = view.Log.Count - 1; i >= Mathf.Max(0, view.Log.Count - 6); i--)
        {
            var line = view.Log[i];
            left.AddChild(MoneyRow(line.When, $"{line.Tickets:N0} tickets at £{line.Price}  ·  £{line.Tickets * line.Price:N0} in  ·  £{line.AdCost:N0} on adverts", muted: true));
        }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        columns.AddChild(right);
        var top = new HBoxContainer();
        var title = Section("Advertising, paid each month it is on");
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(title);
        top.AddChild(UiStyle.Body($"Reaches {view.Reach:N0}", muted: true, size: 13));
        right.AddChild(top);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 5);
        right.AddChild(grid);
        foreach (var advert in view.Adverts)
        {
            var id = advert.Id;
            var tile = Tile(advert.On, 240, toggleAdvert is null || busy ? null : () => toggleAdvert(id));
            tile.AddThemeStyleboxOverride("panel", UiStyle.Box(advert.On ? UiStyle.PinkTint : UiStyle.Surface, 10, advert.On ? UiStyle.StagePink : UiStyle.Border, 7));
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", 0);
            words.AddChild(UiStyle.Body(advert.Name, size: 14));
            words.AddChild(UiStyle.Body($"{advert.Reaches}  ·  reaches {advert.Reach:N0}", muted: true, size: 11));
            row.AddChild(words);
            row.AddChild(UiStyle.Body($"£{advert.Cost:N0}", size: 14));
            tile.AddChild(row);
            grid.AddChild(tile);
        }

        var pricing = Tile(false, 480);
        pricing.AddChild(PriceRow(view.Price, view.FairPrice, busy ? null : price));
        right.AddChild(pricing);
        if (view.Travel is not null)
        {
            right.AddChild(Travel(view.Travel, busy ? null : parking, busy ? null : buses));
        }

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        var short_ = view.AdCost > view.Bank;
        var said = UiStyle.Body(short_
            ? "There is not the money for these adverts. Switch some off."
            : view.Sold >= view.Licence ? "Every ticket is sold. The adverts have stopped."
            : $"Adverts this month £{view.AdCost:N0}. Likely to sell about {view.Forecast:N0}. Line-up pull {view.Pull}%.", muted: true, size: 14);
        said.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        buttons.AddChild(said);
        var go = UiStyle.Button(view.Next is null ? "On to build week" : busy ? $"{view.Next} is running" : $"Run {view.Next}", run ?? (() => { }), Sounds.Coins);
        go.Disabled = busy || short_;
        go.Name = "Run";
        buttons.AddChild(go);
        box.AddChild(buttons);
        return panel;
    }
}
