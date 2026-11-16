using System;
using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>What the field choosing screen shows.</summary>
public sealed record FieldsView(int Year, string Festival, int Licence, int Bank, FieldPlan Plan, string Selected, IReadOnlyList<string> Problems, bool Changed, bool TestMode = false);

/// <summary>What the buttons on the field choosing screen do.</summary>
public sealed record FieldsActions(Action<string>? Select = null, Action<FieldUse?>? Use = null, Action? Confirm = null, Action<int>? TestSize = null);

/// <summary>
/// The Larkmoor estate drawn as a map: every field with its name, the lane, the farm track, the
/// village and the bus stop. Rented fields are coloured by what they are for, with the gateways
/// between them. Clicking a field picks it.
/// </summary>
public partial class EstateMap : Control
{
    public static readonly Color MusicColour = new("#F29AC0");
    public static readonly Color FoodColour = new("#F7D774");
    public static readonly Color CampColour = new("#A9B8F0");
    public static readonly Color ParkColour = new("#B9C2C9");
    private static readonly Color Grass = new("#CFE3B4");
    private static readonly Color HedgeLine = new("#7A9A5A");
    private static readonly Color Land = new("#E6EFD6");
    private static readonly Color Road = new("#D9CDBB");
    private static readonly Color Gate = new("#8A6A4A");

    private const float Margin = 10;

    public FieldPlan Plan { get; set; } = new();

    public string Selected { get; set; } = "";

    public Action<string>? Picked { get; set; }

    public static Color ColourOf(FieldUse use) => use switch
    {
        FieldUse.Music => MusicColour,
        FieldUse.Food => FoodColour,
        FieldUse.Camping => CampColour,
        _ => ParkColour,
    };

    public static string NameOf(FieldUse use) => use switch
    {
        FieldUse.Music => "Music",
        FieldUse.Food => "Food and stalls",
        FieldUse.Camping => "Camping",
        _ => "Car park",
    };

    public static string UnitOf(FieldUse use) => use switch
    {
        FieldUse.Music => "people",
        FieldUse.Food => "people",
        FieldUse.Camping => "campers",
        _ => "cars",
    };

    private float Scale => Math.Min((Size.X - (2 * Margin)) / Estate.MetresWide, (Size.Y - (2 * Margin)) / Estate.MetresDeep);

    private Vector2 At(double x, double y) => new(Margin + ((float)x * Scale), Margin + ((float)y * Scale));

    private Rect2 RectOf(EstateField f) => new(At(f.X, f.Y), new Vector2(f.MetresWide * Scale, f.MetresDeep * Scale));

