using BlueprintFestival.Core;
using Godot;

namespace BlueprintFestival.UI;

/// <summary>The menu opened with Esc during a game: carry on, save, or go back to the main menu.</summary>
public partial class PauseMenu : Control
{
    private Label _message = null!;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Theme = UiStyle.Theme;
        ProcessMode = ProcessModeEnum.Always;
        GetTree().Paused = true;

        AddChild(new ColorRect { Color = new Color(UiStyle.NightInk, 0.45f), AnchorRight = 1, AnchorBottom = 1 });
        var centre = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(centre);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(340, 0) };
        centre.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        column.AddChild(UiStyle.Heading("Paused"));
        column.AddChild(UiStyle.Button("Carry on", Close));
        column.AddChild(UiStyle.SecondaryButton("Save", () => Save()));
        column.AddChild(UiStyle.SecondaryButton("Save and go to main menu", () =>
        {
            if (Save())
            {
                GameManager.Instance.ReturnToMenu();
            }
        }));
        column.AddChild(UiStyle.SecondaryButton("Main menu without saving", GameManager.Instance.ReturnToMenu));
        _message = UiStyle.Body("", muted: true, size: 14);
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_message);
    }

    private bool Save()
    {
        var error = GameManager.Instance.Save();
        _message.Text = error ?? "Saved.";
        return error is null;
    }

    public void Close()
    {
        GetTree().Paused = false;
        QueueFree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
}
