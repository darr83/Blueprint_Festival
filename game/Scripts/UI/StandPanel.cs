using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// Stands on screen. While a stand is being put up, a card on the right to set its rows and roof
/// before dragging it out. Once one is up, a card for it: its size and places, who may use it (and
/// what a seat ticket costs), its steps and the security on them.
/// </summary>
public partial class StandPanel : Control
{
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private PanelContainer _setup = null!;
    private Label _setupKind = null!;
    private Label _rowsCaption = null!;
    private Label _rows = null!;
    private Label _setupAbout = null!;
    private readonly List<Button> _roofChips = new();
    private PanelContainer _editor = null!;
    private Label _kind = null!;
    private Label _size = null!;
    private readonly List<Button> _useChips = new();
    private HBoxContainer _priceRow = null!;
    private Label _price = null!;
    private Label _priceAbout = null!;
    private Label _steps = null!;
    private Button _takeDown = null!;
    private double _since = 1;

    /// <summary>True while the card for setting up the next stand is open.</summary>
    public bool SetupShown => _setup.Visible;

    /// <summary>True while the card for a stand that is up is open.</summary>
    public bool EditorShown => _editor.Visible;

    /// <summary>What the rows line says, for the launch test.</summary>
    public string RowsText => _rows.Text;

    /// <summary>What the size line of a stand's card says, for the launch test.</summary>
    public string SizeText => _size.Text;

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        // Setting up the next stand.
        _setup = Card();
        var box = Column(_setup);
        box.AddChild(UiStyle.Caps("Stands · drag along the front", pink: true));
        _setupKind = UiStyle.Heading("", 24);
        box.AddChild(_setupKind);
        box.AddChild(UiStyle.Rule(1));
        _rowsCaption = UiStyle.Caps("Rows");
        box.AddChild(_rowsCaption);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(UiStyle.ChipButton("−", () => _tool.SetStandRows(_tool.StandRows - 1)));
        _rows = UiStyle.Heading("", 26);
        _rows.CustomMinimumSize = new Vector2(60, 0);
        _rows.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(_rows);
        row.AddChild(UiStyle.ChipButton("+", () => _tool.SetStandRows(_tool.StandRows + 1)));
        box.AddChild(row);
        box.AddChild(UiStyle.Caps("Roof"));
        var roofs = new HBoxContainer();
        roofs.AddThemeConstantOverride("separation", 6);
        foreach (var roof in new[] { StandRoof.None, StandRoof.BackHalf, StandRoof.Full })
        {
            var chip = UiStyle.ChipButton(Stands.Name(roof), () => _tool.SetStandRoof(roof));
            chip.ToggleMode = true;
            _roofChips.Add(chip);
            roofs.AddChild(chip);
        }

        box.AddChild(roofs);
        _setupAbout = Wrapped("");
        box.AddChild(_setupAbout);
        AddChild(_setup);

        // A stand that is up.
        _editor = Card();
        var column = Column(_editor);
        column.AddChild(UiStyle.Caps("Stands · click inside to change", pink: true));
        _kind = UiStyle.Heading("", 24);
        column.AddChild(_kind);
        _size = UiStyle.Body("", size: 15, bold: true);
        column.AddChild(_size);
        column.AddChild(UiStyle.Rule(1));
        column.AddChild(UiStyle.Caps("Who can use it"));
        var uses = new HBoxContainer();
        uses.AddThemeConstantOverride("separation", 6);
        foreach (var use in new[] { StandUse.Anyone, StandUse.SeatTicket, StandUse.Vip })
        {
            var chip = UiStyle.ChipButton(use == StandUse.Anyone ? "Anyone" : Stands.Name(use), () => SetUse(use));
            chip.ToggleMode = true;
            _useChips.Add(chip);
            uses.AddChild(chip);
        }

