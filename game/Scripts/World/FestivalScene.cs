using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.UI;
using Godot;

namespace BlueprintFestival.World;

/// <summary>The festival itself: the 3D site, the camera, the light, and the bars on top.</summary>
public partial class FestivalScene : Node3D
{
    private GameSpeed _speedBeforePause = GameSpeed.Normal;
    private PauseMenu? _pauseMenu;
    private CanvasLayer _hud = null!;

    public FestivalGame Game { get; private set; } = null!;

    public CameraRig Camera { get; private set; } = null!;

    public Sky Sky { get; private set; } = null!;

    public FieldView Field { get; private set; } = null!;

    public CrowdView Crowd { get; private set; } = null!;

    public TopBar TopBar { get; private set; } = null!;

    public SiteView Site { get; private set; } = null!;

    public BuildTool Build { get; private set; } = null!;

    public BuildBar BuildBar { get; private set; } = null!;

    public override void _Ready()
    {
        // Run straight from the editor (F6) and there is no game yet: start a default one.
        Game = GameManager.Instance.Game ?? new FestivalGame();

        Sky = new Sky { Name = "Sky" };
        AddChild(Sky);

        Field = new FieldView { Name = "Field" };
        AddChild(Field);
        Field.Initialise(Game.Field, Game.Name);

        Site = new SiteView { Name = "Site" };
        AddChild(Site);
        Site.Initialise(Game);

        Crowd = new CrowdView { Name = "Crowd" };
        AddChild(Crowd);
        Crowd.Initialise(Game);

        Camera = new CameraRig { Name = "CameraRig" };
        AddChild(Camera);
        Camera.Initialise(Game.Field);

        Build = new BuildTool { Name = "BuildTool" };
        AddChild(Build);
        Build.Initialise(Game, Camera);

        _hud = new CanvasLayer { Name = "Hud" };
        AddChild(_hud);
        BuildBar = new BuildBar { Name = "BuildBar" };
        _hud.AddChild(BuildBar);
        BuildBar.Initialise(Game, Build);
        TopBar = new TopBar { Name = "TopBar" };
        _hud.AddChild(TopBar);
        TopBar.Initialise(Game, OpenMenu);
        var view = new ViewButtons { Name = "ViewButtons" };
        _hud.AddChild(view);
        view.Initialise(Camera);

        var hint = UiStyle.Body("Q / E turn view    WASD move    Wheel zoom    Space pause    R turn a piece", muted: true, size: 13);
        hint.Theme = UiStyle.Theme;
        hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
        hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        _hud.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 18);
        hint.GrowVertical = Control.GrowDirection.Begin;

        Sky.Apply(Game, Camera.Yaw);
    }

    public override void _Process(double delta)
    {
        Game.Advance(delta);
        Sky.Apply(Game, Camera.Yaw);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.Space:
                TogglePause();
                break;
            case Key.Key1:
                Game.Clock.SetSpeed(GameSpeed.Normal);
                break;
            case Key.Key2:
                Game.Clock.SetSpeed(GameSpeed.Fast);
                break;
            case Key.Key3:
                Game.Clock.SetSpeed(GameSpeed.VeryFast);
                break;
            case Key.Key4:
                Game.Clock.SetSpeed(GameSpeed.Fastest);
                break;
            case Key.Escape:
                OpenMenu();
                break;
        }
    }

    public void TogglePause()
    {
        if (Game.Clock.IsPaused)
        {
            Game.Clock.SetSpeed(_speedBeforePause);
        }
        else
        {
            _speedBeforePause = Game.Clock.Speed;
            Game.Clock.SetSpeed(GameSpeed.Paused);
        }
    }

    public void OpenMenu()
    {
        if (_pauseMenu is not null && IsInstanceValid(_pauseMenu))
        {
            return;
        }

        _pauseMenu = new PauseMenu { Name = "PauseMenu" };
        _hud.AddChild(_pauseMenu);
    }

    /// <summary>Shows or hides the bars and buttons, for clean screenshots.</summary>
    public void SetHudShown(bool shown) => _hud.Visible = shown;
}
