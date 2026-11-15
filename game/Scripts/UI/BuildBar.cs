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
    private HBoxContainer _widthRow = null!;
    private readonly List<Button> _widths = new();
    private readonly Dictionary<StallGroup, Button> _shelves = new();

    /// <summary>Which shelf of the Stalls tab is showing: food, drink, the shops, or toilets and water.</summary>
    public StallGroup Shelf { get; private set; } = StallGroup.Food;

    private static string ShelfName(StallGroup group) => group switch
    {
        StallGroup.Food => "Food",
        StallGroup.Drink => "Drink",
        StallGroup.Other => "Ponchos, hats and merchandise",
        StallGroup.Vip => "VIP",
        StallGroup.Fair => "Fairground",
        _ => "Toilets, water and bins",
    };

    /// <summary>The key of the queue lane choice, under Fences.</summary>
    public const string LaneKey = "fenceQueueLane";

    /// <summary>The key of the "no customers" choice, under Fences.</summary>
    public const string KeepOutKey = "fenceNoCustomers";

    /// <summary>The key of the VIP ground choice, under Fences.</summary>
    public const string VipGroundKey = "fenceVipGround";

    /// <summary>The key of a kind of crowd area, under Areas.</summary>
    public static string AreaKey(AreaKind kind) => $"area{kind}";

    private static string AreaAbout(AreaKind kind) => (kind switch
    {
        AreaKind.FrontPen => "A pen at the front of a stage. Security on its gaps count people in and close them when it is full, so nobody is pushed in from behind.",
        AreaKind.Vip => "A pen for VIP guests only, with a gold lanyard to get in. Put it near the stage.",
        _ => "A standing area with its own limit, anywhere in front of a stage.",
    }) + $"\nDrag to draw it. It lets in {CrowdAreas.ComfortablePerMetre:0} people a square metre to start with; click inside to change that. The barrier goes round by itself, with gaps on the side away from the stage. Post one security by each gap.";

    /// <summary>The key of a size of big screen, under Stages.</summary>
    public static string ScreenKey(ScreenKind kind) => $"screen{kind}";

    /// <summary>The key of a size of launch pad, under Fireworks.</summary>
    public static string PadKey(LaunchPadKind kind) => $"pad{kind}";

    /// <summary>The key of the card that opens the firework mixer, under Fireworks.</summary>
    public const string MixerKey = "fireworksMixer";

    /// <summary>Opens the firework mixer. Set by the scene.</summary>
    public System.Action? OpenMixer { get; set; }

    /// <summary>The key of a decoration, on the Decorations shelf under Decor.</summary>
    public static string DecorKey(DecorKind kind) => $"decor{kind}";

    /// <summary>The key of bunting, on the Decorations shelf under Decor.</summary>
    public const string BuntingKey = "decorBunting";

    /// <summary>True when the Decor group is showing its decorations, false for its signs.</summary>
    public bool DecorShelf { get; private set; }

    private HBoxContainer _decorRow = null!;
    private readonly Button[] _decorChips = new Button[2];

    /// <summary>Turns the Decor group to its signs or to its decorations.</summary>
    public void ShowDecorShelf(bool decorations)
    {
        DecorShelf = decorations;
        if (_open != BuildMode.Sign)
        {
            return;
        }

        ClearChoices();
        AddDecorShelf();
        _tool.Stop();
    }

    private void AddDecorShelf()
    {
        if (DecorShelf)
        {
            AddChoice(BuntingKey, "Bunting", $"£{Decor.BuntingPerMetre} a metre", "Little flags on a line between two poles, high enough to walk under.\nDrag to string it, up to 16 m. Start on a pole to carry on.", () => _tool.PickBunting());
            foreach (var d in Decor.Everything)
            {
                AddChoice(DecorKey(d.Kind), d.Name, $"£{d.Cost:N0}", $"{d.About}\nCheers up people within {d.Reach:0} m. R turns it.", () => _tool.PickDecor(d.Kind));
            }
        }
        else
        {
            foreach (var s in Signs.Everything)
            {
                AddChoice(SignKey(s.Kind), s.Name, $"£{s.Cost:N0}", $"{s.About}\n{s.Width / 2f:0.#} m wide. R turns it.", () => _tool.PickSign(s.Kind));
            }
        }

        _decorChips[0].SetPressedNoSignal(!DecorShelf);
        _decorChips[1].SetPressedNoSignal(DecorShelf);
    }

    /// <summary>The key of a sign, under Signs.</summary>
    public static string SignKey(SignKind kind) => $"sign{kind}";

    /// <summary>The key of a seat, a sink or a sail, under Comforts.</summary>
    public static string ComfortKey(ComfortKind kind) => $"comfort{kind}";

    /// <summary>The key of a kind of light, under Lights.</summary>
    public static string LightKey(LightKind kind) => $"light{kind}";

    /// <summary>Which group's choices are showing.</summary>
    public BuildMode Open => _open;

    public string StatusText => _status.Text;

    public int ChoiceCount => _choiceRow.GetChildCount();

    private HBoxContainer _tabRow = null!;

    /// <summary>Where the row of round build buttons starts on the screen, so the key hints can keep clear of it.</summary>
    public float TabsLeft => _tabRow.GlobalPosition.X;

    /// <summary>
    /// The top edge of the bar on screen, with whatever tray is open. Worked out from what the bar
    /// needs as well as where it is, so it is right on the same frame a tray opens.
    /// </summary>
    public float Top => Mathf.Min(GlobalPosition.Y, GetViewportRect().Size.Y - 14 - GetCombinedMinimumSize().Y);

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
        foreach (var group in new[] { StallGroup.Food, StallGroup.Drink, StallGroup.Other, StallGroup.Fair, StallGroup.Services, StallGroup.Vip })
        {
            var chip = UiStyle.ChipButton(ShelfName(group), () => ShowShelf(group), Sounds.Tab);
            chip.ToggleMode = true;
            _shelfRow.AddChild(chip);
            _shelves[group] = chip;
        }

        shelves.AddChild(_shelfRow);
        _decorRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _decorRow.AddThemeConstantOverride("separation", 6);
        for (var i = 0; i < 2; i++)
        {
            var decorations = i == 1;
            var chip = UiStyle.ChipButton(decorations ? "Decorations" : "Signs", () => ShowDecorShelf(decorations), Sounds.Tab);
            chip.ToggleMode = true;
            _decorRow.AddChild(chip);
            _decorChips[i] = chip;
        }

        shelves.AddChild(_decorRow);
        _widthRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _widthRow.AddThemeConstantOverride("separation", 6);
        for (var patches = 1; patches <= FestivalGame.WidestPath; patches++)
        {
            var wide = patches;
            var chip = UiStyle.ChipButton($"{patches * 2} m wide", () => SetPathWidth(wide), Sounds.Tab);
            chip.ToggleMode = true;
            chip.TooltipText = "How wide the next path you drag is laid. A wider path costs more and carries more people.";
            _widthRow.AddChild(chip);
            _widths.Add(chip);
        }

        shelves.AddChild(_widthRow);
        _choiceRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _choiceRow.AddThemeConstantOverride("separation", 8);
        shelves.AddChild(_choiceRow);
        AddChild(_choicePanel);

        // A row of round buttons, each with its name underneath, straight on the field.
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        _tabRow = row;
        AddChild(row);

        AddTab(row, BuildMode.Stage, "stage", "Stages", "Somewhere for the acts to play, and big screens to show them");
        AddTab(row, BuildMode.Fence, "fence", "Fences", "Keep people in, out, or in a line");
        AddTab(row, BuildMode.Gate, "gate", "Gates", "Where tickets are checked");
        AddTab(row, BuildMode.Stall, "stall", "Stalls", "Food, drink, ponchos, hats and merchandise, toilets and water, and the VIP lounge and platform");
        AddTab(row, BuildMode.Staff, "staff", "Staff", "Ticket staff, security, first aid, cleaners and entertainers");
        AddTab(row, BuildMode.Ground, "ground", "Ground", "Straw, trackway, pallets, mats, sand and artificial grass");
        AddTab(row, BuildMode.Path, "path", "Paths", "Mown, bark, gravel and boardwalk paths, up to 6 m wide");
        AddTab(row, BuildMode.Comfort, "bench", "Comforts", "Seats, tables, hand washing and shade");
        AddTab(row, BuildMode.Area, "area", "Areas", "Pens and areas in front of the stages, each with its own limit on how many people get in");
        AddTab(row, BuildMode.Sign, "sign", "Decor", "Signs with your own words on, and decorations: bunting, flags, flowers, a fire pit and giant letters");
        AddTab(row, BuildMode.Light, "light", "Lights", "Path lights, floodlights and fairy lights for after dark");
        AddTab(row, BuildMode.Fireworks, "firework", "Fireworks", "A launch pad, and firework shows to plan on the mixer");
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
        var button = UiStyle.RoundButton(icon, () => Toggle(mode), 54, floating: true, sound: Sounds.Tab);
        button.ToggleMode = true;
        button.TooltipText = tip;
        button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(62, 0), MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(button);
        column.AddChild(Outlined(text, 12));
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
        _decorRow.Visible = mode == BuildMode.Sign;
        _widthRow.Visible = mode == BuildMode.Path;
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
                AddChoice(VipGroundKey, "VIP ground", "Free",
                    "Mark ground for VIP guests only. Everyone else walks round it; staff still go there. Seats put on it are VIP seats.\nDrag to mark it; start on VIP ground to clear it. It shows in gold only while you are marking.", () => _tool.PickVipGround());

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
                AddFloor(GroundKind.Pallets, "Old pallets laid side by side. Cheap and nearly dry, but slow to walk over. People follow them like a path.");
                AddFloor(GroundKind.RubberMats, "Black rubber mats. Hardly any mud and they never wear: best in front of bars and toilets. People follow them like a path.");
                AddFloor(GroundKind.Sand, "A bed of sand: a beach in a field. On a dry day people sit on it.");
                AddFloor(GroundKind.ArtificialGrass, "A lawn that never turns to mud. On a dry day people sit on it.");
                break;
            case BuildMode.Path:
                AddChoice($"ground{GroundKind.MownPath}", "Mown path", $"£{FestivalGame.CostOf(GroundKind.MownPath)} per 2 m",
                    "A strip of grass cut short. Shows people the way, but it is still grass: it wears and goes to mud like any other.\nDrag to lay it.", () => _tool.PickGround(GroundKind.MownPath));
                AddChoice($"ground{GroundKind.BarkChip}", "Bark chip path", $"£{FestivalGame.CostOf(GroundKind.BarkChip)} per 2 m",
                    "Soft underfoot. Soaks up most of the mud and saves the grass.\nDrag to lay it.", () => _tool.PickGround(GroundKind.BarkChip));
                AddChoice($"ground{GroundKind.Gravel}", "Gravel path", $"£{FestivalGame.CostOf(GroundKind.Gravel)} per 2 m",
                    "Rolled gravel. Hardly any mud and hardly any wear.\nDrag to lay it.", () => _tool.PickGround(GroundKind.Gravel));
                AddChoice($"ground{GroundKind.Boardwalk}", "Boardwalk", $"£{FestivalGame.CostOf(GroundKind.Boardwalk)} per 2 m",
                    "Timber planks. No mud and no wear, and smart with it.\nDrag to lay it.", () => _tool.PickGround(GroundKind.Boardwalk));
                break;
            case BuildMode.Sign:
                AddDecorShelf();
                break;
            case BuildMode.Comfort:
                foreach (var c in Comforts.Everything)
                {
                    var seats = c.Seats.Length > 0 ? $"Seats {c.Seats.Length}. " : "";
                    AddChoice(ComfortKey(c.Kind), c.Name, $"£{c.Cost:N0}", $"{c.About}\n{seats}{c.Width / 2f:0.#} m wide, {c.Depth / 2f:0.#} m deep. R turns it.", () => _tool.PickComfort(c.Kind));
                }

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
            case BuildMode.Fireworks:
                foreach (var p in LaunchPads.All)
                {
                    var side = (p.Size + (2 * p.SafeTiles)) / 2;
                    AddChoice(PadKey(p.Kind), p.Name, $"£{p.Cost:N0}",
                        $"{p.About}\nThe safe zone is {side} m square and must be clear of everything. Fence it off from the mixer (£{LaunchPads.FenceCostFor(p.Kind):N0}), or people wander in.",
                        () => _tool.PickLaunchPad(p.Kind));
                }

                AddChoice(MixerKey, "Plan the shows", "Mixer",
                    "Up to four 30-second shows at times you choose. Drag fireworks onto the timeline.\nA good show in the dark cheers everyone up.",
                    () =>
                    {
                        _tool.Stop();
                        OpenMixer?.Invoke();
                    });
                break;
            case BuildMode.Area:
                foreach (var kind in new[] { AreaKind.FrontPen, AreaKind.Standing, AreaKind.Vip })
                {
                    AddChoice(AreaKey(kind), CrowdAreas.Name(kind), $"£{Catalogue.Of(FenceKind.CrowdBarrier).CostPerMetre} a metre", AreaAbout(kind), () => _tool.PickArea(kind));
                }

                AddChoice(KeepOutKey, "Keep clear", "Free",
                    "Ground nobody stands on, such as a walkway round a pen for security. Staff still go there.\nDrag to mark it; start on marked ground to clear it. It only shows while you are marking.", () => _tool.PickKeepOut());
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
            var sells = Rides.IsFair(s.Kind)
                ? $"{menu[0].Name} costs £{menu[0].UsualPrice} to start with.\nOnce it is up, click it to set the price. It cannot go in a camping field."
                : menu.Count > 0
                ? $"Sells {string.Join(", ", menu.Select(m => $"{m.Name.ToLowerInvariant()} £{m.UsualPrice}"))}.\nOnce it is up, click it to set its prices."
                : s.Kind == StallKind.Bin ? "" : "Free to use.";
            AddChoice($"stall{s.Kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{sells}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep. R turns it.", () => _tool.PickStall(s.Kind));
        }

        foreach (var (group, chip) in _shelves)
        {
            chip.SetPressedNoSignal(group == Shelf);
        }
    }

    private void AddFloor(GroundKind kind, string about) =>
        AddChoice($"ground{kind}", FestivalGame.NameOf(kind), $"£{FestivalGame.CostOf(kind)} a patch", $"{about}\nA patch is 2 m square. Click, or drag to cover a stretch.", () => _tool.PickGround(kind));

    /// <summary>Sets how wide the next dragged path is laid, in patches of 2 m.</summary>
    public void SetPathWidth(int patches)
    {
        _tool.SetPathWidth(patches);
        if (_tool.Mode == BuildMode.Ground)
        {
            // Pick the path up again so the hint under the pointer says the new width.
            _tool.PickGround(_tool.Ground);
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

        if (!_choices.ContainsKey(key) && _open == BuildMode.Sign && (key.StartsWith("decor", System.StringComparison.Ordinal) != DecorShelf))
        {
            // It is on the other shelf of the Decor group.
            ShowDecorShelf(!DecorShelf);
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
            : k == "staffguitarist" ? ("guitar", UiStyle.StagePink)
            : k == "staffpianist" ? ("piano", UiStyle.StagePink)
            : k == "staffdrummer" ? ("drum", UiStyle.StagePink)
            : k == "staffdj" ? ("decks", UiStyle.StagePink)
            : booth && k.Contains("ticket") ? ("ticket", UiStyle.StagePink)
            : k.StartsWith("stage") ? ("stage", UiStyle.StagePink)
            : k.StartsWith("fence") ? ("fence", UiStyle.StagePink)
            : k.StartsWith("area") ? ("area", UiStyle.StagePink)
            : k.StartsWith("gate") ? ("gate", UiStyle.StagePink)
            : k.StartsWith("staff") ? ("staff", UiStyle.StagePink)
            : k.StartsWith("light") ? ("light", UiStyle.StagePink)
            : k.StartsWith("screen") ? ("screen", UiStyle.StagePink)
            : k.StartsWith("pad") || k.StartsWith("fireworks") ? ("firework", UiStyle.StagePink)
            : k is "groundmownpath" or "groundbarkchip" or "groundgravel" or "groundboardwalk" ? ("path", UiStyle.StagePink)
            : k.StartsWith("sign") ? ("sign", UiStyle.StagePink)
            : k.StartsWith("decor") ? ("flag", UiStyle.StagePink)
            : k.StartsWith("ground") ? ("ground", UiStyle.StagePink)
            : k is "comforthandgel" or "comforthandwash" ? ("drop", UiStyle.StagePink)
            : k.StartsWith("comfort") ? ("bench", UiStyle.StagePink)
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
            BuildMode.VipGround => VipGroundKey,
            BuildMode.Area => AreaKey(_tool.Area),
            BuildMode.Gate => $"gate{_tool.Gate}",
            BuildMode.Stall => $"stall{_tool.Stall}",
            BuildMode.Ground => $"ground{_tool.Ground}",
            BuildMode.Light or BuildMode.Fairy => LightKey(_tool.Light),
            BuildMode.Screen => ScreenKey(_tool.Screen),
            BuildMode.LaunchPad => PadKey(_tool.Pad),
            BuildMode.Comfort => ComfortKey(_tool.Comfort),
            BuildMode.Sign => SignKey(_tool.Sign),
            BuildMode.Decor => DecorKey(_tool.Decor),
            BuildMode.Bunting => BuntingKey,
            _ => "",
        };
        foreach (var (key, button) in _choices)
        {
            button.SetPressedNoSignal(key == picked);
        }

        for (var i = 0; i < _widths.Count; i++)
        {
            _widths[i].SetPressedNoSignal(_tool.PathWidth == i + 1);
        }

        _status.Text = _tool.Mode == BuildMode.None
            ? (_open == BuildMode.None ? "" : "Pick one to build.")
            : _tool.Status;
        _ = _game;
    }
}
