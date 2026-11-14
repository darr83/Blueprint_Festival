using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The view buttons in the bottom right corner: turn the view and zoom.</summary>
public partial class ViewButtons : PanelContainer
{
    private Button? _crowd;

    /// <summary>Shows the crowd view button as on or off.</summary>
    public void SetCrowdView(bool on)
    {
        if (_crowd is not null)
        {
            _crowd.SetPressedNoSignal(on);
        }
    }

    public void Initialise(CameraRig camera, System.Action<bool>? crowdView = null)
    {
        Theme = UiStyle.Theme;
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 8, radius: 14));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);
        if (crowdView is not null)
        {
            _crowd = UiStyle.SecondaryButton("Crowd view", () => { });
            _crowd.ToggleMode = true;
            _crowd.TooltipText = "Tint the field by how tightly people are packed (C)";
            _crowd.Toggled += on => crowdView(on);
            row.AddChild(_crowd);
        }

        Add(row, "Turn left", "Turn the view a quarter turn (Q)", () => camera.Turn(-1));
        Add(row, "Turn right", "Turn the view a quarter turn (E)", () => camera.Turn(1));
        Add(row, "+", "Zoom in (mouse wheel)", () => camera.ZoomBy(0.8f));
        Add(row, "-", "Zoom out (mouse wheel)", () => camera.ZoomBy(1.25f));
        Add(row, "Whole field", "Show the whole field (Home)", camera.ShowWholeField);
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 16);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Begin;
    }

    private static void Add(Container row, string text, string tip, System.Action pressed)
    {
        var button = UiStyle.SecondaryButton(text, pressed);
        button.TooltipText = tip;
        row.AddChild(button);
    }
}