    /// <summary>The field under a point of the control, or null.</summary>
    public string? FieldAt(Vector2 point) => Estate.All.FirstOrDefault(f => RectOf(f).HasPoint(point))?.Id;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && FieldAt(click.Position) is { } id)
        {
            Picked?.Invoke(id);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var font = UiStyle.SansSemi;
        var s = Scale;
        DrawRect(new Rect2(Vector2.Zero, Size), Land);

        // The lane, with its verges, and the farm track north from it.
        DrawRect(new Rect2(0, At(0, 247).Y, Size.X, 7 * s), Road);
        DrawRect(new Rect2(At(248, 0).X, 0, 6 * s, At(0, 247).Y), Road);
        DrawString(font, At(Estate.MetresWide - 150, 253), "Larkmoor Lane, to the station", HorizontalAlignment.Right, 150 * s, 11, UiStyle.TextMuted);

        // The copse, the mill pond and the village.
        DrawRect(new Rect2(At(388, 82), new Vector2(44 * s, 80 * s)), new Color("#8FB07A"));
        DrawString(font, At(388, 126), "Copse", HorizontalAlignment.Center, 44 * s, 11, UiStyle.TextDark);
        DrawCircle(At(512, 296), 20 * s, new Color("#A9D3E6"));
        DrawString(font, At(482, 299), "Mill pond", HorizontalAlignment.Center, 60 * s, 11, UiStyle.TextDark);
        foreach (var (hx, hy) in new[] { (6, 266), (26, 264), (46, 268), (10, 288), (34, 290), (54, 286), (18, 310), (44, 312) })
        {
            DrawRect(new Rect2(At(hx, hy), new Vector2(13 * s, 10 * s)), new Color("#C9A48C"));
            DrawRect(new Rect2(At(hx, hy), new Vector2(13 * s, 10 * s)), new Color("#7B5A48"), false, 1);
        }

        DrawString(font, At(0, 328), "Larkmoor village", HorizontalAlignment.Left, 120 * s, 12, UiStyle.TextDark);

        foreach (var f in Estate.All)
        {
            var rect = RectOf(f);
            var use = Plan.UseOf(f.Id);
            DrawRect(rect, use is { } u ? ColourOf(u) : Grass);
            var picked = f.Id == Selected;
            DrawRect(rect, picked ? UiStyle.NightInk : HedgeLine, false, picked ? 4 : 2);
            var small = rect.Size.X < 90;
            var size = small ? 10 : 13;
            var middle = rect.Position.Y + (rect.Size.Y / 2);
            DrawString(font, new Vector2(rect.Position.X, middle - (use is null ? -4 : 6)), f.Name, HorizontalAlignment.Center, rect.Size.X, size, UiStyle.TextDark);
            if (use is { } used)
            {
                DrawString(font, new Vector2(rect.Position.X, middle + 9), small && used == FieldUse.CarPark ? "Parking" : small ? NameOf(used).Split(' ')[0] : NameOf(used), HorizontalAlignment.Center, rect.Size.X, size - 2, UiStyle.TextDark);
                DrawString(font, new Vector2(rect.Position.X, middle + 23), $"{f.RoomFor(used):N0} {UnitOf(used)}", HorizontalAlignment.Center, rect.Size.X, size - 2, UiStyle.TextDark);
            }
        }

        // A gateway through every hedge two festival fields share.
        var festival = Plan.Festival.ToList();
        for (var i = 0; i < festival.Count; i++)
        {
            for (var j = i + 1; j < festival.Count; j++)
            {
                if (Estate.Gateway(festival[i], festival[j]) is { } g)
                {
                    var half = g.NorthSouth ? new Vector2(5, 8) : new Vector2(8, 5);
                    DrawRect(new Rect2(At(g.X, g.Y) - half, half * 2), Gate);
                    DrawRect(new Rect2(At(g.X, g.Y) - half, half * 2), UiStyle.NightInk, false, 1);
                }
            }
        }

        // The way in, the car park ways out, and the bus stop.
        var doors = Plan.Of(FieldUse.CarPark).Where(f => f.OnLane).Select(f => (Field: f, Along: 0.5)).ToList();
        if (Plan.WayIn is { OnLane: true } wayIn)
        {
            doors.Add((wayIn, 0.2));
        }

        foreach (var (f, along) in doors)
        {
            var y = f.Bottom <= Estate.LaneTop ? f.Bottom : f.Y - 9;
            DrawRect(new Rect2(At(f.X + (f.MetresWide * along), y), new Vector2(12 * s, 9 * s)), Gate);
        }

        if (Plan.WayIn is { OnLane: false } byTrack)
        {
            var x = byTrack.Right <= Estate.TrackLeft ? byTrack.Right : Estate.TrackRight - 4;
            DrawRect(new Rect2(At(x, byTrack.Bottom - 30), new Vector2(4 * s, 14 * s)), Gate);
        }

        var stop = new Rect2(At(Estate.BusStopX, 243), new Vector2(56, 20));
        DrawRect(stop, Colors.White);
        DrawRect(stop, UiStyle.NightInk, false, 1.5f);
        DrawString(font, stop.Position + new Vector2(0, 14), "Bus stop", HorizontalAlignment.Center, 56, 11, UiStyle.NightInk);
    }
}

public static partial class YearPanels
{
    /// <summary>Test mode (F9): minus and plus to move the licence down or up the ladder, with the bank topped up.</summary>
    private static Control TestSizeRow(FieldsView view, FieldsActions does)
    {
        var row = new HBoxContainer { Name = "TestSize" };
        row.AddThemeConstantOverride("separation", 8);
        var label = UiStyle.Caps("Test mode  ·  festival size");
        label.AddThemeColorOverride("font_color", UiStyle.StagePink);
        row.AddChild(label);
        var down = UiStyle.SecondaryButton("−", () => does.TestSize?.Invoke(-1));
        down.Name = "Smaller";
        down.Disabled = view.Licence <= FestivalGame.FirstLicence;
        down.CustomMinimumSize = new Vector2(44, 34);
        row.AddChild(down);
        var size = UiStyle.Body($"{view.Licence:N0}", size: 16);
        size.CustomMinimumSize = new Vector2(70, 0);
        size.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(size);
        var up = UiStyle.SecondaryButton("+", () => does.TestSize?.Invoke(1));
        up.Name = "Bigger";
        up.Disabled = view.Licence >= FestivalGame.BiggestLicence;
        up.CustomMinimumSize = new Vector2(44, 34);
        row.AddChild(up);
        row.AddChild(UiStyle.Body($"The bank is topped up to at least £{FestivalGame.TestMoneyFor(view.Licence):N0}. Camping opens at {FieldPlan.CampingFrom:N0}. F9 hides this.", muted: true, size: 13));
        return row;
    }

    /// <summary>January, before the line-up: the estate map, the field picked, and what the plan adds up to.</summary>
    public static Control Fields(FieldsView view, FieldsActions? does = null)
    {
        does ??= new FieldsActions();
        var panel = Card(1180);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);
        Masthead(box, $"January  ·  {view.Festival}  ·  Year {view.Year}", "Choose your fields",
            UiStyle.Body($"Licence for up to {view.Licence:N0} people  ·  £{view.Bank:N0} in the bank  ·  Click a field, then say what it is for", muted: true, size: 14), 36);

