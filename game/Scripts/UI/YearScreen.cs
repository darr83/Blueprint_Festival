using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.Sales;
using BlueprintFestival.Simulation.Show;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// Shows the right screen for where the festival is in its year: planning, the build day, the
/// morning after and the results. On the morning after it also picks up litter under the mouse.
/// </summary>
public partial class YearScreen : Control
{
    /// <summary>How far the litter picker reaches, in metres.</summary>
    public const double Reach = 1.5;

    private FestivalGame _game = null!;
    private FestivalScene _scene = null!;
    private Phase? _shown;
    private int _litterShown = -1;
    private int _moneyShown = int.MinValue;
    private Control? _side;
    private int _sideScrollWanted;
    private int _sideScrollTries;
    private int _slot = RunningOrder.SetsAStage - 1;
    private string? _offerFor;
    private int _offer;
    private string _offerSaid = "";
    private bool _booking;
    private float _run = -1;
    private int _turnShown = -1;
    private int _priceShown = -1;
    private FieldPlan? _draft;
    private string _fieldPicked = Estate.VillageGreen;

    /// <summary>True while the January screen is the estate map, before the acts are booked.</summary>
    public bool ChoosingFields => _game.Phase == Phase.Plan && _game.Plan is not null && !GameManager.Instance.FieldsChosen;

    /// <summary>The fields as picked so far on the estate map.</summary>
    public FieldPlan Draft => _draft ??= _game.Plan?.Copy() ?? new FieldPlan();

    /// <summary>How long a month takes to run on the sales screen, in seconds.</summary>
    public const float MonthSeconds = 2.4f;

    /// <summary>True while a month is running on the sales screen.</summary>
    public bool Running => _run >= 0;

    /// <summary>The set picked on the planning screen, which the next artist clicked is booked for.</summary>
    public int SlotPicked => _slot;

    /// <summary>Which part of the year is on screen.</summary>
    public Phase Showing => _shown ?? Phase.Day;

    /// <summary>True while a panel is up in the middle of the screen.</summary>
    public bool HasCentrePanel { get; private set; }

    public bool HasSidePanel => _side is not null;

    /// <summary>Where the side panel is on screen, or an empty rect when there is none.</summary>
    public Rect2 SideRect => _side?.GetGlobalRect() ?? default;

    /// <summary>True when the side panel's lines are cut short to fit, and scroll.</summary>
    public bool SideScrolls => _side?.FindChild(YearPanels.SideScroll, true, false) is ScrollContainer { } s && s.GetChildCount() > 0
        && s.GetChild(0) is Control lines && s.CustomMinimumSize.Y + 0.5f < lines.GetCombinedMinimumSize().Y;

    public void Initialise(FestivalGame game, FestivalScene scene)
    {
        _game = game;
        _scene = scene;
        Theme = UiStyle.Theme;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (Running)
        {
            _run += (float)delta / MonthSeconds;
            if (_run >= 1)
            {
                FinishRun();
            }
            else if (FindChild("Chart", true, false) is SalesChart chart)
            {
                chart.Shown = _run;
                chart.QueueRedraw();
                if (FindChild("Sold", true, false) is Label sold)
                {
                    sold.Text = $"{Mathf.RoundToInt(chart.SoldShown):N0}";
                }
            }

            return;
        }

        if (_shown != _game.Phase
            || (_game.Phase == Phase.Build && (_turnShown != _game.SalesTurn || _priceShown != _game.TicketPrice))
            || (_game.Phase == Phase.Tidy && _litterShown != _game.Litter.Count)
            || (_game.Phase == Phase.Build && _moneyShown != _game.Money))
        {
            Rebuild();
        }

        if (_side is not null)
        {
            _side.Visible = !_scene.Money.Visible && !_scene.DayBoard.Visible;
            FitSide();
        }
    }

    /// <summary>Set by the smoke test to try the side panel on a short screen; null uses the real screen.</summary>
    public float? SideBottomForTest { get; set; }

