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

        _status = UiStyle.Body("", size: 15);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.AddThemeColorOverride("font_color", Colors.White);
        _status.AddThemeColorOverride("font_outline_color", new Color(0.11f, 0.17f, 0.21f, 0.9f));
        _status.AddThemeConstantOverride("outline_size", 6);
        _status.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_status);

        _choicePanel = new PanelContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        _choicePanel.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 10, radius: 14));
        _choiceRow = new HBoxContainer();
        _choiceRow.AddThemeConstantOverride("separation", 8);
        _choicePanel.AddChild(_choiceRow);
        AddChild(_choicePanel);

        var bar = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        bar.AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 8, radius: 14));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        bar.AddChild(row);
        AddChild(bar);

        var title = UiStyle.Body("Build", muted: true, size: 14);
        row.AddChild(new Control { CustomMinimumSize = new Vector2(4, 0) });
        row.AddChild(title);
        AddTab(row, BuildMode.Stage, "Stages", "Somewhere for the acts to play");
        AddTab(row, BuildMode.Fence, "Fences", "Keep people in, out, or in a line");
        AddTab(row, BuildMode.Gate, "Gates", "Where tickets are checked");
        AddTab(row, BuildMode.Stall, "Stalls", "Food, drink, toilets and water");
        AddTab(row, BuildMode.Staff, "Staff", "Ticket staff, security and first aid");
        AddTab(row, BuildMode.Ground, "Ground", "Straw and trackway, to keep feet out of the mud");
        AddTab(row, BuildMode.Remove, "Take down", "Remove something and get the money back");

        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide, LayoutPresetMode.Minsize, 14);
        GrowVertical = GrowDirection.Begin;
    }

    private void AddTab(Container row, BuildMode mode, string text, string tip)
    {
        var button = UiStyle.SecondaryButton(text, () => Toggle(mode));
        button.ToggleMode = true;
        button.TooltipText = tip;
        row.AddChild(button);
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
        foreach (var child in _choiceRow.GetChildren())
        {
            _choiceRow.RemoveChild(child);
            child.QueueFree();
        }

        _choices.Clear();
        switch (mode)
        {
            case BuildMode.Stage:
                foreach (var s in Catalogue.Stages)
                {
                    AddChoice($"stage{s.Kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep.", () => _tool.PickStage(s.Kind));
                }

                break;
            case BuildMode.Fence:
                foreach (var f in Catalogue.Fences)
                {
                    AddChoice($"fence{f.Kind}", f.Name, $"£{f.CostPerMetre:N0} a metre", f.About, () => _tool.PickFence(f.Kind));
                }

                break;
            case BuildMode.Gate:
                foreach (var g in Catalogue.Gates)
                {
                    AddChoice($"gate{g.Kind}", g.Name, $"£{g.Cost:N0}", $"{g.About}\n{g.Width / 2f:0.#} m wide. Checks one person every {g.SecondsPerPerson} seconds in each lane.", () => _tool.PickGate(g.Kind));
                }

                break;
            case BuildMode.Stall:
                foreach (var s in Catalogue.Stalls.Where(s => s.Serves is not null))
                {
                    var takes = s.Price > 0 ? $"People pay £{s.Price} each." : "Free to use.";
                    AddChoice($"stall{s.Kind}", s.Name, $"£{s.Cost:N0}", $"{s.About}\n{takes} {s.Width / 2f:0.#} m wide, {s.Depth / 2f:0.#} m deep.", () => _tool.PickStall(s.Kind));
                }

                break;
            case BuildMode.Staff:
            {
                foreach (var s in Catalogue.Staff)
                {
                    AddChoice($"staff{s.Kind}", s.Name, $"£{s.Wage:N0} a day", s.About, () => _tool.PickStaff(s.Kind));
                }

                foreach (var tent in Catalogue.Stalls.Where(s => FestivalGame.HasBeds(s.Kind)))
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

    public void Close()
    {
        _open = BuildMode.None;
        _choicePanel.Visible = false;
        _tool.Stop();
    }

    /// <summary>Presses one of the open group's choices, as a click would. The key is like "stageBandstand".</summary>
    public bool Choose(string key)
    {
        if (!_choices.TryGetValue(key, out var button))
        {
            return false;
        }

        button.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    private void AddChoice(string key, string name, string price, string tip, System.Action pick)
    {
        var button = UiStyle.SecondaryButton($"{name}\n{price}", pick);
        button.ToggleMode = true;
        button.TooltipText = tip;
        button.CustomMinimumSize = new Vector2(150, 0);
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
            BuildMode.Gate => $"gate{_tool.Gate}",
            BuildMode.Stall => $"stall{_tool.Stall}",
            BuildMode.Ground => $"ground{_tool.Ground}",
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
