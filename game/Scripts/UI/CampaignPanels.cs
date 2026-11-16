using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.Sound;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// One job that could be done this month. Id says what it is: "announce:" and an act, "release",
/// "stunt:" and a stunt, "advert:" and an advert, or "book".
/// </summary>
public sealed record JobView(string Id, string Group, string Name, string About, string Button, bool Can)
{
    /// <summary>True for something already done or switched on, which is shown picked.</summary>
    public bool Done { get; init; }
}

/// <summary>One ticket release: how many it holds, how many are sold, and where it stands.</summary>
public sealed record ReleaseView(string Name, int Size, int Sold, string State);

/// <summary>One answer to this month's news.</summary>
public sealed record NewsChoiceView(string Label, string Outcome, bool Can);

/// <summary>This month's news, if it still wants an answer.</summary>
public sealed record NewsView(string Headline, string About, IReadOnlyList<NewsChoiceView> Choices);

/// <summary>What the campaign screen shows on top of the ticket sales: jobs, buzz, releases and news.</summary>
public sealed record CampaignView(SalesView Sales, int JobsLeft, int JobsAMonth, int Buzz, string BuzzWords, int BuzzFade, IReadOnlyList<JobView> Jobs,
    IReadOnlyList<ReleaseView> Releases, NewsView? News, IReadOnlyList<(int Turn, string When, string What)> Notes, int OnSaleNow);

/// <summary>What the campaign screen can do.</summary>
public sealed record CampaignActions(Action? Run = null, Action<string>? Job = null, Action<int>? News = null, Action<int>? Price = null, Action<int>? VipPrice = null,
    Action<int>? Parking = null, Action<int>? Buses = null, Action<int>? Camping = null);

public static partial class YearPanels
{
    /// <summary>The name of the scrolling list of jobs, so the screen can keep it where the player had it.</summary>
    public const string JobsScroll = "Jobs";

