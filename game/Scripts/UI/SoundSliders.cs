using BlueprintFestival.Audio;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The volume sliders, shown in the pause menu and on the main menu. They are remembered between games.</summary>
public partial class SoundSliders : VBoxContainer
{
    private static StyleBoxFlat Bar(Color colour)
    {
        var bar = new StyleBoxFlat { BgColor = colour, ContentMarginTop = 4, ContentMarginBottom = 4 };
        bar.SetCornerRadiusAll(4);
        return bar;
    }

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 4);
        AddChild(UiStyle.Body("Sound", size: 18));
        foreach (var (bus, label) in SoundBoard.Sliders)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var name = UiStyle.Body(label, muted: true, size: 14);
            name.CustomMinimumSize = new Vector2(170, 0);
            row.AddChild(name);
            var slider = new HSlider
            {
                Name = bus,
                MinValue = 0,
                MaxValue = 1,
                Step = 0.05,
                Value = SoundBoard.Volume(bus),
                CustomMinimumSize = new Vector2(150, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                FocusMode = FocusModeEnum.None,
            };
            slider.AddThemeStyleboxOverride("slider", Bar(new Color(UiStyle.NightInk, 0.15f)));
            slider.AddThemeStyleboxOverride("grabber_area", Bar(UiStyle.StagePink));
            slider.AddThemeStyleboxOverride("grabber_area_highlight", Bar(UiStyle.StagePink));
            slider.ValueChanged += value => SoundBoard.Instance?.SetVolume(bus, (float)value);
            // Let go of a slider and hear how loud that is.
            slider.DragEnded += _ => SoundBoard.Cue(BlueprintFestival.Simulation.Sound.Sounds.Click);
            row.AddChild(slider);
            AddChild(row);
        }
    }
}
