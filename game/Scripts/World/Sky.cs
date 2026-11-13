using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The sun, the moon and the colour of the light, driven by the game clock. Midday is bright and
/// warm, dawn and dusk are low and golden, and the night is properly dark but still readable.
/// </summary>
public partial class Sky : Node3D
{
    private static readonly Color DaySun = new("#FFF4E0");
    private static readonly Color GoldenSun = new("#FFB56B");
    private static readonly Color Moon = new("#8FA6D8");
    private static readonly Color DayAmbient = new("#DDE8F2");
    private static readonly Color NightAmbient = new("#5F74A8");
    private static readonly Color DayBackdrop = new("#6FA060");
    private static readonly Color NightBackdrop = new("#1D2B36");

    private DirectionalLight3D _sun = null!;
    private DirectionalLight3D _fill = null!;
    private WorldEnvironment _environment = null!;

    /// <summary>How far into the night it is: 0 by day, 1 at night.</summary>
    public float Night { get; private set; }

    public float SunEnergy => _sun.LightEnergy;

    public override void _Ready()
    {
        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowEnabled = true,
            ShadowBias = 0.04f,
            ShadowNormalBias = 1.2f,
            ShadowBlur = 0.8f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 700f,
        };
        AddChild(_sun);

        // A weak cool light from the other side, without shadows, so faces turned away from the sun
        // are not all one flat shade.
        _fill = new DirectionalLight3D { Name = "Fill", LightColor = new Color("#CFE0FF"), ShadowEnabled = false };
        AddChild(_fill);

        _environment = new WorldEnvironment
        {
            Name = "Environment",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                SsaoEnabled = true,
                SsaoRadius = 1.0f,
                SsaoIntensity = 1.8f,
                AdjustmentEnabled = true,
                AdjustmentSaturation = 1.1f,
                AdjustmentContrast = 1.05f,
            },
        };
        AddChild(_environment);
    }

    /// <summary>Sets the light for the time on the clock, with the sun over the viewer's left shoulder.</summary>
    public void Apply(FestivalGame game, float viewYawDegrees)
    {
        var time = game.Now.TimeOfDay;
        var day = (float)Daylight.At(time);
        var golden = (float)Daylight.GoldenHour(time);
        Night = 1 - day;

        _sun.LightColor = Moon.Lerp(DaySun, day).Lerp(GoldenSun, golden * 0.7f);
        _sun.LightEnergy = Mathf.Lerp(0.24f, 0.95f, day);
        _fill.LightEnergy = Mathf.Lerp(0.03f, 0.14f, day);

        // High at midday, lower and longer shadows towards dawn and dusk. The moon sits fairly high.
        var height = Mathf.Lerp(58f, 34f, golden);
        _sun.RotationDegrees = new Vector3(-height, viewYawDegrees - 72f, 0);
        _fill.RotationDegrees = new Vector3(-35f, viewYawDegrees + 110f, 0);

        var env = _environment.Environment;
        env.AmbientLightColor = NightAmbient.Lerp(DayAmbient, day);
        env.AmbientLightEnergy = Mathf.Lerp(0.3f, 0.55f, day);
        env.BackgroundColor = NightBackdrop.Lerp(DayBackdrop, day);
    }
}
