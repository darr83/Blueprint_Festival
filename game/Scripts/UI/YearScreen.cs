using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Show;
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
        if (_shown != _game.Phase
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
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _shown = _game.Phase;
        _litterShown = _game.Litter.Count;
        _moneyShown = _game.Money;
        _side = null;
        HasCentrePanel = false;
        switch (_game.Phase)
        {
            case Phase.Plan:
                Centre(YearPanels.Plan(PlanOf(_game), StartBuilding, PickTicket, PickHeadliner, TogglePonchos, ToggleSecondStage));
                break;
            case Phase.Build:
                Side(YearPanels.BuildDay(_game.Crowd.Expected, _game.Money, OpenGates, _game.Weather.Summary));
                break;
            case Phase.Tidy:
                Side(YearPanels.Tidy(_game.Litter.Count, _game.LitterAtStart, _game.ScoutsFee, () => PayScouts(), HandBack));
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
        AddChild(new ColorRect { Color = new Color(0.11f, 0.17f, 0.21f, 0.45f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Stop });
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(centre);
        centre.AddChild(panel);
        HasCentrePanel = true;
    }

    private void Side(Control panel)
    {
        AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, 18);
        panel.GrowHorizontal = GrowDirection.Begin;
        panel.Position = new Vector2(panel.Position.X, 62);
        _side = panel;
    }

    /// <summary>What a kind of act is called on the planning screen.</summary>
    public static string KindText(Act act) => act.Kind switch
    {
        ActKind.Dj => "DJ",
        ActKind.Solo => "Solo singer",
        ActKind.Duo => "Duo",
        _ => $"{act.Members.Count switch { 3 => "Three", 4 => "Four", 5 => "Five", var n => n.ToString() }}-piece {(act.Kind == ActKind.FolkBand ? "folk band" : "band")}",
    };

    public static PlanView PlanOf(FestivalGame game) => new(
        game.Year, game.Name, game.Day.ToString("dddd d MMMM yyyy"), game.Licence, game.Money,
        FestivalGame.TicketPrices.Select(p => new TicketChoice(p, game.ExpectedAt(p))).ToList(),
        FestivalGame.TicketPrices.ToList().IndexOf(game.TicketPrice),
        game.Headliners.Select(h => new ActOffer(h.Act.Name, KindText(h.Act), h.Fee, h.Stars)).ToList(),
        game.HeadlinerPicked,
        game.Show.Slots.Take(6).Select(s => s.Act.Name).ToList(),
        game.SupportFee / 6, game.EarlyForecast, game.PonchosOrdered, game.PonchoCost, game.Ground.Name, game.FieldRent,
        game.SecondStageBooked, game.SecondStageFee);

    public static ResultsView ResultsOf(FestivalGame game)
    {
        var r = game.Result!;
        return new ResultsView(r.Year, $"{game.Name}, {game.Day:dddd d MMMM yyyy}", r.Came, r.Expected, r.Happy, r.WentHome,
            Lines(game.Income), Lines(game.Spending), r.LitterCleared, r.GrassWorn, r.Verdict, r.LicenceNow, r.LicenceNext,
            r.Treated, r.Untreated, r.TroubleSorted, r.TroubleIgnored, r.CrushInjuries, r.PulledOut, r.FieldRepair, r.Weather);
    }

    private static List<(string What, int Amount)> Lines(IEnumerable<LedgerLine> lines) => lines.Select(l => (l.What, l.Amount)).ToList();

    public void PickTicket(int index)
    {
        _game.ChooseTicket(FestivalGame.TicketPrices[index]);
        Rebuild();
    }

    public void ToggleSecondStage()
    {
        _game.BookSecondStage(!_game.SecondStageBooked);
        Rebuild();
    }

    public void TogglePonchos()
    {
        _game.OrderPonchos(!_game.PonchosOrdered);
        Rebuild();
    }

    public void PickHeadliner(int index)
    {
        _game.ChooseHeadliner(index);
        Rebuild();
    }

    public void StartBuilding()
    {
        if (_game.ConfirmPlan())
        {
            Rebuild();
        }
    }

    public void OpenGates()
    {
        _game.OpenGates();
        Rebuild();
    }

    public bool PayScouts()
    {
        var paid = _game.PayScouts();
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
