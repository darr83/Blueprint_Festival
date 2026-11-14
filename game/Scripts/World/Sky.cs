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

    private static readonly Color CloudLight = new("#C9D1DA");
    private static readonly Color HotSun = new("#FFEFC4");
    private float _cloud = -1;

    /// <summary>How overcast the sky is drawn: 0 clear, 1 under heavy rain cloud. It eases from one to the other.</summary>
    public float Overcast => Mathf.Max(0, _cloud);

    /// <summary>How overcast the sky ought to be for the weather right now.</summary>
    public static float CloudFor(FestivalGame game)
    {
        var rain = (float)game.RainNow;
        return game.Weather.Kind switch
        {
            WeatherKind.Rain => rain > 0.05f ? 0.75f + (0.25f * rain) : 0.55f,
            WeatherKind.Showers => rain > 0.05f ? 0.6f + (0.3f * rain) : 0.2f,
            _ => 0,
        };
    }

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
    public void Apply(FestivalGame game, float viewYawDegrees, bool settle = false)
    {
        if (settle)
        {
            _cloud = -1;
        }

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

        // Cloud takes the strength out of the sun and the colour out of everything; a hot day is a touch brighter and yellower.
        var target = CloudFor(game);
        _cloud = _cloud < 0 ? target : Mathf.MoveToward(_cloud, target, (float)GetProcessDeltaTime() * 0.25f);
        var cloud = _cloud * day;
        _sun.LightColor = _sun.LightColor.Lerp(CloudLight, cloud * 0.8f);
        _sun.LightEnergy *= 1 - (0.6f * cloud);
        _sun.ShadowOpacity = 1 - (0.65f * cloud);
        env.AmbientLightColor = env.AmbientLightColor.Lerp(CloudLight, cloud * 0.6f);
        env.AmbientLightEnergy += 0.16f * cloud;
        env.AdjustmentSaturation = 1.1f - (0.3f * cloud);
        env.BackgroundColor = env.BackgroundColor.Lerp(new Color("#5E7D63"), cloud * 0.7f);
        if (game.Weather.Kind == WeatherKind.Hot)
        {
            _sun.LightColor = _sun.LightColor.Lerp(HotSun, day * 0.6f);
            _sun.LightEnergy *= 1 + (0.1f * day);
        }
    }
}
