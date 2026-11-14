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
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 7, radius: 999));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        AddChild(row);
        if (crowdView is not null)
        {
            _crowd = UiStyle.RoundButton("crowd", () => { }, 44);
            _crowd.ToggleMode = true;
            _crowd.TooltipText = "Crowd view: tint the field by how tightly people are packed (C)";
            _crowd.Toggled += on => crowdView(on);
            row.AddChild(_crowd);
        }

        Add(row, "left", "Turn the view a quarter turn left (Q)", () => camera.Turn(-1));
        Add(row, "right", "Turn the view a quarter turn right (E)", () => camera.Turn(1));
        Add(row, "plus", "Zoom in (mouse wheel)", () => camera.ZoomBy(0.8f));
        Add(row, "minus", "Zoom out (mouse wheel)", () => camera.ZoomBy(1.25f));
        Add(row, "whole", "Show the whole field (Home)", camera.ShowWholeField);
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 22);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Begin;
    }

    private static void Add(Container row, string icon, string tip, System.Action pressed)
    {
        var button = UiStyle.RoundButton(icon, pressed, 44);
        button.TooltipText = tip;
        row.AddChild(button);
    }
}
