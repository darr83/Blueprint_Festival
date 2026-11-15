using BlueprintFestival.Core;
using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The sun, the moon and the colour of the light, driven by the game clock. The sun crosses the sky
/// through the day, so shadows are long in the morning, short at lunchtime and long and golden in
/// the evening; then a blue dusk, and a dark night under the moon for the lamps to shine in.
/// </summary>
public partial class Sky : Node3D
{
    private static readonly Color DaySun = new("#FFF6E6");
    private static readonly Color GoldenSun = new("#FFC27A");
    private static readonly Color LowSun = new("#FF8E4F");
    private static readonly Color Moon = new("#9FB6F2");
    private static readonly Color DayAmbient = new("#AFC8EE");
    private static readonly Color GoldenAmbient = new("#E2B7C2");
    private static readonly Color DuskAmbient = new("#7C74C4");
    private static readonly Color NightAmbient = new("#4A5EA8");
    private static readonly Color DayBackdrop = new("#6FA060");
    private static readonly Color NightBackdrop = new("#121B2A");
    private static readonly Color CloudLight = new("#C9D1DA");
    private static readonly Color HotSun = new("#FFEFC4");

    /// <summary>How far the site camera stands back from the point it looks at, in metres.</summary>
    public const float CameraDistance = 300f;

    private DirectionalLight3D _sun = null!;
    private DirectionalLight3D _fill = null!;
    private WorldEnvironment _environment = null!;
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

    /// <summary>True when the sun or moon is casting shadows.</summary>
    public bool ShadowsOn => _sun.ShadowEnabled;

    /// <summary>How far from the camera shadows are drawn, in metres: closer in gives sharper shadows.</summary>
    public float ShadowReach => _sun.DirectionalShadowMaxDistance;

    /// <summary>Compass degrees the light in the sky is shining from.</summary>
    public float LightBearing { get; private set; }

