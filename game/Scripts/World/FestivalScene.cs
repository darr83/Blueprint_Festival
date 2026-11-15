using BlueprintFestival.Audio;
using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
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

    /// <summary>The two stage cameras, out front and at the back of the stage.</summary>
    public StageCam StageCam { get; private set; } = null!;

    public ViewButtons ViewButtons => _viewButtons;

    public Sky Sky { get; private set; } = null!;

    public FieldView Field { get; private set; } = null!;

    public CrowdView Crowd { get; private set; } = null!;

    public TopBar TopBar { get; private set; } = null!;

    public SiteView Site { get; private set; } = null!;

    public BuildTool Build { get; private set; } = null!;

    /// <summary>The path lights, floodlights and fairy lights.</summary>
    public LightsView Lights { get; private set; } = null!;

    /// <summary>The big screens, showing the stages live.</summary>
    public ScreenView Screens { get; private set; } = null!;

    /// <summary>The launch pad, its safe zone and fence, and the fireworks in the sky.</summary>
    public FireworksView Fireworks { get; private set; } = null!;

    /// <summary>The firework mixer.</summary>
    public FireworksPanel Mixer { get; private set; } = null!;

    public ShowView Acts { get; private set; } = null!;

    /// <summary>The acts on the second stage.</summary>
    public ShowView SecondActs { get; private set; } = null!;

    public MoneyPanel Money { get; private set; } = null!;
    public StallPanel StallMenu { get; private set; } = null!;

    public BuildBar BuildBar { get; private set; } = null!;

    public LitterView Litter { get; private set; } = null!;

    public StaffView Staff { get; private set; } = null!;

    public YearScreen Year { get; private set; } = null!;

    public DensityView Density { get; private set; } = null!;

    public WeatherView Weather { get; private set; } = null!;

    public CrowdPanel CrowdSafety { get; private set; } = null!;

    public FestivalSounds Sounds { get; private set; } = null!;

    public CampView Camp { get; private set; } = null!;

    public ParkingView Parking { get; private set; } = null!;

    public MusicPlayer Music { get; private set; } = null!;

    private ViewButtons _viewButtons = null!;
    private Label _hint = null!;

    /// <summary>
    /// Keeps the view buttons and the key hints clear of the build bar: they sit in the bottom
    /// corners, and move up above the bar whenever it is showing.
    /// </summary>
    public void LayOutCorners()
    {
        // A panel in the middle of the screen has the bottom of the screen to itself.
        _viewButtons.Visible = !Year.HasCentrePanel;
        // The key hints step aside where the build buttons would run into them, on a narrow screen.
        _hint.Visible = !Year.HasCentrePanel && (!BuildBar.Visible || _hint.Position.X + _hint.Size.X + 12 < BuildBar.TabsLeft);
        var height = GetViewport().GetVisibleRect().Size.Y;
        // The build buttons sit in the middle and the view buttons in the corner, level with them.
        const float lift = 0;
        _viewButtons.Position = new Vector2(_viewButtons.Position.X, height - _viewButtons.Size.Y - 22 - lift);
        _hint.Position = new Vector2(_hint.Position.X, height - _hint.Size.Y - 22 - lift);
    }


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

        Lights = new LightsView { Name = "Lights" };
        AddChild(Lights);
        Lights.Initialise(Game);

        Screens = new ScreenView { Name = "BigScreens" };
        AddChild(Screens);
        Screens.Initialise(Game);

        Fireworks = new FireworksView { Name = "Fireworks" };
        AddChild(Fireworks);
        Fireworks.Initialise(Game);
        Game.FireworksStarted += OnFireworks;

        Acts = new ShowView { Name = "Show" };
        AddChild(Acts);
        Acts.Initialise(Game);
        SecondActs = new ShowView { Name = "SecondShow" };
        AddChild(SecondActs);
        SecondActs.Initialise(Game, second: true);

        Litter = new LitterView { Name = "Litter" };
        AddChild(Litter);
        Litter.Initialise(Game);

        Music = new MusicPlayer { Name = "Music" };
        AddChild(Music);
        Music.Initialise(Game);

        Camp = new CampView { Name = "Camp" };
        AddChild(Camp);
        Camp.Initialise(Game);

        Parking = new ParkingView { Name = "Parking" };
        AddChild(Parking);
        Parking.Initialise(Game);

        Crowd = new CrowdView { Name = "Crowd" };
        AddChild(Crowd);
        Crowd.Initialise(Game);
        Crowd.Music = Music;
        Acts.Music = Music;
        SecondActs.Music = Music;

        Staff = new StaffView { Name = "Staff" };
        AddChild(Staff);
        Staff.Initialise(Game);

        Weather = new WeatherView { Name = "Weather" };
        AddChild(Weather);
        Weather.Initialise(Game);

        Density = new DensityView { Name = "Density" };
        AddChild(Density);
        Density.Initialise(Game);

        Sounds = new FestivalSounds { Name = "Sounds" };
        AddChild(Sounds);
        Sounds.Initialise(Game);

        Camera = new CameraRig { Name = "CameraRig" };
        AddChild(Camera);
        Camera.Initialise(Game.Field);
        StageCam = new StageCam { Name = "StageCam" };
        AddChild(StageCam);
        StageCam.Initialise(Game, Camera);
        Music.Follow(Camera, StageCam);

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
        _hud.AddChild(TopBar.SkipButton);
        _hud.AddChild(TopBar.GateButton);
        StallMenu = new StallPanel { Name = "StallPanel" };
        _hud.AddChild(StallMenu);
        StallMenu.Initialise(Game, Build);
        Mixer = new FireworksPanel { Name = "FireworksPanel" };
        _hud.AddChild(Mixer);
        Mixer.Initialise(Game, Fireworks, Build);
        Mixer.LookAtPad = LookAtPad;
        BuildBar.OpenMixer = Mixer.Open;
        Money = new MoneyPanel { Name = "MoneyPanel" };
        _hud.AddChild(Money);
        Money.Initialise(Game);
        TopBar.MoneyPressed += ToggleMoney;
        Year = new YearScreen { Name = "YearScreen" };
        _hud.AddChild(Year);
        Year.Initialise(Game, this);
        // The badges along the top stay bright over a sheet of paper in the middle of the screen.
        _hud.MoveChild(TopBar, -1);
        BuildBar.Visible = Game.CanBuild;
        _viewButtons = new ViewButtons { Name = "ViewButtons" };
        _hud.AddChild(_viewButtons);
        _viewButtons.Initialise(Camera, on =>
        {
            _viewFromWarning = false;
            SetCrowdView(on);
        }, StageCam);
        CrowdSafety = new CrowdPanel { Name = "CrowdPanel" };
        _hud.AddChild(CrowdSafety);
        CrowdSafety.Initialise(Game, Density);
        CrowdSafety.ShowMe += (x, y) =>
        {
            _viewFromWarning = false;
            SetCrowdView(true);
            StageCam.Show(null);
            Camera.LookAt((float)x, (float)y, 18f);
        };
        Game.CrushWarning += OnCrushWarning;

        _hint = UiStyle.Body("Q / E turn view    WASD move    Wheel zoom\nSpace pause    C crowd view", muted: true, size: 13);
        _hint.Theme = UiStyle.Theme;
        _hint.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
        _hint.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        _hud.AddChild(_hint);
        _hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 22);
        _hint.GrowVertical = Control.GrowDirection.Begin;

        Sky.Apply(Game, StageCam.Yaw);
    }

    public override void _Process(double delta)
    {
        Game.Advance(delta);
        Sky.Apply(Game, StageCam.Yaw);
        CrowdSafety.RightInset = Money.Visible ? Money.Size.X + 14 : 0;
        if (_viewFromWarning && Density.Showing && !Game.CrushBuilding && Game.CrowdStatus != Crowding.CrushRisk)
        {
            // The knot of people has cleared by itself: put the view back as the player had it.
            _viewFromWarning = false;
            SetCrowdView(false);
        }

        if (Density.Showing && Game.Phase is not (Phase.Day or Phase.Build))
        {
            // There is no crowd to look at once the day is over.
            SetCrowdView(false);
        }

        if (BuildBar.Visible != Game.CanBuild)
        {
            // Nothing is built while planning, tidying or reading the results.
            if (!Game.CanBuild)
            {
                BuildBar.Close();
            }

            BuildBar.Visible = Game.CanBuild;
        }

        Fireworks.ZoneShown = Mixer.Visible || Build.Mode == BuildMode.LaunchPad;
        LayOutCorners();
    }

    /// <summary>Points the camera at the launch pad from far enough back to see the fireworks above it.</summary>
    public void LookAtPad()
    {
        if (Game.Site.LaunchPad is { } pad)
        {
            StageCam.Show(null);
            var middle = SiteView.Middle(pad.Rect);
            Camera.LookAt(middle.X, middle.Z, 60f);
        }
    }

    /// <summary>A show's time has come: slow the clock so it can be watched, and look at it if the player is not busy.</summary>
    private void OnFireworks(Simulation.Show.FireworkShow show, Simulation.Show.FireworkResult result)
    {
        if (result.Outcome == Simulation.Show.FireworkOutcome.CalledOff)
        {
            SoundBoard.Cue(Simulation.Sound.Sounds.Groan);
            return;
        }

        if (Game.Clock.Speed > GameSpeed.Normal)
        {
            Game.Clock.SetSpeed(GameSpeed.Normal);
        }
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
            case Key.Key5:
                Game.Clock.SetSpeed(GameSpeed.Testing16);
                break;
            case Key.Key6:
                Game.Clock.SetSpeed(GameSpeed.Testing32);
                break;
            case Key.N:
                Game.SkipToNextAct();
                break;
            case Key.G when Game.Phase == Phase.Day && Game.GatesShut:
                Game.OpenGates();
                break;
            case Key.C:
                _viewFromWarning = false;
                SetCrowdView(!Density.Showing);
                break;
            case Key.F:
                StageCam.Toggle(StageShot.Front);
                break;
            case Key.B:
                StageCam.Toggle(StageShot.Back);
                break;
            case Key.Tab:
                // On a stage camera, on to the next stage.
                StageCam.NextStage();
                break;
            case Key.Escape:
                if (StageCam.Showing is not null)
                {
                    // Esc leaves a stage camera first; pressed again it opens the menu.
                    StageCam.Show(null);
                }
                else
                {
                    OpenMenu();
                }

                break;
        }
    }

    /// <summary>Switches the crowd view on or off, and keeps its button in step.</summary>
    public void SetCrowdView(bool on)
    {
        Density.Showing = on;
        _viewButtons.SetCrowdView(on);
        CrowdSafety.Refresh();
    }

    /// <summary>Somewhere has become a crush risk: slow the clock to normal and show the player where.</summary>
    private bool _viewFromWarning;

    private void OnCrushWarning(double x, double y)
    {
        if (Game.Clock.Speed > GameSpeed.Normal)
        {
            Game.Clock.SetSpeed(GameSpeed.Normal);
        }

        if (!Density.Showing)
        {
            _viewFromWarning = true;
            SetCrowdView(true);
        }
    }

    public override void _ExitTree()
    {
        Game.CrushWarning -= OnCrushWarning;
        Game.FireworksStarted -= OnFireworks;
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

    /// <summary>Opens or shuts the money panel.</summary>
    public void ToggleMoney()
    {
        Money.Visible = !Money.Visible;
        if (Money.Visible)
        {
            Money.Refresh();
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
