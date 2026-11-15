using BlueprintFestival.Simulation;
using BlueprintFestival.Simulation.Core;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The sky seen from down among the crowd: a dome drawn behind everything else, blue by day with a
/// few clouds, gold and pink as the sun goes down, dark with stars and the moon at night, and grey
/// and heavy under rain. Only the stage cameras (and the feeds to the big screens) look up far
/// enough to see it; the view of the site looks down on the field and leaves it out.
/// </summary>
public partial class Sky
{
    /// <summary>The render layer the sky is drawn on, left out of the view of the site.</summary>
    public const uint SkyLayer = 1u << 12;

    private static readonly Color DayZenith = new("#2E6CCB");
    private static readonly Color DayHorizon = new("#94C2EC");
    private static readonly Color GoldenZenith = new("#4C6DB5");
    private static readonly Color GoldenHorizon = new("#F0A878");
    private static readonly Color DuskZenith = new("#262A68");
    private static readonly Color DuskHorizon = new("#C9707F");
    private static readonly Color NightZenith = new("#04070F");
    private static readonly Color NightHorizon = new("#141C34");
    private static readonly Color GreyZenith = new("#7D8792");
    private static readonly Color GreyHorizon = new("#A9B1B9");
    private static readonly Color SunsetGlow = new("#FF9A5C");
    private static readonly Color DayCloud = new("#FFFFFF");
    private static readonly Color GoldenCloud = new("#FFC9A6");
    private static readonly Color DuskCloud = new("#8E6E99");
    private static readonly Color NightCloud = new("#232B40");
    private static readonly Color RainCloud = new("#8F98A3");

    private static Shader? _domeShader;

    /// <summary>
    /// How the sky is drawn. Every point of the dome is pushed back to the far end of the view, so
    /// anything at all in the scene stands in front of it, however far away.
    /// </summary>
    private static Shader DomeShader => _domeShader ??= new Shader
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

            uniform vec3 zenith : source_color;
            uniform vec3 horizon : source_color;
            uniform vec3 ground : source_color;
            uniform vec3 glow_colour : source_color;
            uniform float glow = 0.0;
            uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
            uniform vec3 sun_colour : source_color;
            uniform float sun_show = 0.0;
            uniform vec3 moon_dir = vec3(0.0, 1.0, 0.0);
            uniform float moon_show = 0.0;
            uniform float stars = 0.0;
            uniform float cloud_cover = 0.2;
            uniform vec3 cloud_lit : source_color;
            uniform vec3 cloud_shade : source_color;
            uniform vec2 drift = vec2(0.0);
            uniform float brightness = 1.0;

            varying vec3 world_pos;