    public override void _Ready()
    {
        _sun = new DirectionalLight3D
        {
            Name = "Sun",
            ShadowBias = 0.03f,
            ShadowNormalBias = 1.0f,
            ShadowBlur = 1.3f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal,
            DirectionalShadowMaxDistance = 700f,
            DirectionalShadowFadeStart = 1f,
        };
        AddChild(_sun);

        // A weak cool light from the other side, without shadows, so faces turned away from the sun
        // are not all one flat shade.
        _fill = new DirectionalLight3D { Name = "Fill", LightColor = new Color("#CFE0FF"), ShadowEnabled = false, LightSpecular = 0 };
        AddChild(_fill);

        _environment = new WorldEnvironment
        {
            Name = "Environment",
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
                TonemapWhite = 1.3f,
                SsaoRadius = 1.2f,
                SsaoIntensity = 2.2f,
                SsaoPower = 1.6f,
                GlowEnabled = true,
                GlowIntensity = 0.7f,
                GlowStrength = 1.0f,
                GlowBloom = 0f,
                GlowHdrThreshold = 1.5f,
                GlowHdrScale = 1.6f,
                GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
                AdjustmentEnabled = true,
                AdjustmentSaturation = 1.18f,
                AdjustmentContrast = 1.06f,
            },
        };
        AddChild(_environment);
        RenderingServer.DirectionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftHigh);
        RenderingServer.PositionalSoftShadowFilterSetQuality(RenderingServer.ShadowQuality.SoftMedium);
        RenderingServer.DirectionalShadowAtlasSetSize(4096, true);
        Graphics.Changed += Follow;
        Follow();
    }

    public override void _ExitTree() => Graphics.Changed -= Follow;

    private void Follow()
    {
        _sun.ShadowEnabled = Graphics.Shadows;
        _environment.Environment.SsaoEnabled = Graphics.Shadows;
    }

    /// <summary>Sets the light for the time on the clock. The view's direction no longer matters: the sun has its own path.</summary>
    public void Apply(FestivalGame game, float viewYawDegrees, bool settle = false)
    {
        if (settle)
        {
            _cloud = -1;
        }

        var time = game.Now.TimeOfDay;
        var day = (float)Daylight.At(time);
        var golden = (float)Daylight.GoldenHour(time);
        var warmth = (float)SunPath.Warmth(time);
        var light = SunPath.At(time);
        var strength = (float)light.Strength;
        Night = 1 - day;
        LightBearing = (float)light.Bearing;

        // A light shines along its own -Z: with no turn it comes from the south, a quarter turn brings it from the east.
        _sun.RotationDegrees = new Vector3(-(float)light.Height, 180f - LightBearing, 0);
        _fill.RotationDegrees = new Vector3(-40f, -LightBearing + 20f, 0);
        if (light.Moon)
        {
            _sun.LightColor = Moon;
            _sun.LightEnergy = 0.5f * strength;
            _sun.ShadowOpacity = 0.75f;
        }
        else
        {
            _sun.LightColor = warmth < 0.6f ? DaySun.Lerp(GoldenSun, warmth / 0.6f) : GoldenSun.Lerp(LowSun, (warmth - 0.6f) / 0.4f);
            // A low sun only grazes the ground, so it is turned up to keep the evening bright.
            _sun.LightEnergy = 1.08f * strength * Mathf.Lerp(1f, 1.75f, warmth);
            _sun.ShadowOpacity = 0.92f;
        }

        _fill.LightEnergy = Mathf.Lerp(0.02f, 0.16f, day);

        // The sky lights everything a little from all round: blue by day, pink as the sun goes down,
        // violet at dusk and a deep blue at night.
        var env = _environment.Environment;
        var evening = NightAmbient.Lerp(DuskAmbient, Mathf.Clamp(day * 2.5f, 0, 1));
        var sky = evening.Lerp(GoldenAmbient, Mathf.Clamp((day - 0.25f) * 2f, 0, 1)).Lerp(DayAmbient, 1 - warmth);
        env.AmbientLightColor = sky;
        env.AmbientLightEnergy = Mathf.Lerp(0.42f, 0.42f, day) + (0.18f * golden);
        env.BackgroundColor = NightBackdrop.Lerp(DayBackdrop, day);
        // Lamplight on grass is vivid enough without help.
        env.AdjustmentSaturation = Mathf.Lerp(1.0f, 1.18f, day);

        // Cloud takes the strength out of the sun and the colour out of everything; a hot day is a touch brighter and yellower.
        var target = CloudFor(game);
        _cloud = _cloud < 0 ? target : Mathf.MoveToward(_cloud, target, (float)GetProcessDeltaTime() * 0.25f);
        var cloud = _cloud * day;
        _sun.LightColor = _sun.LightColor.Lerp(CloudLight, cloud * 0.8f);
        _sun.LightEnergy *= 1 - (0.6f * cloud);
        _sun.ShadowOpacity *= 1 - (0.65f * cloud);
        env.AmbientLightColor = env.AmbientLightColor.Lerp(CloudLight, cloud * 0.6f);
        env.AmbientLightEnergy += 0.2f * cloud;
        env.AdjustmentSaturation -= 0.3f * cloud;
        env.BackgroundColor = env.BackgroundColor.Lerp(new Color("#5E7D63"), cloud * 0.7f);
        if (game.Weather.Kind == WeatherKind.Hot)
        {
            _sun.LightColor = _sun.LightColor.Lerp(HotSun, day * 0.6f * (1 - warmth));
            _sun.LightEnergy *= 1 + (0.1f * day);
        }

        Frame();
    }

    /// <summary>
    /// Keeps the shadows sharp. The site camera stands a long way back, and shadows drawn all the way
    /// from it to the far hedge are spread too thin to show a person's; so they are drawn only for
    /// the strip of ground actually on screen, which gets narrower as the player zooms in.
    /// </summary>
    private void Frame()
    {
        if (GetViewport()?.GetCamera3D() is not { } camera)
        {
            return;
        }

        if (camera.Projection == Camera3D.ProjectionType.Orthogonal)
        {
            var (near, far) = ShadowStrip(camera.Size);
            camera.Near = near;
            _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Orthogonal;
            _sun.DirectionalShadowMaxDistance = far;

            // The softening is a share of the ground covered, so it is turned down as the view closes in
            // or a person's shadow would be smudged away to nothing.
            _sun.ShadowBlur = Mathf.Clamp(camera.Size / 170f, 0.14f, 1f);
        }
        else
        {
            // A stage camera is down among the crowd: fine shadows close by, coarser ones further off.
            _sun.DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel4Splits;
            _sun.DirectionalShadowMaxDistance = 140f;
            _sun.ShadowBlur = 0.5f;
        }
    }

    /// <summary>How near and how far from the site camera the ground on screen is, with room for tall things, for a view so many metres high.</summary>
    public static (float Near, float Far) ShadowStrip(float viewMetres)
    {
        var reach = (viewMetres * 0.5f / Mathf.Tan(Mathf.DegToRad(CameraRig.PitchDegrees))) + 40f;
        return (Mathf.Max(0.5f, CameraDistance - reach), CameraDistance + reach);
    }
}
