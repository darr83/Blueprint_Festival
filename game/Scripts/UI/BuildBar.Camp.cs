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

    private void AddCampChoices()
    {
        if (!_watchingCamp)
        {
            _watchingCamp = true;
            _game.Site.Changed += ShowCampChecks;
            _game.Site.CampZonesChanged += ShowCampChecks;
        }

        foreach (var zone in CampLayout.Zones)
        {
            AddChoice(CampKey(zone), CampLayout.Name(zone), "Free", $"{CampLayout.About(zone)}\nDrag over camping ground to mark it. Start on ground marked this way to clear it.", () => _tool.PickCampZone(zone));
        }

        foreach (var kind in new[] { StallKind.ShowerBlock, StallKind.CampInfo, StallKind.CampShop, StallKind.ToiletBlock, StallKind.WaterPoint })
        {
            var s = Catalogue.Of(kind);
            var menu = Catalogue.MenuOf(kind);
            var sells = menu.Count > 0 ? $"Sells {string.Join(", ", menu.Select(m => $"{m.Name.ToLowerInvariant()} £{m.UsualPrice}"))}. Click it once it is up to set its prices." : "Free to use.";
            AddChoice($"stall{kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{sells}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep. R turns it.", () => _tool.PickStall(kind));
        }
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
