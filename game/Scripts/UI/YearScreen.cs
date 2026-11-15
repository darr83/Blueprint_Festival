using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Sales;
using BlueprintFestival.Simulation.Show;
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
    private int _slot = RunningOrder.SetsAStage - 1;
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
            _side.Visible = !_scene.Money.Visible;
        }
    }

    private void Rebuild()
    {
        var old = GetChildren();

        var scroll = FindChild("Artists", true, false) is ScrollContainer artists ? artists.ScrollVertical : 0;
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
                    Centre(YearPanels.Fields(FieldsOf(_game, Draft, _fieldPicked), new FieldsActions(SelectField, UseField, ConfirmFields)));
                    break;
                }

                Centre(YearPanels.Plan(PlanOf(_game, _slot), new PlanActions(BookEvent, PickSlot, PickArtist, ToggleStage, ChangePrice, TogglePonchos, ToggleCamping,
                    _game.Plan is null ? null : ChangeFields, ChangeParking, ChangeBuses, MoveSet), scroll));
                break;
            case Phase.Sell:
                Centre(YearPanels.Sales(SalesOf(_game, Running), RunMonth, ToggleAdvert, ChangePrice, Running, ChangeParking, ChangeBuses));
                if (Running && FindChild("Chart", true, false) is SalesChart chart)
                {
                    chart.Shown = 0;
                }

                break;
            case Phase.Build:
                Side(YearPanels.BuildDay(_game.Crowd.Expected, _game.Money, StartDay, _game.Weather.Summary, WeekOf(_game), NextDay, ChangePrice, TravelOf(_game), ChangeParking, ChangeBuses));
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
        var show = set >= RunningOrder.SecondBase ? game.SecondShow : game.Show;
        return show.TimeOf(set - show.Base).Start.ToString("HH:mm");
    }

    private static string StageName(int set) => set >= RunningOrder.SecondBase ? "second stage" : "main stage";

    private static IReadOnlyList<SlotView> SlotsOf(FestivalGame game, RunningOrder show) => Enumerable.Range(0, RunningOrder.SetsAStage).Select(i =>
    {
        var set = show.Base + i;
        var act = show.At(set);
        return new SlotView(set, TimeOf(game, set), act?.Name, act is null ? "" : Roster.NameOf(act.Genre), act?.Rating ?? 0, act is null ? 0 : game.FeeOf(act.Rating))
        {
            CanEarlier = game.CanMoveSet(set, -1),
            CanLater = game.CanMoveSet(set, 1),
        };
    }).ToList();

    /// <summary>What the planning screen shows, with one set picked to book an artist for.</summary>
    public static PlanView PlanOf(FestivalGame game, int picked = 3) => new(
        game.Year, game.Name, game.Day.ToString("dddd d MMMM yyyy"), game.Licence, game.Money,
        SlotsOf(game, game.Show), game.SecondStageBooked ? SlotsOf(game, game.SecondShow) : null,
        picked, $"the {StageName(picked)} at {TimeOf(game, picked)}",
        Roster.Artists.Select(a =>
        {
            var at = game.SetOf(a.Name);
            var kind = a.Kind switch { ActKind.Dj => "DJ", ActKind.Solo => "Solo guitarist", ActKind.Duo => "Duo", ActKind.FolkBand => "Folk band", _ => "Band" };
            return new ArtistView(a.Name, $"{kind} · {Roster.NameOf(a.Genre)} · {Roster.FansText(a.Fans)} fans", a.Rating, game.FeeOf(a.Rating),
                at is { } set && set != picked ? $"Booked for the {StageName(set)} at {TimeOf(game, set)}" : "", at == picked);
        }).ToList(),
        game.TicketPrice, (int)System.Math.Round(game.FairPrice), (int)System.Math.Round(game.LineUpPull * 100), game.ActFees, game.LineUp.Count(), game.PlanCost,
        game.EarlyForecast, game.PonchosOrdered, game.PonchoCost,
        game.Plan is { } fields ? string.Join(" and ", fields.Festival.Select(f => f.Name)) : game.Ground.Name, game.FieldRent,
        game.CampingBooked, game.CampingOffered ? FestivalGame.CampingCost : 0, game.CampersExpected, FestivalGame.CampingFee, TravelOf(game));

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
            game.SalesLog.Select(l => (l.When, l.Tickets, l.Price, l.AdCost)).ToList(), after, TravelOf(game));
    }

    /// <summary>What the build panel shows in build week, or null in a year whose tickets are paid for at the gate.</summary>
    public static BuildWeekView? WeekOf(FestivalGame game) => !game.SoldAhead ? null
        : new BuildWeekView(game.NextSalesTurn, game.TicketsSold, game.OnSale, game.SalesForecast, game.AdvertCost, game.TicketPrice, (int)System.Math.Round(game.FairPrice));

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

    /// <summary>Books an artist for the set picked, or with null leaves it empty.</summary>
    public void PickArtist(string? artist)
    {
        SoundBoard.Cue(_game.BookAct(_slot, artist) ? Sounds.Click : Sounds.Refuse);
        Rebuild();
    }

    /// <summary>Adds a second stage with empty sets to fill, or takes it away again.</summary>
    public void ToggleStage()
    {
        if (_game.SecondStageBooked)
        {
            _game.BookSecondStage(false);
            _slot = RunningOrder.SetsAStage - 1;
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
            FestivalGame.BusSeats, FestivalGame.BusCost, game.CanGetHere, plan.MusicRoom, game.Licence, sold, split.OnFoot, split.ByCar, split.ByBus);
    }

    public void ChangeParking(int pounds)
    {
        _game.SetParkingCharge(_game.ParkingCharge + pounds);
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

    public void ToggleAdvert(string id)
    {
        _game.SetAdvert(id, !_game.AdvertOn(id));
        SoundBoard.Cue(Sounds.Click);
        Rebuild();
    }

    /// <summary>Books the acts and puts the tickets on sale: on to the sales screen.</summary>
    public void BookEvent()
    {
        if (_game.BookEvent())
        {
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
        game.Plan is null || !draft.Save().SequenceEqual(game.Plan.Save()));

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

    public void TogglePonchos()
    {
        _game.OrderPonchos(!_game.PonchosOrdered);
        SoundBoard.Cue(_game.PonchosOrdered ? Sounds.Ponchos : Sounds.Click);
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
