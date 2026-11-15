using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The first screen: name your festival and start, or carry on with the saved one.</summary>
public partial class MainMenu : Control
{
    private LineEdit _name = null!;
    private Label _message = null!;

    /// <summary>The name box, so the launch test can check the menu is really there.</summary>
    public LineEdit NameBox => _name;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiStyle.Theme;

        AddChild(new ColorRect { Color = UiStyle.Surface, AnchorRight = 1, AnchorBottom = 1 });
        AddChild(new Bunting { AnchorRight = 1, OffsetBottom = 34, FlagWidth = 52 });

        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(centre);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        column.AddThemeConstantOverride("separation", 12);
        centre.AddChild(column);

        var kicker = UiStyle.Caps("Plan it  ·  build it  ·  run it  ·  tidy up", pink: true, size: 13);
        kicker.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(kicker);
        var title = UiStyle.Heading("Blueprint Festival", 64);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);
        column.AddChild(UiStyle.Rule());
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 14) });

        column.AddChild(UiStyle.Caps("Name your festival"));
        _name = new LineEdit { PlaceholderText = FestivalGame.DefaultName, MaxLength = FestivalGame.MaxNameLength };
        _name.TextSubmitted += _ => StartNew();
        column.AddChild(_name);

        column.AddChild(UiStyle.Button("New festival", StartNew));
        var resume = UiStyle.SecondaryButton("Continue", Continue);
        resume.Disabled = !GameManager.Instance.HasSave();
        resume.TooltipText = resume.Disabled ? "No saved festival yet" : "";
        column.AddChild(resume);
        var sliders = new SoundSliders { Name = "SoundSliders", Visible = false };
        column.AddChild(UiStyle.SecondaryButton("Sound", () => sliders.Visible = !sliders.Visible));
        column.AddChild(sliders);
        column.AddChild(BlueprintFestival.Core.ScreenMode.MakeButton());
        column.AddChild(BlueprintFestival.Core.Graphics.MakeButton());
        column.AddChild(UiStyle.SecondaryButton("Quit", () => GetTree().Quit()));

        _message = UiStyle.Body("", muted: true, size: 14);
        _message.HorizontalAlignment = HorizontalAlignment.Center;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_message);

        var version = UiStyle.Body("Step 17: the festival programme look", muted: true, size: 13);
        AddChild(version);
        version.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft, LayoutPresetMode.Minsize, 18);
        version.GrowVertical = GrowDirection.Begin;
    }

    private void StartNew() => GameManager.Instance.StartNewFestival(_name.Text);

    private void Continue()
    {
        if (GameManager.Instance.Load() is { } error)
        {
            _message.Text = error;
        }
    }
}
