using BlueprintFestival.World;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The view buttons in the bottom right corner: turn the view and zoom.</summary>
public partial class ViewButtons : PanelContainer
{
    public void Initialise(CameraRig camera)
    {
        Theme = UiStyle.Theme;
        AddThemeStyleboxOverride("panel", UiStyle.Card(margin: 8, radius: 14));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        AddChild(row);
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