        if (view.TestMode)
        {
            box.AddChild(TestSizeRow(view, does));
        }

        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 16);
        box.AddChild(columns);

        var map = new EstateMap { Name = "Estate", Plan = view.Plan, Selected = view.Selected, Picked = does.Select, CustomMinimumSize = new Vector2(760, 470), MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        columns.AddChild(map);

        var side = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        side.AddThemeConstantOverride("separation", 6);
        columns.AddChild(side);

        if (Estate.Find(view.Selected) is { } field)
        {
            var where = field.OnLane ? "on the lane" : field.OnTrack ? "on the farm track" : "reached through other fields";
            side.AddChild(Section(field.Name));
            side.AddChild(UiStyle.Body($"{field.MetresWide} m by {field.MetresDeep} m  ·  {(field.Rent > 0 ? $"£{field.Rent:N0} a year" : "lent free by the parish")}  ·  {where}", muted: true, size: 13));
            var uses = new GridContainer { Columns = 2 };
            uses.AddThemeConstantOverride("h_separation", 6);
            uses.AddThemeConstantOverride("v_separation", 6);
            side.AddChild(uses);
            var current = view.Plan.UseOf(field.Id);
            foreach (var use in new[] { FieldUse.Music, FieldUse.Food, FieldUse.Camping, FieldUse.CarPark })
            {
                var text = $"{EstateMap.NameOf(use)}\n{field.RoomFor(use):N0} {EstateMap.UnitOf(use)}";
                var button = current == use ? UiStyle.Button(text, () => { }) : UiStyle.SecondaryButton(text, () => does.Use?.Invoke(use));
                button.Name = $"Use {use}";
                button.CustomMinimumSize = new Vector2(176, 52);
                button.AddThemeFontSizeOverride("font_size", 14);
                uses.AddChild(button);
            }

            var drop = UiStyle.SecondaryButton("Do not rent this field", () => does.Use?.Invoke(null));
            drop.Name = "Give up";
            drop.Disabled = current is null;
            drop.AddThemeFontSizeOverride("font_size", 14);
            side.AddChild(drop);
        }

        side.AddChild(Section("This year's site"));
        var plan = view.Plan;
        var onSale = Math.Min(view.Licence, plan.MusicRoom);
        foreach (var (what, value) in new[]
        {
            ("Fields rented", $"{plan.Fields.Count()}"),
            ("Rent for the year", $"£{plan.Rent:N0}"),
            ("Room in the music fields", $"{plan.MusicRoom:N0}"),
            ("Tickets you can sell", $"{onSale:N0}"),
            ("Room for campers", $"{plan.CampingRoom:N0}"),
            ("Parking spaces", plan.ParkingSpaces > 0 ? $"{plan.ParkingSpaces:N0}, bringing {(int)(plan.ParkingSpaces * FestivalGame.PeopleACar):N0}" : "0"),
        })
        {
            var row = new HBoxContainer();
            var label = UiStyle.Body(what, muted: true, size: 14);
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(label);
            row.AddChild(UiStyle.Body(value, size: 14));
            side.AddChild(row);
        }

        var notes = view.Problems.ToList();
        if (notes.Count == 0 && plan.MusicRoom < view.Licence)
        {
            notes.Add($"Your licence allows {view.Licence:N0} but the music fields hold {plan.MusicRoom:N0}. A bigger music field would let you sell more.");
        }

        var canCome = FestivalGame.Walkers + (int)(plan.ParkingSpaces * FestivalGame.PeopleACar);
        if (notes.Count == 0 && canCome < onSale)
        {
            notes.Add($"About {canCome:N0} can walk or park. Hire shuttle buses on the next screen, or rent a car park, to sell more tickets.");
        }

        if (notes.Count == 0 && plan.Rent > view.Bank)
        {
            notes.Add("The rent is more than there is in the bank.");
        }

        foreach (var note in notes)
        {
            var line = UiStyle.Body(note, size: 13);
            line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            line.CustomMinimumSize = new Vector2(350, 0);
            side.AddChild(line);
        }

        side.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        var legend = new HBoxContainer();
        legend.AddThemeConstantOverride("separation", 10);
        foreach (var use in new[] { FieldUse.Music, FieldUse.Food, FieldUse.Camping, FieldUse.CarPark })
        {
            legend.AddChild(new ColorRect { Color = EstateMap.ColourOf(use), CustomMinimumSize = new Vector2(14, 14), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
            legend.AddChild(UiStyle.Body(use == FieldUse.CarPark ? "Parking" : EstateMap.NameOf(use).Split(' ')[0], muted: true, size: 12));
        }

        side.AddChild(legend);
        var go = UiStyle.Button(view.Changed ? "Rent these fields and book the acts" : "Keep these fields and book the acts", does.Confirm ?? (() => { }));
        go.Name = "Confirm";
        go.Disabled = view.Problems.Count > 0;
        side.AddChild(go);
        return panel;
    }
}