    /// <summary>
    /// Makes the side panel fit the screen: its lines scroll when there is no room for them all,
    /// so its buttons always show above the view buttons and the build bar.
    /// </summary>
    public void FitSide()
    {
        if (_side?.FindChild(YearPanels.SideScroll, true, false) is not ScrollContainer scroll
            || scroll.GetChildCount() == 0 || scroll.GetChild(0) is not Control lines
            || _side.FindChild(YearPanels.SideFooter, true, false) is not Control footer)
        {
            return;
        }

        var view = (ViewButtons?)_scene.ViewButtons;
        var bottom = SideBottomForTest ?? SidePanelRoom.Bottom(GetViewportRect().Size.Y,
            view is { Visible: true } ? view.GlobalPosition.Y : null, _scene.BuildBar.Visible ? _scene.BuildBar.Top : null);
        var frame = _side.GetThemeStylebox("panel")?.GetMinimumSize().Y ?? 0;
        // The footer and the gap above it, which the scrolling lines must leave room for.
        var below = footer.GetCombinedMinimumSize().Y + 8;
        var want = SidePanelRoom.ScrollHeight(lines.GetCombinedMinimumSize().Y, _side.GlobalPosition.Y, bottom, frame, below);
        if (!Mathf.IsEqualApprox(scroll.CustomMinimumSize.Y, want))
        {
            scroll.CustomMinimumSize = new Vector2(0, want);
            _side.Size = new Vector2(_side.Size.X, 0);
        }

        // The scroll bar only takes the old position once the new lines are laid out, a frame or two on.
        if (_sideScrollWanted > 0 && _sideScrollTries > 0)
        {
            scroll.ScrollVertical = _sideScrollWanted;
            _sideScrollTries--;
            if (scroll.ScrollVertical == _sideScrollWanted)
            {
                _sideScrollWanted = 0;
            }
        }
    }

    private void Rebuild()
    {
        var old = GetChildren();

        var scroll = FindChild("Artists", true, false) is ScrollContainer artists ? artists.ScrollVertical : 0;
        var jobs = FindChild(YearPanels.JobsScroll, true, false) is ScrollContainer jobList ? jobList.ScrollVertical : 0;
        // A price change rebuilds the panel: keep it scrolled where the player had it.
        _sideScrollWanted = FindChild(YearPanels.SideScroll, true, false) is ScrollContainer side ? side.ScrollVertical : 0;
        _sideScrollTries = 3;
        _shown = Running ? Phase.Sell : _game.Phase;
        _turnShown = _game.SalesTurn;
        _priceShown = _game.TicketPrice;
        _litterShown = _game.Litter.Count;
        _moneyShown = _game.Money;
        _side = null;
        HasCentrePanel = false;
        foreach (var child in old)
        {
            RemoveChild(child);
            child.QueueFree();
        }

        switch (_shown)
        {
            case Phase.Plan:
                if (ChoosingFields)
                {
                    Centre(YearPanels.Fields(FieldsOf(_game, Draft, _fieldPicked), new FieldsActions(SelectField, UseField, ConfirmFields, TestSize)));
                    break;
                }

                Centre(YearPanels.Plan(PlanOf(_game, _slot, _offerFor, _offer, _offerSaid), new PlanActions(BookEvent, PickSlot, PickArtist, ToggleStage, ChangePrice, ToggleCamping,
                    _game.Plan is null ? null : ChangeFields, ChangeParking, ChangeBuses, MoveSet, ChangeCamping)
                {
                    ChangeOffer = ChangeOffer,
                    MakeOffer = MakeOffer,
                    PayAsk = PayAsk,
                    AddStage = AddStage,
                    RemoveStage = RemoveStage,
                }, scroll));
                break;
            case Phase.Sell when _game.Campaigning && _booking && !Running:
                Centre(YearPanels.Plan(PlanOf(_game, _slot, _offerFor, _offer, _offerSaid) with { Late = _game.NextSalesTurn ?? "" },
                    new PlanActions(StopBooking, PickSlot, PickArtist)
                    {
                        ChangeOffer = ChangeOffer,
                        MakeOffer = MakeOffer,
                        PayAsk = PayAsk,
                    }, scroll));
                break;
            case Phase.Sell when _game.Campaigning:
                Centre(YearPanels.Campaign(CampaignOf(_game, Running), new CampaignActions(RunMonth, DoJob, AnswerNews, ChangePrice, ChangeVipPrice, ChangeParking, ChangeBuses, ChangeCamping), Running, jobs));
                if (Running && FindChild("Chart", true, false) is SalesChart line)
                {
                    line.Shown = 0;
                }

                break;
            case Phase.Sell:
                Centre(YearPanels.Sales(SalesOf(_game, Running), RunMonth, ToggleAdvert, ChangePrice, Running, ChangeParking, ChangeBuses, ChangeCamping, ChangeVipPrice));
                if (Running && FindChild("Chart", true, false) is SalesChart chart)
                {
                    chart.Shown = 0;
                }

                break;
            case Phase.Build:
                Side(YearPanels.BuildDay(_game.Crowd.Expected, _game.Money, StartDay, _game.Weather.Summary, WeekOf(_game), NextDay, ChangePrice, TravelOf(_game), ChangeParking, ChangeBuses, ChangeCamping, ChangeVipPrice, _game.StageProblems));
                break;
            case Phase.Tidy:
                Side(YearPanels.Tidy(_game.Litter.Count, _game.LitterAtStart, _game.ScoutsFee, () => PayScouts(), HandBack, _game.LitterPicked, _game.LitterBinned));
                break;
            case Phase.Results:
                Centre(YearPanels.Results(ResultsOf(_game), NextYear));
                break;
            case Phase.Closed:
                Centre(YearPanels.Results(ResultsOf(_game), () => GameManager.Instance.ReturnToMenu(), closed: true));
                break;
        }
    }