    private static PanelContainer Boxed(Color? background = null, Color? border = null)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiStyle.Outlined(background ?? UiStyle.Surface, 10, 12, border ?? UiStyle.Border));
        return panel;
    }

    private static Control JobTile(JobView job, Action<string>? pressed, bool busy)
    {
        var tile = Tile(job.Done, 380);
        tile.Name = $"Job {job.Id}";
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        var name = UiStyle.Body(job.Name, size: 15, bold: true);
        name.ClipText = true;
        words.AddChild(name);
        var about = UiStyle.Body(job.About, muted: true, size: 12);
        about.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        about.CustomMinimumSize = new Vector2(230, 0);
        words.AddChild(about);
        row.AddChild(words);
        var id = job.Id;
        var button = UiStyle.ChipButton(job.Button, pressed is null || busy ? () => { } : () => pressed(id));
        button.Name = "Do";
        button.Disabled = busy || !job.Can;
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        button.CustomMinimumSize = new Vector2(96, 0);
        row.AddChild(button);
        tile.AddChild(row);
        if (!job.Can && !job.Done)
        {
            tile.Modulate = new Color(1, 1, 1, 0.6f);
        }

        return tile;
    }

    /// <summary>The buzz as a number and a bar, with what it means and how fast it fades.</summary>
    private static Control BuzzBox(CampaignView view)
    {
        var panel = Boxed();
        panel.Name = "Buzz";
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        var top = new HBoxContainer();
        var title = UiStyle.Heading("Buzz", 22);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(title);
        top.AddChild(UiStyle.Heading($"{view.Buzz}", 22));
        box.AddChild(top);
        var bar = new ProgressBar { MinValue = 0, MaxValue = 100, Value = view.Buzz, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 12) };
        bar.AddThemeStyleboxOverride("background", UiStyle.Box(UiStyle.Border, 6));
        bar.AddThemeStyleboxOverride("fill", UiStyle.Box(UiStyle.Sunflower, 6));
        box.AddChild(bar);
        var said = Wrapped($"{view.BuzzWords}. Buzz sells tickets, and falls by {view.BuzzFade} a month unless you feed it.", 350, muted: true, size: 12);
        box.AddChild(said);
        return panel;
    }

    private static Control NewsBox(NewsView news, Action<int>? answer, bool busy)
    {
        var panel = Boxed(UiStyle.SunTint, UiStyle.Sunflower);
        panel.Name = "News";
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);
        box.AddChild(UiStyle.Caps("News  ·  wants an answer this month", pink: true));
        box.AddChild(Wrapped(news.Headline, 350, size: 15));
        box.AddChild(Wrapped(news.About, 350, muted: true, size: 12));
        var row = new HFlowContainer();
        row.AddThemeConstantOverride("h_separation", 6);
        row.AddThemeConstantOverride("v_separation", 6);
        for (var i = 0; i < news.Choices.Count; i++)
        {
            var choice = news.Choices[i];
            var index = i;
            var button = UiStyle.ChipButton(choice.Label, answer is null || busy ? () => { } : () => answer(index));
            button.Name = $"Answer {i}";
            button.TooltipText = choice.Outcome;
            button.Disabled = busy || !choice.Can;
            row.AddChild(button);
        }

        box.AddChild(row);
        return panel;
    }

    private static Control ReleaseRow(ReleaseView release)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var words = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", -2);
        words.AddChild(UiStyle.Body(release.Name, size: 14, bold: true));
        words.AddChild(UiStyle.Body($"{release.Sold:N0} of {release.Size:N0} sold", muted: true, size: 12));
        row.AddChild(words);
        var chip = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var (background, ink) = release.State switch
        {
            "Sold out" => (new Color("#DDF3E5"), UiStyle.FirstAidGreen),
            "On sale" => (UiStyle.PinkTint, UiStyle.PinkPressed),
            _ => (UiStyle.Border, UiStyle.TextMuted),
        };
        var box = UiStyle.Box(background, 999);
        box.ContentMarginLeft = box.ContentMarginRight = 10;
        box.ContentMarginTop = box.ContentMarginBottom = 2;
        chip.AddThemeStyleboxOverride("panel", box);
        var state = UiStyle.Body(release.State, size: 12, bold: true);
        state.AddThemeColorOverride("font_color", ink);
        chip.AddChild(state);
        row.AddChild(chip);
        return row;
    }

    /// <summary>
    /// The ticket campaign from January to June: the month's jobs down the left, the buzz, the
    /// sales line and the news in the middle, and the tickets, the releases, the price and the
    /// button that runs the month on the right.
    /// </summary>
    public static Control Campaign(CampaignView view, CampaignActions? does = null, bool busy = false, float scroll = 0)
    {
        does ??= new CampaignActions();
        var sales = view.Sales;
        var panel = Card(1220);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        panel.AddChild(box);

        var year = new HBoxContainer();
        year.AddThemeConstantOverride("separation", 6);
        var months = new[] { "January", "February", "March", "April", "May", "June", "July: build week" };
        for (var i = 0; i < months.Length; i++)
        {
            var now = i == Mathf.Min(sales.Turn, 6);
            var done = i < sales.Turn && !now;
            var dot = new PanelContainer { CustomMinimumSize = new Vector2(38, 38), TooltipText = months[i] };
            dot.AddThemeStyleboxOverride("panel", now ? UiStyle.Box(UiStyle.StagePink, 999) : done ? UiStyle.Box(UiStyle.NightInk, 999) : UiStyle.Outlined(UiStyle.Surface, 999, 0, new Color("#D9D3DC")));
            var letter = UiStyle.Body(months[i][..1], size: 15);
            letter.AddThemeFontOverride("font", UiStyle.SansBold);
            letter.AddThemeColorOverride("font_color", now || done ? Colors.White : new Color("#A8A1AD"));
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            dot.AddChild(letter);
            year.AddChild(dot);
        }

        Masthead(box, $"{sales.Festival}  ·  The ticket campaign", sales.Next is null ? "On to build week" : $"{sales.Next}. What will you do this month?", year, 36);

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 20);
        box.AddChild(columns);

        // Left: the jobs.
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(400, 0) };
        left.AddThemeConstantOverride("separation", 8);
        columns.AddChild(left);
        var head = new HBoxContainer();
        var title = UiStyle.Heading("Jobs this month", 22);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        var count = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, Name = "JobsLeft" };
        var pill = UiStyle.Box(view.JobsLeft > 0 ? UiStyle.NightInk : UiStyle.TextMuted, 999);
        pill.ContentMarginLeft = pill.ContentMarginRight = 12;
        pill.ContentMarginTop = pill.ContentMarginBottom = 3;
        count.AddThemeStyleboxOverride("panel", pill);
        var left_ = UiStyle.Body(view.JobsLeft == 0 ? "No time left this month" : $"Time for {view.JobsLeft} of {view.JobsAMonth}", size: 12, bold: true);
        left_.AddThemeColorOverride("font_color", Colors.White);
        count.AddChild(left_);
        head.AddChild(count);
        left.AddChild(head);
        var scroller = new ScrollContainer { CustomMinimumSize = new Vector2(400, 520), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Name = JobsScroll, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        left.AddChild(scroller);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroller.AddChild(list);
        var group = "";
        foreach (var job in view.Jobs)
        {
            if (job.Group != group)
            {
                group = job.Group;
                var caption = Section(group);
                if (list.GetChildCount() > 0)
                {
                    list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });
                }

                list.AddChild(caption);
            }

            list.AddChild(JobTile(job, does.Job, busy));
        }

        if (scroll > 0)
        {
            scroller.Ready += () => scroller.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, (int)scroll);
        }

        // Right of the jobs: two columns, and the getting there row under both when there is one.
        var rest = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        rest.AddThemeConstantOverride("separation", 10);
        columns.AddChild(rest);
        var pair = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        pair.AddThemeConstantOverride("separation", 20);
        rest.AddChild(pair);

        var middle = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
        middle.AddThemeConstantOverride("separation", 10);
        pair.AddChild(middle);
        middle.AddChild(BuzzBox(view));
        var marks = new List<int>();
        foreach (var note in view.Notes)
        {
            if (note.What.EndsWith("announced", StringComparison.Ordinal))
            {
                marks.Add(note.Turn);
            }
        }

        middle.AddChild(new SalesChart { Name = "Chart", SoldAfter = sales.SoldAfter, Licence = sales.Licence, Marks = marks, CustomMinimumSize = new Vector2(390, sales.Travel is null ? 220 : 170), Theme = UiStyle.Theme });
        if (view.News is not null)
        {
            middle.AddChild(NewsBox(view.News, does.News, busy));
        }
        else
        {
            var notes = new VBoxContainer { Name = "Notes" };
            notes.AddThemeConstantOverride("separation", 0);
            if (view.Notes.Count == 0)
            {
                notes.AddChild(Wrapped("Nothing has happened yet. Tickets only sell when people hear about the festival: announce an act, or get it talked about.", 380, muted: true, size: 13));
            }

            for (var i = view.Notes.Count - 1; i >= Mathf.Max(0, view.Notes.Count - 4); i--)
            {
                notes.AddChild(MoneyRow(view.Notes[i].What, view.Notes[i].When, muted: true));
            }

            middle.AddChild(notes);
        }

        var right = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(322, 0) };
        right.AddThemeConstantOverride("separation", 8);
        pair.AddChild(right);
        var numbers = new HBoxContainer();
        numbers.AddThemeConstantOverride("separation", 20);
        numbers.AddChild(BigNumber($"{sales.Sold:N0}", sales.Sold >= sales.Licence ? "sold. Sold out" : $"sold of {sales.Licence:N0}", UiStyle.StagePink, 40, "Sold"));
        numbers.AddChild(BigNumber($"£{sales.Taken:N0}", "taken", null, 40, "Taken"));
        right.AddChild(numbers);
        var bank = UiStyle.Body($"£{sales.Bank:N0} in the bank", muted: true, size: 13);
        bank.Name = "Bank";
        right.AddChild(bank);
        var releases = Boxed();
        releases.Name = "Releases";
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 4);
        releases.AddChild(rows);
        foreach (var release in view.Releases)
        {
            rows.AddChild(ReleaseRow(release));
        }

        right.AddChild(releases);
        var priced = PriceRow(sales.Price, sales.FairPrice, busy ? null : does.Price, "Ticket price  ·  judged on the acts announced");
        priced.TooltipText = "Each release sells at the price set while it is on sale. People judge the price by the acts they know about, so it is worth more once the big names are announced.";
        right.AddChild(priced);
        if (sales.Vip is { } vip)
        {
            var vipRow = PriceRow(vip.Price, vip.Fair, busy ? null : does.VipPrice, $"VIP price  ·  {vip.Sold:N0} of {vip.OnSale:N0} sold");
            vipRow.Name = "VipTicket";
            right.AddChild(vipRow);
        }

        right.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var short_ = sales.AdCost > sales.Bank;
        var nothing = view.OnSaleNow == 0 && sales.Sold < sales.Licence;
        var said = Wrapped(short_ ? "There is not the money for these adverts. Stop some."
            : sales.Sold >= sales.Licence ? "Every ticket is sold."
            : nothing ? "Every ticket on sale is sold. Open the next release to sell more."
            : $"{sales.Next} should sell about {sales.Forecast:N0}.{(sales.AdCost > 0 ? $" Adverts £{sales.AdCost:N0}." : "")}", 310, size: 14);
        said.Name = "Likely";
        if (nothing || short_)
        {
            said.AddThemeColorOverride("font_color", new Color("#B57D00"));
        }

        right.AddChild(said);
        var go = UiStyle.Button(sales.Next is null ? "On to build week" : busy ? $"{sales.Next} is running" : $"Run {sales.Next}", does.Run ?? (() => { }), Sounds.Coins);
        go.Disabled = busy || short_;
        go.Name = "Run";
        go.CustomMinimumSize = new Vector2(0, 50);
        right.AddChild(go);

        if (sales.Travel is not null)
        {
            rest.AddChild(Travel(sales.Travel, busy ? null : does.Parking, busy ? null : does.Buses, camping: busy ? null : does.Camping));
        }

        return panel;
    }
}