            float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
            float hash3(vec3 p) { return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453); }

            float noise(vec2 p) {
                vec2 i = floor(p);
                vec2 f = fract(p);
                vec2 u = f * f * (3.0 - 2.0 * f);
                return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
            }

            float fbm(vec2 p) {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 5; i++) {
                    v += a * noise(p);
                    p = p * 2.03 + vec2(17.0, 9.0);
                    a *= 0.5;
                }
                return v;
            }

            void vertex() {
                world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
                POSITION = PROJECTION_MATRIX * (MODELVIEW_MATRIX * vec4(VERTEX, 1.0));
                // Right at the far end of the view (the depth runs from 1 near to 0 far).
                POSITION.z = POSITION.w * 0.000001;
            }

            void fragment() {
                vec3 dir = normalize(world_pos - CAMERA_POSITION_WORLD);
                float up = dir.y;
                float h = clamp(up, 0.0, 1.0);
                vec3 col = mix(horizon, zenith, pow(h, 0.38));

                // A warm glow low down on the side the sun is setting.
                vec3 flat_dir = normalize(vec3(dir.x, 0.0, dir.z) + vec3(0.00001));
                vec3 sun_flat = normalize(vec3(sun_dir.x, 0.0, sun_dir.z) + vec3(0.00001));
                float side = max(dot(flat_dir, sun_flat), 0.0);
                col = mix(col, glow_colour, glow * pow(side, 3.0) * pow(1.0 - h, 3.0));

                // Stars, faint ones and bright ones, and the moon.
                vec3 s = dir * 220.0;
                vec3 cell = floor(s);
                float r = hash3(cell);
                float near_centre = smoothstep(0.32, 0.0, length(fract(s) - 0.5));
                vec3 night_lights = vec3(0.85, 0.9, 1.0) * stars * step(0.9965, r) * near_centre * (0.6 + 2.4 * step(0.9993, r)) * smoothstep(0.02, 0.15, up);
                float md = dot(dir, moon_dir);
                night_lights += vec3(1.0, 0.98, 0.9) * moon_show * (2.2 * smoothstep(0.99955, 0.99968, md) + 0.25 * pow(max(md, 0.0), 60.0));

                // The sun: a bright disc with a halo round it.
                float sd = dot(dir, sun_dir);
                vec3 sunlight = sun_colour * sun_show * (4.0 * smoothstep(0.99955, 0.99972, sd) + 0.5 * pow(max(sd, 0.0), 300.0) + 0.18 * pow(max(sd, 0.0), 12.0));

                // Clouds on a flat layer overhead, thinning out towards the horizon.
                vec2 p = dir.xz / (up + 0.12) * 2.4 + drift;
                float d = fbm(p);
                float cover = smoothstep(0.66 - 0.5 * cloud_cover, 0.8 - 0.32 * cloud_cover, d) * smoothstep(0.0, 0.1, up);
                float shade = clamp((fbm(p * 1.7 + vec2(3.0, 1.0)) - 0.3) * 1.6, 0.0, 1.0);
                vec3 cloud = mix(cloud_shade, cloud_lit, shade);
                // The sun lights up the edge of a cloud in front of it.
                cloud += sun_colour * sun_show * 0.35 * pow(max(sd, 0.0), 20.0) * (1.0 - cover);

                col = col + night_lights * (1.0 - cover) + sunlight * (1.0 - 0.9 * cover);
                col = mix(col, cloud, cover);

                // Below the horizon is the land beyond the hedges, hazy where it meets the sky.
                col = mix(col, ground, smoothstep(0.0, -0.05, up));
                ALBEDO = col * brightness;
            }
            """,
    };

    private MeshInstance3D _dome = null!;
    private ShaderMaterial _domeMaterial = null!;

    private void MakeDome()
    {
        _domeMaterial = new ShaderMaterial { Shader = DomeShader };
        _dome = new MeshInstance3D
        {
            Name = "SkyDome",
            Mesh = new SphereMesh { Radius = 1000f, Height = 2000f, RadialSegments = 48, Rings = 24 },
            MaterialOverride = _domeMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Layers = SkyLayer,
            // Never left out for being off screen: the camera is always inside it.
            CustomAabb = new Aabb(new Vector3(-5000, -5000, -5000), new Vector3(10000, 10000, 10000)),
        };
        AddChild(_dome);
    }

    /// <summary>Which way the sky is, from the ground, at so many degrees up and a compass bearing (90 east, 180 south).</summary>
    public static Vector3 SkyDirection(double heightDegrees, double bearingDegrees)
    {
        var h = Mathf.DegToRad((float)heightDegrees);
        var b = Mathf.DegToRad((float)bearingDegrees);
        return new Vector3(Mathf.Cos(h) * Mathf.Sin(b), Mathf.Sin(h), -Mathf.Cos(h) * Mathf.Cos(b));
    }

    /// <summary>The colour of the sky overhead and at the horizon, for how light it is, how golden the light and how cloudy.</summary>
    public static (Color Zenith, Color Horizon) SkyColours(float day, float warmth, float cloud)
    {
        var dusk = Mathf.Clamp(day * 2.5f, 0, 1);
        var gold = Mathf.Clamp((day - 0.25f) * 2f, 0, 1);
        var zenith = NightZenith.Lerp(DuskZenith, dusk).Lerp(GoldenZenith, gold).Lerp(DayZenith, 1 - warmth);
        var horizon = NightHorizon.Lerp(DuskHorizon, dusk).Lerp(GoldenHorizon, gold).Lerp(DayHorizon, 1 - warmth);

        // Rain cloud turns the whole sky grey, and darker as the light goes.
        var grey = Mathf.Clamp(cloud * 1.15f, 0, 1);
        zenith = zenith.Lerp(GreyZenith * Mathf.Lerp(0.25f, 1f, day), grey);
        horizon = horizon.Lerp(GreyHorizon * Mathf.Lerp(0.25f, 1f, day), grey);
        return (zenith, horizon);
    }

    /// <summary>How much of the sky is under cloud: a few fair-weather clouds on a dry day, none on a hot one, and all of it in the rain.</summary>
    public static float CloudCover(WeatherKind weather, float overcast) =>
        weather == WeatherKind.Hot ? 0.06f : Mathf.Clamp(0.24f + (0.86f * overcast), 0, 1);

    private void PaintDome(FestivalGame game, float day, float warmth, float golden, Color ground)
    {
        var time = game.Now.TimeOfDay;
        var cloud = Overcast;
        var (zenith, horizon) = SkyColours(day, warmth, cloud);
        var m = _domeMaterial;
        m.SetShaderParameter("zenith", zenith);
        m.SetShaderParameter("horizon", horizon);
        m.SetShaderParameter("ground", ground);
        m.SetShaderParameter("glow_colour", SunsetGlow);
        m.SetShaderParameter("glow", golden * 0.75f * (1 - cloud));

        var height = SunPath.Height(time);
        var sunUp = Mathf.Clamp(((float)height + 1f) / 3f, 0, 1);
        m.SetShaderParameter("sun_dir", SkyDirection(height, SunPath.Bearing(time)));
        m.SetShaderParameter("sun_colour", DaySun.Lerp(GoldenSun, warmth).Lerp(LowSun, warmth * warmth));
        m.SetShaderParameter("sun_show", sunUp * (1 - (0.95f * cloud)));

        var moon = SunPath.At(time);
        m.SetShaderParameter("moon_dir", SkyDirection(SunPath.MoonHeight, SunPath.MoonBearing));
        m.SetShaderParameter("moon_show", moon.Moon ? (float)moon.Strength * (1 - cloud) : 0);
        m.SetShaderParameter("stars", Mathf.Clamp((1 - day - 0.35f) / 0.5f, 0, 1) * (1 - cloud));

        m.SetShaderParameter("cloud_cover", CloudCover(game.Weather.Kind, cloud));
        var dusk = Mathf.Clamp(day * 2.5f, 0, 1);
        var gold = Mathf.Clamp((day - 0.25f) * 2f, 0, 1);
        var lit = NightCloud.Lerp(DuskCloud, dusk).Lerp(GoldenCloud, gold).Lerp(DayCloud, 1 - warmth);
        lit = lit.Lerp(RainCloud * Mathf.Lerp(0.3f, 1f, day), cloud);
        m.SetShaderParameter("cloud_lit", lit);
        m.SetShaderParameter("cloud_shade", lit.Darkened(0.28f + (0.2f * cloud)));

        // The clouds drift across with the game clock, from the west.
        var minutes = (float)(time.TotalMinutes + (game.Now.DayOfYear * 1440));
        m.SetShaderParameter("drift", new Vector2(minutes * -0.004f, minutes * 0.0015f));
        m.SetShaderParameter("brightness", 1.0f);
    }
}
