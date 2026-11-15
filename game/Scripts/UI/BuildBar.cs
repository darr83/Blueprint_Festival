using BlueprintFestival.Audio;
using BlueprintFestival.Simulation.Sound;
using System.Linq;
using System.Collections.Generic;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>
/// The build bar along the bottom: Stages, Fences, Gates, Stalls and Take down. Picking a group opens its
/// choices with their prices; picking a choice hands it to the mouse.
/// </summary>
public partial class BuildBar : VBoxContainer
{
    private readonly Dictionary<BuildMode, Button> _tabs = new();
    private readonly Dictionary<string, Button> _choices = new();
    private FestivalGame _game = null!;
    private BuildTool _tool = null!;
    private PanelContainer _choicePanel = null!;
    private HBoxContainer _choiceRow = null!;
    private Label _status = null!;
    private BuildMode _open = BuildMode.None;
    private HBoxContainer _shelfRow = null!;
    private readonly Dictionary<StallGroup, Button> _shelves = new();

    /// <summary>Which shelf of the Stalls tab is showing: food, drink, the shops, or toilets and water.</summary>
    public StallGroup Shelf { get; private set; } = StallGroup.Food;

    private static string ShelfName(StallGroup group) => group switch
    {
        StallGroup.Food => "Food",
        StallGroup.Drink => "Drink",
        StallGroup.Other => "Ponchos, hats and merchandise",
        _ => "Toilets, water and bins",
    };

    /// <summary>The key of the queue lane choice, under Fences.</summary>
    public const string LaneKey = "fenceQueueLane";

    /// <summary>The key of the "no customers" choice, under Fences.</summary>
    public const string KeepOutKey = "fenceNoCustomers";

    /// <summary>The key of a size of big screen, under Stages.</summary>
    public static string ScreenKey(ScreenKind kind) => $"screen{kind}";

    /// <summary>The key of a kind of light, under Lights.</summary>
    public static string LightKey(LightKind kind) => $"light{kind}";

    /// <summary>Which group's choices are showing.</summary>
    public BuildMode Open => _open;

    public string StatusText => _status.Text;

    public int ChoiceCount => _choiceRow.GetChildCount();

