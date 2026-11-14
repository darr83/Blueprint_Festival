using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// What the "getting there" rows show: how many can walk, what the car parks and the buses can
/// bring, and how the tickets sold so far would travel.
/// </summary>
public sealed record TravelView(int Walkers, int Spaces, int CanDrive, int Parking, int FairParking, int Buses, int BusSeats, int BusCost,
    int CanGetHere, int MusicRoom, int Licence, int Sold, int OnFoot, int ByCar, int ByBus);

public static partial class YearPanels
{
    private static Control Stepper(string name, string value, System.Action<int>? change)
    {
        var row = new HBoxContainer { Name = name };
        row.AddThemeConstantOverride("separation", 6);
        var down = StepButton(false, change is null ? null : () => change(-1));
        var up = StepButton(true, change is null ? null : () => change(1));
        var label = UiStyle.Heading(value, 19);
        label.CustomMinimumSize = new Vector2(40, 0);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(down);
        row.AddChild(label);
        row.AddChild(up);
        return row;
    }

    private static Control TravelCell(string title, Control? stepper, string words, int width)
    {
        var cell = new VBoxContainer { CustomMinimumSize = new Vector2(width, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cell.AddThemeConstantOverride("separation", 2);
        var top = new HBoxContainer { CustomMinimumSize = new Vector2(0, 34) };
        top.AddThemeConstantOverride("separation", 8);
        var name = UiStyle.Body(title, size: 15, bold: true);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(name);
        if (stepper is not null)
        {
            top.AddChild(stepper);
        }

        cell.AddChild(top);
        var said = UiStyle.Body(words, muted: true, size: 12);
        said.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        said.CustomMinimumSize = new Vector2(width, 0);
        cell.AddChild(said);
        return cell;
    }

    /// <summary>What holds ticket sales back, in a line: the licence, the music fields or the ways of getting there.</summary>
    public static string TravelSummary(TravelView view)
    {
        var onSale = System.Math.Min(System.Math.Min(view.Licence, view.MusicRoom), System.Math.Max(view.CanGetHere, view.Sold));
        var held = view.CanGetHere < System.Math.Min(view.Licence, view.MusicRoom)
            ? $"Only {view.CanGetHere:N0} people have a way to get here, so that is all the tickets you can sell. Hire buses or rent a car park to sell more."
            : $"{view.CanGetHere:N0} people have a way to get here, enough for all {onSale:N0} tickets.";
        return view.Sold > 0 ? $"{held} Of the {view.Sold:N0} sold so far, {view.OnFoot:N0} walk, {view.ByCar:N0} drive and {view.ByBus:N0} take the bus." : held;
    }

    /// <summary>
    /// Getting there: villagers on foot, the parking charge and the shuttle buses, each with what
    /// it brings. Laid out in a row for the wide screens and stacked for the build panel.
    /// </summary>
    public static Control Travel(TravelView view, System.Action<int>? parking = null, System.Action<int>? buses = null, bool wide = true)
    {
        var tile = Tile(false, wide ? 560 : 300);
        tile.Name = "Travel";
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        tile.AddChild(box);
        BoxContainer cells = wide ? new HBoxContainer() : new VBoxContainer();
        cells.AddThemeConstantOverride("separation", wide ? 24 : 8);
        if (!wide)
        {
            box.AddChild(Section("Getting there"));
        }

        box.AddChild(cells);
        var width = wide ? 250 : 290;
        var dear = view.Parking > view.FairParking ? " Dear, so some will not pay it." : "";
        cells.AddChild(TravelCell("Parking, a car", view.Spaces > 0 ? Stepper("Parking", $"£{view.Parking}", parking) : null,
            view.Spaces > 0 ? $"{view.Spaces:N0} spaces bring about {view.CanDrive:N0}.{dear}" : "No car park field this year.", width));
        cells.AddChild(TravelCell("Shuttle buses", Stepper("Buses", $"{view.Buses}", buses),
            view.Buses > 0 ? $"Bring up to {view.Buses * view.BusSeats:N0}. £{view.Buses * view.BusCost:N0} on the day." : $"Each brings {view.BusSeats} from the station for £{view.BusCost}.", width));
        var summary = UiStyle.Body($"Getting there: about {view.Walkers:N0} can walk. {TravelSummary(view)}", size: 13);
        summary.Name = "Summary";
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        summary.CustomMinimumSize = new Vector2(wide ? 520 : 290, 0);
        box.AddChild(summary);
        return tile;
    }
}