        column.AddChild(uses);
        _priceRow = new HBoxContainer();
        _priceRow.AddThemeConstantOverride("separation", 6);
        _priceRow.AddChild(UiStyle.Body("Seat ticket", size: 15));
        _priceRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _priceRow.AddChild(UiStyle.ChipButton("−", () => ChangePrice(-1)));
        _price = UiStyle.Heading("", 22);
        _price.CustomMinimumSize = new Vector2(56, 0);
        _price.HorizontalAlignment = HorizontalAlignment.Center;
        _priceRow.AddChild(_price);
        _priceRow.AddChild(UiStyle.ChipButton("+", () => ChangePrice(1)));
        column.AddChild(_priceRow);
        _priceAbout = Wrapped("");
        column.AddChild(_priceAbout);
        column.AddChild(UiStyle.Rule(1));
        column.AddChild(UiStyle.Caps("Steps"));
        _steps = Wrapped("");
        column.AddChild(_steps);
        column.AddChild(UiStyle.Rule(1));
        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 10);
        buttons.AddChild(UiStyle.Button("Done", () => _tool.SelectStand(-1)));
        _takeDown = UiStyle.SecondaryButton("Take it down", TakeDown);
        buttons.AddChild(_takeDown);
        column.AddChild(buttons);
        AddChild(_editor);

        tool.StandPicked += Refresh;
        game.Site.StandsChanged += Refresh;
        game.Site.Changed += Refresh;
        Refresh();
    }

    public override void _ExitTree()
    {
        _tool.StandPicked -= Refresh;
        _game.Site.StandsChanged -= Refresh;
        _game.Site.Changed -= Refresh;
    }

    private static PanelContainer Card()
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(370, 0), Visible = false };
        card.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 20, radius: 14));
        return card;
    }

    private static VBoxContainer Column(PanelContainer card)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        card.AddChild(box);
        return box;
    }

    private static Label Wrapped(string text)
    {
        var label = UiStyle.Body(text, muted: true, size: 14);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(330, 0);
        return label;
    }

    private PlacedStand? Selected => _tool.SelectedStand >= 0 && _tool.SelectedStand < _game.Site.Stands.Count ? _game.Site.Stands[_tool.SelectedStand] : null;

    private void SetUse(StandUse use)
    {
        if (Selected is not null)
        {
            _game.SetStandUse(_tool.SelectedStand, use);
        }

        Refresh();
    }

    private void ChangePrice(int by)
    {
        if (Selected is { } stand)
        {
            _game.SetStandPrice(_tool.SelectedStand, stand.Price + by);
        }
    }

    private void TakeDown()
    {
        if (Selected is { } stand)
        {
            _tool.SelectStand(-1);
            _game.Remove(stand.At(0, 0));
        }
    }

    /// <summary>Opens the card for a stand. For the launch test.</summary>
    public void Open(int stand) => _tool.SelectStand(stand);

    private void Refresh()
    {
        _since = 0;
        var building = _game.CanBuild && _tool.Mode == BuildMode.Stand;
        _setup.Visible = building && Selected is null;
        _editor.Visible = building && Selected is not null;
        if (_setup.Visible)
        {
            var kind = _tool.StandKind;
            var tiered = Stands.Tiered(kind);
            _setupKind.Text = Stands.Name(kind);
            _rowsCaption.Text = tiered ? "ROWS" : "METRES DEEP";
            _rows.Text = $"{_tool.StandRows}";
            for (var i = 0; i < _roofChips.Count; i++)
            {
                _roofChips[i].SetPressedNoSignal((int)_tool.StandRoof == i);
            }

            var price = kind switch
            {
                StandKind.Seated => $"£{Stands.SeatCost} a seat",
                StandKind.Terrace => $"£{Stands.TerraceCost} a standing place",
                StandKind.Deck => $"£{Stands.DeckCostPerMetre} a square metre",
                _ => $"£{Stands.AccessibleCostPerMetre} a square metre",
            };
            _setupAbout.Text = (tiered
                    ? $"Each row is 1 m deep and {Stands.Rise:0.#} m higher than the one in front. More rows see further but cost more. "
                    : kind == StandKind.Deck ? $"The deck stands {Stands.DeckHeight:0.#} m up, two people a square metre. " : $"The deck is {Stands.AccessibleHeight:0.#} m up with a ramp: one wheelchair a square metre. ")
                + $"{price}, rail £{Stands.RailCostPerMetre} a metre, roof £{Stands.RoofCostPerMetre} a metre (£{Stands.HalfRoofCostPerMetre} for the back half).\n"
                + $"Drag along the front. It faces the nearest stage{(_tool.StandTurned ? ", turned round" : "")}; R turns it round.";
        }

        if (Selected is not { } stand)
        {
            return;
        }

        var index = _tool.SelectedStand;
        var places = stand.Places;
        _kind.Text = Stands.Name(stand.Kind);
        var what = Stands.Tiered(stand.Kind) ? $"{stand.Rows} rows · {places:N0} {(stand.Kind == StandKind.Seated ? "seats" : "standing")}" : $"{stand.Rows} m deep · {places:N0} places";
        _size.Text = $"{stand.Length * FieldMap.TileMetres:0.#} m long · {what}";
        for (var i = 0; i < _useChips.Count; i++)
        {
            _useChips[i].SetPressedNoSignal((int)stand.Use == i);
        }

        _priceRow.Visible = stand.Use == StandUse.SeatTicket;
        _price.Text = $"£{stand.Price}";
        _priceAbout.Text = stand.Use switch
        {
            StandUse.SeatTicket => $"Paid to security at the steps. About {Crowd.WillPay(stand.Price):P0} of those who fancy a seat will pay £{stand.Price}.",
            StandUse.Vip => "Only VIP guests go up. The rail turns gold.",
            _ => stand.Kind == StandKind.Accessible ? "For people who need it, and their friends, first come first served." : "Free, first come first served.",
        };
        var manned = _game.StepsManned(index);
        _steps.Text = $"{stand.Steps.Count} {(stand.Steps.Count == 1 ? "set" : "sets")} of steps, {manned} with security by {(stand.Steps.Count == 1 ? "it" : "them")}. "
            + "Click the rail to add or close steps. Post one security by the steps to keep the stand to its places and take the seat tickets; with nobody there people squeeze on, and someone can fall.";
        _takeDown.Text = $"Take it down (£{_game.Site.RefundAt(stand.At(0, 0)):N0} back)";
    }

    public override void _Process(double delta)
    {
        var screen = GetViewportRect().Size;
        _setup.Position = new Vector2(screen.X - 22 - _setup.Size.X, UiStyle.TopClear + 8);
        _editor.Position = new Vector2(screen.X - 22 - _editor.Size.X, UiStyle.TopClear + 8);
        _since += delta;
        var building = _game.CanBuild && _tool.Mode == BuildMode.Stand;
        if (_since > 0.5 || building != (_setup.Visible || _editor.Visible))
        {
            Refresh();
        }
    }
}