    private void Centre(Control panel)
    {
        AddChild(new ColorRect { Color = new Color(UiStyle.NightInk, 0.38f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Stop });

        // The sheet sits in the middle of what is left under the badges along the top.
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, OffsetTop = UiStyle.TopClear - 14, OffsetBottom = -8 };
        AddChild(centre);
        centre.AddChild(panel);
        HasCentrePanel = true;
    }

    private void Side(Control panel)
    {
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 22);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.Position = new Vector2(panel.Position.X, UiStyle.TopClear);
        _side = panel;
        FitSide();
    }

    /// <summary>What a kind of act is called on the planning screen.</summary>
    public static string KindText(Act act) => act.Kind switch
    {
        ActKind.Dj => "DJ",
        ActKind.Solo => "Solo guitarist",
        ActKind.Duo => "Duo",
        _ => $"{act.Members.Count switch { 3 => "Three", 4 => "Four", 5 => "Five", var n => n.ToString() }}-piece {(act.Kind == ActKind.FolkBand ? "folk band" : "band")}",
    };

    private static string TimeOf(FestivalGame game, int set)
    {
        var show = game.ShowOf(set);
        return show.TimeOf(set - show.Base).Start.ToString("HH:mm");
    }

    private static string StageName(int set) => FestivalGame.StageTitle(RunningOrder.StageOf(set)).ToLowerInvariant();

    private static IReadOnlyList<SlotView> SlotsOf(FestivalGame game, RunningOrder show) => Enumerable.Range(0, RunningOrder.SetsAStage).Select(i =>
    {
        var set = show.Base + i;
        var act = show.At(set);
        return new SlotView(set, TimeOf(game, set), act?.Name, act is null ? "" : Roster.NameOf(act.Genre), act?.Rating ?? 0, act is null ? 0 : game.FeeFor(act))
        {
            CanEarlier = game.CanMoveSet(set, -1),
            CanLater = game.CanMoveSet(set, 1),
        };
    }).ToList();

    /// <summary>What a stage's line-up needs in build week, in a few words, or nothing with nobody booked.</summary>
    private static string NeedsOf(FestivalGame game, bool second) => NeedsOf(game, second ? 1 : 0);

    private static string NeedsOf(FestivalGame game, int stage) => game.StageNeeded(stage) is { } size
        ? $"needs a {Catalogue.Name(size).ToLowerInvariant()} stage, from £{Booking.CheapestOf(size).Cost:N0}"
        : "";

    /// <summary>Why an artist cannot be booked for a set, or nothing if they can.</summary>
    public static string BlockedFor(FestivalGame game, Artist artist, int set) => game.AvailabilityOf(artist) switch
    {
        Availability.Gone => "Signed for another festival",
        Availability.WalkedAway => "Turned you down twice",
        _ => FestivalGame.WouldPlaySet(artist.Rating, set) ? "" : "Only plays one of the last two sets",
    };

    /// <summary>Who else is after an artist and how long they will stay free.</summary>
    public static string RivalsText(FestivalGame game, Artist artist)
    {
        var rivals = game.RivalsFor(artist);
        if (rivals == 0 || game.GoneAfter(artist) is not { } last)
        {
            return "";
        }

        var month = last + 1 < FestivalGame.SalesMonths.Count ? FestivalGame.SalesMonths[last + 1] : "July";
        return $"{rivals} asking · gone by {month}";
    }

    /// <summary>What an artist wants, for the offer box: the size of stage and how late a set.</summary>
    public static string WantsText(Artist artist)
    {
        var size = Booking.StageFor(artist.Rating);
        var stage = size == StageSize.Small ? "Will play any stage" : $"Wants a {Catalogue.Name(size).ToLowerInvariant()} stage or bigger (from £{Booking.CheapestOf(size).Cost:N0} in build week)";
        return artist.Rating >= 4 ? $"{stage}, and one of the last two sets." : $"{stage}.";
    }

    /// <summary>What the planning screen shows, with one set picked to book an artist for and perhaps an offer being made.</summary>
    public static PlanView PlanOf(FestivalGame game, int picked = 3, string? offerFor = null, int offer = 0, string offerSaid = "")
    {
        var asked = offerFor is null ? null : Roster.Named(offerFor);
        return new(
        game.Year, game.Name, game.Day.ToString("dddd d MMMM yyyy"), game.Licence, game.Money,
        SlotsOf(game, game.Show), game.SecondStageBooked ? SlotsOf(game, game.SecondShow) : null,
        picked, $"the {StageName(picked)} at {TimeOf(game, picked)}",
        Roster.Artists.Select(a =>
        {
            var at = game.SetOf(a.Name);
            var kind = a.Kind switch { ActKind.Dj => "DJ", ActKind.Solo => "Solo guitarist", ActKind.Duo => "Duo", ActKind.FolkBand => "Folk band", _ => "Band" };
            return new ArtistView(a.Name, $"{kind} · {Roster.NameOf(a.Genre)} · {Roster.FansText(a.Fans)} fans · pulls a {FanPull.Words(FanPull.Of(a))}", a.Rating, game.FeeFor(a.Name),
                at is { } set && set != picked ? $"Booked for the {StageName(set)} at {TimeOf(game, set)}" : "", at == picked)
            {
                Blocked = at is null ? BlockedFor(game, a, picked) : "",
                Rivals = at is null ? RivalsText(game, a) : "",
                Asked = a.Name == offerFor,
            };
        }).ToList(),
        game.TicketPrice, (int)System.Math.Round(game.FairPrice), (int)System.Math.Round(game.LineUpPull * 100), game.ActFees, game.LineUp.Count(), game.PlanCost,
        game.EarlyForecast,
        game.Plan is { } fields ? string.Join(" and ", fields.Festival.Select(f => f.Name)) : game.Ground.Name, game.FieldRent,
        game.CampingBooked, game.CampingOffered ? FestivalGame.CampingCost : 0, game.CampersExpected, game.CampingPrice, TravelOf(game))
        {
            MainNeeds = NeedsOf(game, false),
            SecondNeeds = NeedsOf(game, true),
            Extra = game.Shows.Skip(1).Where(s => s.Booked)
                .Select(s => new StageBillView(s.Number, FestivalGame.StageTitle(s.Number), SlotsOf(game, s), NeedsOf(game, s.Number))).ToList(),
            Offer = asked is null ? null : new OfferView(asked.Name, asked.Rating, game.FeeFor(asked.Name), System.Math.Min(offer, game.FeeFor(asked.Name)),
                Booking.ChanceWords(game.ChanceOfYes(asked, offer)), WantsText(asked), offerSaid,
                game.AvailabilityOf(asked) == Availability.Free && FestivalGame.WouldPlaySet(asked.Rating, picked)),
        };
    }

    /// <summary>What the ticket sales screen shows.</summary>
    public static SalesView SalesOf(FestivalGame game, bool running = false)
    {
        // While a month plays out, the screen is still on that month.
        var turn = running ? game.SalesTurn - 1 : game.SalesTurn;
        var after = new List<int> { 0 };
        foreach (var line in game.SalesLog)
        {
            after.Add(after[^1] + line.Tickets);
        }

        return new SalesView(game.Name, game.OnSale, game.Money, turn, turn < FestivalGame.SalesMonths.Count ? FestivalGame.SalesMonths[turn] : null, game.TicketsSold,
            game.SalesLog.Sum(l => l.Taken), game.SalesLog.Sum(l => l.AdCost), game.AdvertReach, game.AdvertCost, game.SalesForecast,
            game.TicketPrice, (int)System.Math.Round(game.FairPrice), (int)System.Math.Round(game.LineUpPull * 100),
            Adverts.All.Select(a => new AdvertView(a.Id, a.Name, a.Cost, a.Reach, a.Reaches, game.AdvertOn(a.Id))).ToList(),
            game.SalesLog.Select(l => (l.When, l.Tickets, l.Price, l.AdCost, l.Vips, l.VipPrice)).ToList(), after, TravelOf(game), VipOf(game));
    }

    /// <summary>The sets nobody is booked for, on the stages that are booked.</summary>
    public static IReadOnlyList<int> EmptySets(FestivalGame game)
    {
        return game.BookedShows.SelectMany(show => Enumerable.Range(show.Base, RunningOrder.SetsAStage).Where(s => show.At(s) is null)).ToList();
    }

    /// <summary>What the campaign screen shows: the sales, and the month's jobs, buzz, releases and news.</summary>
    public static CampaignView CampaignOf(FestivalGame game, bool running = false)
    {
        var sales = SalesOf(game, running) with { FairPrice = (int)System.Math.Round(game.PerceivedFairPrice) };
        var time = game.JobsLeft > 0;
        var jobs = new List<JobView>();
        var closing = game.Show.Slots.LastOrDefault()?.Act.Name;
        foreach (var slot in game.Secret.OrderByDescending(s => game.AnnounceWorth(s.Act.Name)))
        {
            var act = slot.Act;
            var head = act.Name == closing ? " Your headliner: extra buzz." : "";
            jobs.Add(new JobView($"announce:{act.Name}", "Announce an act", $"Announce {act.Name}",
                $"{act.Rating} star{(act.Rating == 1 ? "" : "s")}, {Roster.FansText(act.Fans)} fans. About {game.AnnounceWorth(act.Name):N0} tickets this month.{head}", "Announce", time));
        }

        if (game.ReleaseOpen < Campaign.Releases.Count - 1)
        {
            var next = game.ReleaseOpen + 1;
            jobs.Add(new JobView("release", "Tickets", $"Open the {Campaign.Releases[next].Name.ToLowerInvariant()}",
                $"Puts {game.ReleaseSize(next):N0} more tickets on sale. {game.ReleaseLeft:N0} are left of those on sale now.", "Open", game.CanOpenRelease));
        }

        foreach (var stunt in Campaign.Stunts)
        {
            var ready = game.StuntReadyIn(stunt.Id);
            var cost = game.StuntCost(stunt);
            var again = stunt.EveryMonths == 0 ? "Once a year." : $"Every {stunt.EveryMonths} months.";
            jobs.Add(new JobView($"stunt:{stunt.Id}", "Get talked about", stunt.Name, $"{stunt.About} Buzz up {stunt.Buzz}. {again}",
                ready < 0 ? "Done" : ready > 0 ? $"In {ready} mo." : cost > 0 ? $"£{cost:N0}" : "Free", game.CanDoStunt(stunt.Id)));
        }

        foreach (var advert in Adverts.All)
        {
            var on = game.AdvertOn(advert.Id);
            jobs.Add(new JobView($"advert:{advert.Id}", $"Adverts  ·  reaching {game.AdvertReach:N0}  ·  paid each month they run", advert.Name,
                $"{advert.Reaches}. Reaches {advert.Reach:N0} people for £{advert.Cost:N0} a month.{(on ? " Stopping is not a job." : "")}", on ? "Stop" : "Start", on || time)
            {
                Done = on,
            });
        }

        var empty = EmptySets(game).Count;
        var free = Roster.Artists.Count(a => game.AvailabilityOf(a) == Availability.Free);
        var dearer = (int)System.Math.Round(Booking.DearerAMonth * 100 * game.SalesTurn);
        jobs.Add(new JobView("book", "The line-up", "Book another act",
            empty == 0 ? "Every set has an act." : $"{empty} set{(empty == 1 ? " is" : "s are")} empty and {free} artists are still free.{(dearer > 0 ? $" Fees are up {dearer}% since January." : "")}",
            "Book", time && empty > 0));

        var releases = Campaign.Releases.Select((r, i) => new ReleaseView(r.Name, game.ReleaseSize(i), game.ReleaseSold(i),
            game.ReleaseSize(i) > 0 && game.ReleaseSold(i) >= game.ReleaseSize(i) ? "Sold out" : i <= game.ReleaseOpen ? "On sale" : "Not yet")).ToList();
        var news = game.PendingNews is { } item
            ? new NewsView(item.Headline, item.About, item.Choices.Select((c, i) => new NewsChoiceView(c.Label, c.Outcome, game.CanAnswerNews(i))).ToList())
            : null;
        string When(int turn) => turn < FestivalGame.SalesMonths.Count ? FestivalGame.SalesMonths[turn] : "Build week";
        return new CampaignView(sales, game.JobsLeft, Campaign.JobsAMonth, (int)System.Math.Round(game.Buzz), Campaign.BuzzWords(game.Buzz), (int)Campaign.BuzzFade, jobs, releases,
            running ? null : news, game.CampaignNotes.Select(n => (n.Turn, When(n.Turn), n.What)).ToList(), game.ReleaseLeft);
    }

    /// <summary>Does one of the month's jobs, by the id on its card.</summary>
    public void DoJob(string id)
    {
        if (Running)
        {
            return;
        }

        var colon = id.IndexOf(':');
        var what = colon < 0 ? id : id[..colon];
        var which = colon < 0 ? "" : id[(colon + 1)..];
        bool done;
        switch (what)
        {
            case "announce":
                done = _game.Announce(which);
                break;
            case "release":
                done = _game.OpenRelease();
                break;
            case "stunt":
                done = _game.DoStunt(which);
                break;
            case "advert" when _game.AdvertOn(which):
                _game.SetAdvert(which, false);
                done = true;
                break;
            case "advert":
                done = _game.StartAdvert(which);
                break;
            case "book" when _game.JobsLeft > 0 && EmptySets(_game) is { Count: > 0 } empty:
                _booking = true;
                _slot = empty[^1];
                _offerFor = null;
                _offerSaid = "";
                done = true;
                break;
            default:
                done = false;
                break;
        }

        SoundBoard.Cue(done ? (what == "announce" ? Sounds.Coins : Sounds.Click) : Sounds.Refuse);
        Rebuild();
    }

    /// <summary>True while another act is being booked from the campaign screen.</summary>
    public bool BookingLate => _booking;

    /// <summary>Goes back from booking another act to the month's jobs.</summary>
    public void StopBooking()
    {
        _booking = false;
        _offerFor = null;
        _offerSaid = "";
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Answers this month's news with one of its choices.</summary>
    public void AnswerNews(int choice)
    {
        SoundBoard.Cue(_game.AnswerNews(choice) ? Sounds.Click : Sounds.Refuse);
        Rebuild();
    }

    /// <summary>The VIP tickets for the sales screens, or null in a year paid for at the gate.</summary>
    public static VipView? VipOf(FestivalGame game) => game.VipOnSale <= 0 ? null
        : new VipView(game.VipTicketsSold, game.VipOnSale, game.VipTicketPrice, (int)System.Math.Round(game.VipFairPrice), game.VipForecast, game.VipTakings);

    /// <summary>What the build panel shows in build week, or null in a year whose tickets are paid for at the gate.</summary>
    public static BuildWeekView? WeekOf(FestivalGame game) => !game.SoldAhead ? null
        : new BuildWeekView(game.NextSalesTurn, game.TicketsSold, game.OnSale, game.SalesForecast, game.AdvertCost, game.TicketPrice, (int)System.Math.Round(game.FairPrice), null, VipOf(game));

    public static ResultsView ResultsOf(FestivalGame game)
    {
        var r = game.Result!;
        return new ResultsView(r.Year, $"{game.Name}, {game.Day:dddd d MMMM yyyy}", r.Came, r.Expected, r.Happy, r.WentHome,
            Lines(game.Income), Lines(game.Spending), r.LitterCleared, r.GrassWorn, r.Verdict, r.LicenceNow, r.LicenceNext,
            r.Treated, r.Untreated, r.TroubleSorted, r.TroubleIgnored, r.CrushInjuries, r.PulledOut, r.FieldRepair, r.Weather)
        {
            CrushChoices = (r.Announcements, r.SecuritySent, r.ActsCancelled, r.HurtLeftAlone),
        };
    }

    private static List<(string What, int Amount)> Lines(IEnumerable<LedgerLine> lines) => lines.Select(l => (l.What, l.Amount)).ToList();

    public void PickTicket(int index)
    {
        _game.ChooseTicket(FestivalGame.TicketPrices[index]);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Picks the set that the next artist clicked is booked for.</summary>
    public void PickSlot(int set)
    {
        _slot = set;
        _offerSaid = "";
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Moves a set an hour earlier or later, and picks it.</summary>
    public void MoveSet(int set, int hours)
    {
        var moved = _game.MoveSet(set, hours);
        _slot = set;
        SoundBoard.Cue(moved ? Sounds.Click : Sounds.Refuse);
        Rebuild();
    }

    /// <summary>The artist an offer is being made to, or null.</summary>
    public string? OfferFor => _offerFor;

    /// <summary>The offer as it stands, in pounds.</summary>
    public int OfferNow => _offer;

    /// <summary>
    /// Picks an artist for the set picked. Someone already on the bill moves straight to it; anyone
    /// else has to be made an offer first. With null the set is left empty.
    /// </summary>
    public void PickArtist(string? artist)
    {
        _offerSaid = "";
        if (_game.Phase == Phase.Sell && (artist is null || _game.SetOf(artist) is not null || !EmptySets(_game).Contains(_slot)))
        {
            // Once tickets are on sale nobody can be moved or dropped: only empty sets can be filled.
            _offerSaid = artist is not null && _game.SetOf(artist) is null ? "Pick an empty set first." : "";
            SoundBoard.Cue(Sounds.Refuse);
            Rebuild();
            return;
        }

        if (artist is null || _game.SetOf(artist) is not null)
        {
            _offerFor = null;
            SoundBoard.Cue(_game.BookAct(_slot, artist) ? Sounds.Click : Sounds.Refuse);
            Rebuild();
            return;
        }

        if (Roster.Named(artist) is not { } found || BlockedFor(_game, found, _slot).Length > 0)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return;
        }

        _offerFor = artist;
        _offer = _game.FeeFor(artist);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Puts the offer up or down a step of about a twentieth of what the artist asks.</summary>
    public void ChangeOffer(int steps)
    {
        if (_offerFor is null)
        {
            return;
        }

        var ask = _game.FeeFor(_offerFor);
        var step = System.Math.Max(1, ask / 20);
        _offer = System.Math.Clamp(_offer + (steps * step), System.Math.Max(1, ask / 2), ask);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Makes the offer as it stands to the artist.</summary>
    public void MakeOffer() => Offer(_offer);

    /// <summary>Pays the artist what they ask.</summary>
    public void PayAsk() => Offer(int.MaxValue);

    private void Offer(int pounds)
    {
        if (_offerFor is not { } artist)
        {
            return;
        }

        var late = _game.Phase == Phase.Sell;
        var outcome = late ? _game.OfferLate(_slot, artist, pounds) : _game.Offer(_slot, artist, pounds);
        switch (outcome)
        {
            case OfferOutcome.Accepted:
                _offerFor = null;
                _offerSaid = "";
                _booking = false;
                SoundBoard.Cue(Sounds.Coins);
                break;
            case OfferOutcome.Refused:
                _offerSaid = "They said no. You can try once more.";
                SoundBoard.Cue(Sounds.Refuse);
                break;
            case OfferOutcome.WalkedAway:
                _offerSaid = "They said no again and have signed for another festival.";
                SoundBoard.Cue(Sounds.Refuse);
                break;
            default:
                _offerSaid = outcome == OfferOutcome.WantsLaterSet ? "They only play one of the last two sets."
                    : outcome == OfferOutcome.NoMoney ? "There is not the money in the bank for that."
                    : "They cannot be booked for this set.";
                SoundBoard.Cue(Sounds.Refuse);
                break;
        }

        Rebuild();
    }

    /// <summary>Adds the next stage (up to four in all) with empty sets to fill, and picks its first set.</summary>
    public void AddStage()
    {
        if (_game.AddEmptyStage() is var added and > 0)
        {
            _slot = RunningOrder.BaseOf(added);
            SoundBoard.Cue(Sounds.Click);
        }
        else
        {
            SoundBoard.Cue(Sounds.Refuse);
        }

        Rebuild();
    }

    /// <summary>Takes an extra stage and its acts off the bill; the stages after it move up a place.</summary>
    public void RemoveStage(int stage)
    {
        if (_game.RemoveStage(stage))
        {
            var left = _game.StagesBooked;
            // Stay on the same tab if a stage moved up into it, otherwise the one before.
            var showing = System.Math.Min(stage, left - 1);
            _slot = showing <= 0 ? RunningOrder.SetsAStage - 1 : RunningOrder.BaseOf(showing);
            SoundBoard.Cue(Sounds.Click);
        }

        Rebuild();
    }

    /// <summary>Adds a second stage with empty sets to fill, or takes it away again.</summary>
    public void ToggleStage()
    {
        if (_game.SecondStageBooked)
        {
            _game.RemoveStage(1);
            _slot = _game.SecondStageBooked ? RunningOrder.SecondBase : RunningOrder.SetsAStage - 1;
        }
        else
        {
            _game.AddEmptySecondStage();
            _slot = RunningOrder.SecondBase;
        }

        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Puts the ticket price up or down by some pounds.</summary>
    /// <summary>What the getting there rows show. Null where everyone can walk: the first years and the old single field.</summary>
    public static TravelView? TravelOf(FestivalGame game)
    {
        if (game.Plan is not { } plan || (game.Licence <= FestivalGame.Walkers && plan.ParkingSpaces == 0 && game.BusesHired == 0))
        {
            return null;
        }

        var sold = game.SoldAhead ? game.TicketsSold : 0;
        var split = game.TravelFor(sold);
        return new TravelView(FestivalGame.Walkers, plan.ParkingSpaces, game.CanDrive, game.ParkingCharge, FestivalGame.FairParking, game.BusesHired,
            FestivalGame.BusSeats, FestivalGame.BusCost, game.CanGetHere, plan.MusicRoom, game.Licence, sold, split.OnFoot, split.ByCar, split.ByBus)
        {
            Pitch = game.CampingBooked ? game.CampingPrice : null,
            Camping = (int)System.Math.Round(game.CamperShare * 100),
            Campers = game.CampersExpected,
            Pitches = plan.CampingRoom,
        };
    }

    public void ChangeParking(int pounds)
    {
        _game.SetParkingCharge(_game.ParkingCharge + pounds);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Puts the price of a camping pitch up or down by a pound.</summary>
    public void ChangeCamping(int pounds)
    {
        _game.SetCampingPrice(_game.CampingPrice + pounds);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    public void ChangeBuses(int buses)
    {
        _game.HireBuses(_game.BusesHired + buses);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    public void ChangePrice(int pounds)
    {
        _game.SetTicketPrice(_game.TicketPrice + pounds);
        Rebuild();
    }

    public void ChangeVipPrice(int pounds)
    {
        _game.SetVipTicketPrice(_game.VipTicketPrice + pounds);
        Rebuild();
    }

    public void ToggleAdvert(string id)
    {
        if (_game.Campaigning && _game.Phase == Phase.Sell)
        {
            DoJob($"advert:{id}");
            return;
        }

        _game.SetAdvert(id, !_game.AdvertOn(id));
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Books the acts and puts the tickets on sale: on to the sales screen.</summary>
    public void BookEvent()
    {
        if (_game.BookEvent(campaign: true))
        {
            _booking = false;
            Rebuild();
        }
        else
        {
            SoundBoard.Cue(Sounds.Refuse);
        }
    }

    /// <summary>Runs the next month of ticket sales and plays it out on the chart.</summary>
    public void RunMonth()
    {
        if (Running)
        {
            return;
        }

        if (_game.RunSales() is null)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return;
        }

        _run = 0;
        Rebuild();
    }

    /// <summary>Jumps to the end of the month that is running.</summary>
    public void FinishRun()
    {
        _run = -1;
        Rebuild();
    }

    /// <summary>Ends a day of build week, which sells that day's tickets.</summary>
    public void NextDay()
    {
        if (_game.RunSales() is null)
        {
            SoundBoard.Cue(Sounds.Refuse);
        }

        Rebuild();
    }

    public void ToggleSecondStage()
    {
        _game.BookSecondStage(!_game.SecondStageBooked);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>What the field choosing screen shows for a plan being drawn up.</summary>
    public static FieldsView FieldsOf(FestivalGame game, FieldPlan draft, string picked) => new(
        game.Year, game.Name, game.Licence, game.Money, draft, picked, draft.Problems(game.Licence, game.Year),
        game.Plan is null || !draft.Save().SequenceEqual(game.Plan.Save()), TestMode.On);

    /// <summary>Test mode: the same January one step up (1) or down (-1) the licence ladder.</summary>
    public void TestSize(int step)
    {
        if (!ChoosingFields)
        {
            return;
        }

        var licence = FestivalGame.TestLicenceStep(_game.Licence, step);
        if (licence == _game.Licence)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return;
        }

        SoundBoard.Cue(Sounds.Click);
        GameManager.Instance.Open(_game.AtTestSize(licence));
    }

    public void SelectField(string id)
    {
        _fieldPicked = id;
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Rents the picked field for a use, or gives it up with null.</summary>
    public void UseField(FieldUse? use)
    {
        Draft.Set(_fieldPicked, use);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Takes the fields as drawn up and moves on to the acts. A changed site is laid out again.</summary>
    public void ConfirmFields()
    {
        if (!ChoosingFields || Draft.Problems(_game.Licence, _game.Year).Count > 0)
        {
            SoundBoard.Cue(Sounds.Refuse);
            return;
        }

        SoundBoard.Cue(Sounds.Click);
        if (_game.Plan is not null && Draft.Save().SequenceEqual(_game.Plan.Save()))
        {
            GameManager.Instance.FieldsChosen = true;
            Rebuild();
        }
        else if (_game.WithFields(Draft) is { } moved)
        {
            GameManager.Instance.ChangeFields(moved);
        }
    }

    /// <summary>Back from the acts to the estate map.</summary>
    public void ChangeFields()
    {
        GameManager.Instance.FieldsChosen = false;
        _draft = null;
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    public void ToggleCamping()
    {
        _game.BookCamping(!_game.CampingBooked);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    public void PickHeadliner(int index)
    {
        _game.ChooseHeadliner(index);
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    public void StartBuilding()
    {
        if (_game.ConfirmPlan())
        {
            Rebuild();
        }
        else
        {
            SoundBoard.Cue(Sounds.Refuse);
        }
    }

    /// <summary>Ends the build: the day starts at eight with the gates shut and people queuing outside.</summary>
    public void StartDay()
    {
        _game.StartDay();
        Rebuild();
    }

    public bool PayScouts()
    {
        var paid = _game.PayScouts();
        SoundBoard.Cue(paid ? Sounds.Coins : Sounds.Refuse);
        Rebuild();
        return paid;
    }

    public void HandBack()
    {
        _game.HandBack();
        Rebuild();
    }

    public void NextYear() => GameManager.Instance.StartNextYear();

    /// <summary>Picks up the litter round a spot on the ground. Returns how many pieces.</summary>
    public int PickAt(double xMetres, double zMetres) => _game.Phase == Phase.Tidy ? _game.PickLitter(xMetres, zMetres, Reach) : 0;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F9 } && _game.Phase == Phase.Plan)
        {
            // Test mode: the size buttons on the January field screen.
            TestMode.Toggle();
            GetViewport()?.SetInputAsHandled();
            SoundBoard.Cue(Sounds.Click);
            Rebuild();
            return;
        }

        if (_game.Phase != Phase.Tidy)
        {
            return;
        }

        // A click picks up what is under the mouse; holding the button sweeps.
        var sweeping = @event is InputEventMouseMotion && Input.IsMouseButtonPressed(MouseButton.Left);
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && !sweeping)
        {
            return;
        }

        var camera = _scene.Camera.Camera;
        var mouse = GetViewport().GetMousePosition();
        var origin = camera.ProjectRayOrigin(mouse);
        var direction = camera.ProjectRayNormal(mouse);
        if (Mathf.Abs(direction.Y) > 0.001f)
        {
            var hit = origin + (direction * (-origin.Y / direction.Y));
            PickAt(hit.X, hit.Z);
        }
    }
}