    public void Initialise(FestivalGame game, BuildTool tool)
    {
        _game = game;
        _tool = tool;
        Theme = UiStyle.Theme;
        Alignment = AlignmentMode.End;
        AddThemeConstantOverride("separation", 8);
        MouseFilter = MouseFilterEnum.Ignore;

        _status = Outlined("", 15);
        AddChild(_status);

        _choicePanel = new PanelContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _choicePanel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 12, radius: 14));
        var shelves = new VBoxContainer();
        shelves.AddThemeConstantOverride("separation", 8);
        _choicePanel.AddChild(shelves);
        _shelfRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _shelfRow.AddThemeConstantOverride("separation", 6);
        foreach (var group in new[] { StallGroup.Food, StallGroup.Drink, StallGroup.Other, StallGroup.Services })
        {
            var chip = UiStyle.ChipButton(ShelfName(group), () => ShowShelf(group), Sounds.Tab);
            chip.ToggleMode = true;
            _shelfRow.AddChild(chip);
            _shelves[group] = chip;
        }

        shelves.AddChild(_shelfRow);
        _choiceRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _choiceRow.AddThemeConstantOverride("separation", 8);
        shelves.AddChild(_choiceRow);
        AddChild(_choicePanel);

        // A row of round buttons, each with its name underneath, straight on the field.
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        AddChild(row);

        AddTab(row, BuildMode.Stage, "stage", "Stages", "Somewhere for the acts to play, and big screens to show them");
        AddTab(row, BuildMode.Fence, "fence", "Fences", "Keep people in, out, or in a line");
        AddTab(row, BuildMode.Gate, "gate", "Gates", "Where tickets are checked");
        AddTab(row, BuildMode.Stall, "stall", "Stalls", "Food, drink, ponchos, hats and merchandise, toilets and water");
        AddTab(row, BuildMode.Staff, "staff", "Staff", "Ticket staff, security, first aid and cleaners");
        AddTab(row, BuildMode.Ground, "ground", "Ground", "Straw, trackway and paths, to keep feet out of the mud");
        AddTab(row, BuildMode.Light, "light", "Lights", "Path lights, floodlights and fairy lights for after dark");
        AddTab(row, BuildMode.Remove, "remove", "Take down", "Remove something and get the money back");

        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize, 14);
        GrowVertical = GrowDirection.Begin;
    }

    private static Label Outlined(string text, int size)
    {
        var label = UiStyle.Body(text, size: size, bold: true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.12f, 0.08f, 0.85f));
        label.AddThemeConstantOverride("outline_size", 5);
        label.MouseFilter = MouseFilterEnum.Ignore;
        return label;
    }

    private void AddTab(Container row, BuildMode mode, string icon, string text, string tip)
    {
        var button = UiStyle.RoundButton(icon, () => Toggle(mode), 62, floating: true, sound: Sounds.Tab);
        button.ToggleMode = true;
        button.TooltipText = tip;
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(72, 0), MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(button);
        column.AddChild(Outlined(text, 13));
        row.AddChild(column);
        _tabs[mode] = button;
    }

    /// <summary>Opens a group, or closes it if it is already open.</summary>
    public void Toggle(BuildMode mode)
    {
        if (_open == mode)
        {
            Close();
            return;
        }

        _open = mode;
        ClearChoices();
        _shelfRow.Visible = mode == BuildMode.Stall;
        switch (mode)
        {
            case BuildMode.Stage:
                foreach (var s in Catalogue.Stages)
                {
                    AddChoice($"stage{s.Kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep.", () => _tool.PickStage(s.Kind));
                }

                foreach (var b in BigScreens.All)
                {
                    AddChoice(ScreenKey(b.Kind), b.Name, $"£{b.Cost:N0}",
                        $"{b.About}\nShows the nearest stage live. People up to {b.Reach:0} m in front of it get the whole act, however far back from the stage they are.\nWithout one, people more than {BigScreens.ClearMetres:0} m from a stage enjoy the act less and less.\nR turns it.",
                        () => _tool.PickScreen(b.Kind));
                }

                break;
            case BuildMode.Fence:
                foreach (var f in Catalogue.Fences)
                {
                    AddChoice($"fence{f.Kind}", f.Name, $"£{f.CostPerMetre:N0} a metre", f.About, () => _tool.PickFence(f.Kind));
                }

                AddChoice(LaneKey, "Queue lane", $"£{SiteLayout.LaneCostPerMetre:N0} a metre",
                    "Drag along the ground where people should queue for a gate, a stall or the toilets. The barriers go up either side by themselves.\nStart within 3 m of the front. Drag again from its end to turn a corner.", () => _tool.PickLane());
                AddChoice(KeepOutKey, "No customers", "Free",
                    "Mark ground customers must keep off, such as behind the stalls. Staff still go there.\nDrag to mark it; start on marked ground to clear it. It only shows while you are marking.", () => _tool.PickKeepOut());

                break;
            case BuildMode.Gate:
                foreach (var g in Catalogue.Gates)
                {
                    AddChoice($"gate{g.Kind}", g.Name, $"£{g.Cost:N0}", $"{g.About}\n{g.Width / 2f:0.#} m wide. Checks one person every {g.SecondsPerPerson} seconds in each lane.", () => _tool.PickGate(g.Kind));
                }

                break;
            case BuildMode.Stall:
                AddShelf();
                break;
            case BuildMode.Staff:
            {
                foreach (var s in Catalogue.Staff)
                {
                    AddChoice($"staff{s.Kind}", s.Name, $"£{s.Wage:N0} a day", s.About, () => _tool.PickStaff(s.Kind));
                }

                foreach (var tent in Catalogue.Stalls.Where(s => FestivalGame.HasBeds(s.Kind) || s.Kind == StallKind.Bin))
                {
                    AddChoice($"stall{tent.Kind}", tent.Name, $"£{tent.Cost:N0}", $"{tent.About}\n{tent.Width / 2f:0.#} m wide, {tent.Depth / 2f:0.#} m deep.", () => _tool.PickStall(tent.Kind));
                }

                break;
            }

            case BuildMode.Ground:
                AddChoice($"ground{GroundKind.Straw}", "Straw", $"£{FestivalGame.CostOf(GroundKind.Straw)} a patch",
                    "Scattered on wet ground it soaks up most of the mud. It can go down on the day, once you see where the mud is.\nA patch is 2 m square.", () => _tool.PickGround(GroundKind.Straw));
                AddChoice($"ground{GroundKind.Trackway}", "Trackway", $"£{FestivalGame.CostOf(GroundKind.Trackway)} a patch",
                    "Metal panels. No mud at all, and the grass underneath comes to no harm.\nA patch is 2 m square.", () => _tool.PickGround(GroundKind.Trackway));
                AddChoice($"ground{GroundKind.MownPath}", "Mown path", $"£{FestivalGame.CostOf(GroundKind.MownPath)} per 2 m",
                    "A strip of grass cut short. Shows people the way, but it is still grass: it wears and goes to mud like any other.\nDrag to lay it, 2 m wide.", () => _tool.PickGround(GroundKind.MownPath));
                AddChoice($"ground{GroundKind.BarkChip}", "Bark chip path", $"£{FestivalGame.CostOf(GroundKind.BarkChip)} per 2 m",
                    "Soft underfoot. Soaks up most of the mud and saves the grass.\nDrag to lay it, 2 m wide.", () => _tool.PickGround(GroundKind.BarkChip));
                AddChoice($"ground{GroundKind.Gravel}", "Gravel path", $"£{FestivalGame.CostOf(GroundKind.Gravel)} per 2 m",
                    "Rolled gravel. Hardly any mud and hardly any wear.\nDrag to lay it, 2 m wide.", () => _tool.PickGround(GroundKind.Gravel));
                break;
            case BuildMode.Light:
                foreach (var l in Lighting.All)
                {
                    var fairy = l.Kind == LightKind.FairyLights;
                    AddChoice(LightKey(l.Kind), l.Name, fairy ? $"£{l.Cost:N0} a metre" : $"£{l.Cost:N0}",
                        $"{l.About}\n{(fairy ? "Drag from one place to another; the poles go in by themselves. Start on a pole to carry on from it." : "Click on the grass to put it up.")}\nAfter dark, people are unhappy anywhere that is not lit.",
                        () => _tool.PickLight(l.Kind));
                }

                break;
            case BuildMode.Remove:
                _tool.PickRemove();
                break;
        }

        _choicePanel.Visible = _choiceRow.GetChildCount() > 0;
        if (mode != BuildMode.Remove)
        {
            _tool.Stop();
        }
    }

    private void ClearChoices()
    {
        foreach (var child in _choiceRow.GetChildren())
        {
            _choiceRow.RemoveChild(child);
            child.QueueFree();
        }

        _choices.Clear();
    }

    /// <summary>Fills the Stalls tab with one shelf of stalls.</summary>
    private void AddShelf()
    {
        foreach (var s in Catalogue.StallsIn(Shelf))
        {
            var menu = Catalogue.MenuOf(s.Kind);
            var sells = menu.Count > 0
                ? $"Sells {string.Join(", ", menu.Select(m => $"{m.Name.ToLowerInvariant()} £{m.UsualPrice}"))}.\nOnce it is up, click it to set its prices."
                : s.Kind == StallKind.Bin ? "" : "Free to use.";
            AddChoice($"stall{s.Kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{sells}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep. R turns it.", () => _tool.PickStall(s.Kind));
        }

        foreach (var (group, chip) in _shelves)
        {
            chip.SetPressedNoSignal(group == Shelf);
        }
    }

    /// <summary>Shows another shelf of the Stalls tab.</summary>
    public void ShowShelf(StallGroup group)
    {
        Shelf = group;
        if (_open != BuildMode.Stall)
        {
            return;
        }

        ClearChoices();
        AddShelf();
        _tool.Stop();
    }

    public void Close()
    {
        _open = BuildMode.None;
        _choicePanel.Visible = false;
        _tool.Stop();
    }

    /// <summary>Presses one of the open group's choices, as a click would. The key is like "stageBandstand".</summary>
    public bool Choose(string key)
    {
        if (!_choices.ContainsKey(key) && _open == BuildMode.Stall && key.StartsWith("stall", System.StringComparison.Ordinal)
            && System.Enum.TryParse<StallKind>(key["stall".Length..], out var kind) && Catalogue.GroupOf(kind) != Shelf)
        {
            // It is on another shelf: turn to that one first.
            ShowShelf(Catalogue.GroupOf(kind));
        }

        if (!_choices.TryGetValue(key, out var button))
        {
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>The icon for a thing to build, from its key: toilets get a door, first aid a green cross, and so on.</summary>
    private static (string Icon, Color Colour) IconOf(string key)
    {
        var k = key.ToLowerInvariant();
        var booth = k.StartsWith("stall") || k.StartsWith("staff");
        return booth && (k.Contains("aid") || k.Contains("medical")) ? ("cross", UiStyle.FirstAidGreen)
            : booth && k.Contains("toilet") ? ("door", UiStyle.StagePink)
            : booth && k.Contains("water") ? ("drop", UiStyle.StagePink)
            : k is "stallbar" or "stallcocktails" or "stallslush" ? ("glass", UiStyle.StagePink)
            : k == "stallcoffee" ? ("cup", UiStyle.StagePink)
            : k == "stallbuckethats" ? ("hat", UiStyle.StagePink)
            : k == "stallmerchandise" ? ("bag", UiStyle.StagePink)
            : k == "stallponchos" ? ("drop", UiStyle.StagePink)
            : k is "stallhotdogs" or "stallburgers" or "stallfries" or "stallpizza" or "stallnoodles" or "stallicecream" ? ("food", UiStyle.StagePink)
            : booth && k.Contains("security") ? ("shield", UiStyle.StagePink)
            : k == "stallbin" ? ("bin", UiStyle.StagePink)
            : k == "staffcleaner" ? ("broom", UiStyle.StagePink)
            : booth && k.Contains("ticket") ? ("ticket", UiStyle.StagePink)
            : k.StartsWith("stage") ? ("stage", UiStyle.StagePink)
            : k.StartsWith("fence") ? ("fence", UiStyle.StagePink)
            : k.StartsWith("gate") ? ("gate", UiStyle.StagePink)
            : k.StartsWith("staff") ? ("staff", UiStyle.StagePink)
            : k.StartsWith("light") ? ("light", UiStyle.StagePink)
            : k.StartsWith("screen") ? ("screen", UiStyle.StagePink)
            : k.StartsWith("ground") ? ("ground", UiStyle.StagePink)
            : ("stall", UiStyle.StagePink);
    }

    private void AddChoice(string key, string name, string price, string tip, System.Action pick)
    {
        // A card: the icon, the name, and the price in the poster font.
        var button = UiStyle.Button("", pick);
        button.ThemeTypeVariation = UiStyle.ChoiceCard;
        button.ToggleMode = true;
        button.TooltipText = tip;
        button.CustomMinimumSize = new Vector2(124, 104);
        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 1);
        column.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, LayoutPresetMode.Minsize, 6);
        var (icon, colour) = IconOf(key);
        column.AddChild(UiStyle.IconView(icon, 30, colour));
        var title = UiStyle.Body(name, size: 13, bold: true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.MaxLinesVisible = 2;
        title.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(title);
        var cost = UiStyle.Heading(price, 15);
        cost.HorizontalAlignment = HorizontalAlignment.Center;
        cost.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(cost);
        button.AddChild(column);
        _choiceRow.AddChild(button);
        _choices[key] = button;
    }

    public override void _Process(double delta)
    {
        // The mouse can drop the tool (right-click or Esc): follow it.
        if (_tool.Mode == BuildMode.None && _open == BuildMode.Remove)
        {
            _open = BuildMode.None;
        }

        foreach (var (mode, button) in _tabs)
        {
            button.SetPressedNoSignal(_open == mode);
        }

        var picked = _tool.Mode switch
        {
            BuildMode.Stage => $"stage{_tool.Stage}",
            BuildMode.Fence => $"fence{_tool.Fence}",
            BuildMode.Lane => LaneKey,
            BuildMode.KeepOut => KeepOutKey,
            BuildMode.Gate => $"gate{_tool.Gate}",
            BuildMode.Stall => $"stall{_tool.Stall}",
            BuildMode.Ground => $"ground{_tool.Ground}",
            BuildMode.Light or BuildMode.Fairy => LightKey(_tool.Light),
            BuildMode.Screen => ScreenKey(_tool.Screen),
            _ => "",
        };
        foreach (var (key, button) in _choices)
        {
            button.SetPressedNoSignal(key == picked);
        }

        _status.Text = _tool.Mode == BuildMode.None
            ? (_open == BuildMode.None ? "" : "Pick one to build.")
            : _tool.Status;
        _ = _game;
    }
}
