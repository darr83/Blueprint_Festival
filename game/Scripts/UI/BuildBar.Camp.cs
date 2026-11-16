using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The Camping group of the build bar: the kinds of camping ground to mark, the fire lane, the
/// showers, the info point, the camp shop and the toilets and taps, with the camp's checks above
/// them so the player can see what the camp still needs.
/// </summary>
public partial class BuildBar
{
    private VBoxContainer _checks = null!;
    private bool _watchingCamp;

    /// <summary>The key of a kind of camping ground, under Camping.</summary>
    public static string CampKey(CampZone zone) => $"camp{zone}";

    /// <summary>The lines of the camp's checks showing just now.</summary>
    public System.Collections.Generic.IReadOnlyList<string> CampCheckLines =>
        _checks.Visible ? _checks.GetChildren().OfType<Label>().Select(l => l.Text).ToList() : System.Array.Empty<string>();

    /// <summary>True while the prices of the dearer ways to camp can be changed on the Camping tab.</summary>
    public bool CampPricesShown => _checks.Visible && _checks.GetNodeOrNull("WaysToCamp") is not null;

    private HBoxContainer _campRow = null!;
    private readonly Button[] _campChips = new Button[2];

    /// <summary>True while the Camping group shows the things to put up, false while it shows the kinds of ground to mark.</summary>
    public bool CampThings { get; private set; }

    /// <summary>The two halves of the Camping group: the ground to mark, and the things to put up and the people to hire.</summary>
    private void AddCampRow(Container shelves)
    {
        _campRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _campRow.AddThemeConstantOverride("separation", 6);
        for (var i = 0; i < 2; i++)
        {
            var things = i == 1;
            var chip = UiStyle.ChipButton(things ? "Things to put up" : "Ground to mark", () => ShowCampThings(things), BlueprintFestival.Simulation.Sound.Sounds.Tab);
            chip.ToggleMode = true;
            _campRow.AddChild(chip);
            _campChips[i] = chip;
        }

        shelves.AddChild(_campRow);
    }

    /// <summary>Turns the Camping group to its kinds of ground or to the things to put up.</summary>
    public void ShowCampThings(bool things)
    {
        CampThings = things;
        if (_open != BuildMode.Camp)
        {
            return;
        }

        ClearChoices();
        AddCampChoices();
        _tool.Stop();
    }

    private void AddCampChoices()
    {
        if (!_watchingCamp)
        {
            _watchingCamp = true;
            _game.Site.Changed += ShowCampChecks;
            _game.Site.CampZonesChanged += ShowCampChecks;
        }

        _campChips[0].SetPressedNoSignal(!CampThings);
        _campChips[1].SetPressedNoSignal(CampThings);
        if (!CampThings)
        {
            foreach (var zone in CampLayout.Zones)
            {
                AddChoice(CampKey(zone), CampLayout.Name(zone), "Free", $"{CampLayout.About(zone)}\nDrag over camping ground to mark it. Start on ground marked this way to clear it.", () => _tool.PickCampZone(zone));
            }

            return;
        }

        foreach (var kind in new[] { StallKind.ShowerBlock, StallKind.CampInfo, StallKind.CampShop, StallKind.FirePit, StallKind.ToiletBlock, StallKind.WaterPoint })
        {
            var s = Catalogue.Of(kind);
            var menu = Catalogue.MenuOf(kind);
            var sells = menu.Count > 0 ? $"Sells {string.Join(", ", menu.Select(m => $"{m.Name.ToLowerInvariant()} £{m.UsualPrice}"))}. Click it once it is up to set its prices." : "Free to use.";
            AddChoice($"stall{kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{sells}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep. R turns it.", () => _tool.PickStall(kind));
        }

        var steward = Catalogue.Of(StaffKind.CampSteward);
        AddChoice($"staff{StaffKind.CampSteward}", steward.Name, $"£{steward.Wage:N0} a day", $"{steward.About}\nSecurity posted in a camping field walk the tents at night too, and stop thieves.", () => _tool.PickStaff(StaffKind.CampSteward));
    }

    /// <summary>Fills in the camp's checks while the Camping group is open, and hides them otherwise.</summary>
    private void ShowCampChecks()
    {
        if (_checks is null)
        {
            return;
        }

        foreach (var child in _checks.GetChildren())
        {
            _checks.RemoveChild(child);
            child.QueueFree();
        }

        _checks.Visible = _open == BuildMode.Camp;
        if (!_checks.Visible)
        {
            return;
        }

        if (!_game.HasCampingGround || !_game.CampingBooked)
        {
            _checks.AddChild(CheckLine("No camping field this year. Choose a field for camping when you plan the year, from 5,000 people.", new Color("#7C7783")));
            return;
        }

        foreach (var check in _game.CampChecks())
        {
            var (mark, colour) = check.State switch
            {
                CampCheckState.Met => ("✓", new Color("#1E9E52")),
                CampCheckState.Short => ("!", new Color("#A35F00")),
                _ => ("✗", new Color("#D8434B")),
            };
            _checks.AddChild(CheckLine($"{mark}  {check.Text}", colour));
        }

        if (_game.Phase is Phase.Plan or Phase.Sell or Phase.Build)
        {
            _checks.AddChild(WaysToCamp());
        }
    }

    /// <summary>The prices of the dearer ways to camp, each with a step down and a step up, set before the gates open.</summary>
    private Control WaysToCamp()
    {
        var row = new HBoxContainer { Name = "WaysToCamp", Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        var title = UiStyle.Body("Ways to camp:", size: 13, bold: true);
        row.AddChild(title);
        foreach (var kind in CampLayout.Extras)
        {
            var step = kind == CampKind.Glamping ? 10 : 5;
            row.AddChild(PriceButton("−", kind, -step));
            var label = UiStyle.Body($"{CampLayout.KindName(kind)} £{_game.CampKindPrice(kind)}", size: 13);
            label.TooltipText = $"What a party pays for {CampLayout.KindName(kind).ToLowerInvariant()}. Dearer means fewer book it. Mark ground for it above.";
            label.MouseFilter = MouseFilterEnum.Pass;
            row.AddChild(label);
            row.AddChild(PriceButton("+", kind, step));
            row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0) });
        }

        return row;
    }

    private Button PriceButton(string text, CampKind kind, int by)
    {
        var button = UiStyle.ChipButton(text, () =>
        {
            _game.SetCampKindPrice(kind, _game.CampKindPrice(kind) + by);
            ShowCampChecks();
        });
        button.Name = $"{kind}{(by > 0 ? "Up" : "Down")}";
        button.CustomMinimumSize = new Vector2(28, 26);
        return button;
    }

    private static Label CheckLine(string text, Color colour)
    {
        var line = UiStyle.Body(text, size: 13, bold: true);
        line.AddThemeColorOverride("font_color", colour);
        line.HorizontalAlignment = HorizontalAlignment.Center;
        line.MouseFilter = MouseFilterEnum.Ignore;
        return line;
    }
}
