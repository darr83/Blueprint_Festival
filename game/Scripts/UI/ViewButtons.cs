using BlueprintFestival.World;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The view buttons in the bottom right corner: turn the view and zoom, the crowd view and the two stage cameras.</summary>
public partial class ViewButtons : PanelContainer
{
    private Button? _crowd;
    private Button? _front;
    private Button? _back;

    /// <summary>The stage camera buttons, for the launch test: out front at the band, and at the back looking at the crowd.</summary>
    public Button? FrontCamButton => _front;

    public Button? BackCamButton => _back;

    /// <summary>Shows the crowd view button as on or off.</summary>
    public void SetCrowdView(bool on)
    {
        if (_crowd is not null)
        {
            _crowd.SetPressedNoSignal(on);
        }
    }

    public void Initialise(CameraRig camera, System.Action<bool>? crowdView = null, StageCam? stageCam = null)
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

        if (stageCam is not null)
        {
            _front = CamButton(row, "camband", "Stage cam out front: watch the band (F). Press again or Esc to go back", () => stageCam.Toggle(StageShot.Front));
            _back = CamButton(row, "camcrowd", "Stage cam at the back of the stage: look out at the crowd (B). Press again or Esc to go back", () => stageCam.Toggle(StageShot.Back));
            stageCam.Changed += () =>
            {
                _front.SetPressedNoSignal(stageCam.Showing == StageShot.Front);
                _back.SetPressedNoSignal(stageCam.Showing == StageShot.Back);
            };
        }

        // Turning or zooming means looking at the site again, so it leaves a stage camera.
        System.Action site = () => stageCam?.Show(null);
        Add(row, "left", "Turn the view a quarter turn left (Q)", () => { site(); camera.Turn(-1); });
        Add(row, "right", "Turn the view a quarter turn right (E)", () => { site(); camera.Turn(1); });
        Add(row, "plus", "Zoom in (mouse wheel)", () => { site(); camera.ZoomBy(0.8f); });
        Add(row, "minus", "Zoom out (mouse wheel)", () => { site(); camera.ZoomBy(1.25f); });
        Add(row, "whole", "Show the whole field (Home)", () => { site(); camera.ShowWholeField(); });
        SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, 22);
        GrowHorizontal = GrowDirection.Begin;
        GrowVertical = GrowDirection.Begin;
    }

    private static Button CamButton(Container row, string icon, string tip, System.Func<bool> pressed)
    {
        var button = UiStyle.RoundButton(icon, () => { }, 44);
        button.ToggleMode = true;
        button.TooltipText = tip;
        button.Toggled += _ =>
        {
            if (!pressed())
            {
                // No stage to film yet: the button stays up.
                button.SetPressedNoSignal(false);
            }
        };
        row.AddChild(button);
        return button;
    }

    private static void Add(Container row, string icon, string tip, System.Action pressed)
    {
        var button = UiStyle.RoundButton(icon, pressed, 44);
        button.TooltipText = tip;
        row.AddChild(button);
    }
}
