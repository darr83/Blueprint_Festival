using Godot;

namespace BlueprintFestival.World;

/// <summary>The three looks offered for the big screens in the mock-up.</summary>
public enum ScreenLook
{
    /// <summary>A black screen on a scaffold tower with speakers hung beside it.</summary>
    Scaffold,

    /// <summary>A screen on a mast on a white trailer, with speaker boxes on the trailer bed.</summary>
    Trailer,

    /// <summary>A screen in a painted timber frame with bunting, wooden speaker cabinets and horns.</summary>
    Programme,
}

/// <summary>
/// Builds the frame, stand and speakers of a big screen around a picture of a given width and
/// height. The picture itself is a separate quad (it shows a camera feed), placed by the caller at
/// <see cref="PictureCentre"/>. Everything faces +Z, with the stand standing on the ground at the origin.
/// </summary>
public static class ScreenModels
{
    private static readonly Color Steel = new("#9AA3A8");
    private static readonly Color DarkSteel = new("#3E464B");
    private static readonly Color Black = new("#1C1F22");
    private static readonly Color Grille = new("#2B3034");
    private static readonly Color Concrete = new("#B9B4AA");
    private static readonly Color White = new("#F4F2EC");
    private static readonly Color Pink = new("#E94F7B");
    private static readonly Color Sunflower = new("#F6B93B");
    private static readonly Color Cream = new("#FFF4DC");
    private static readonly Color Wood = new("#8A6A45");
    private static readonly Color DarkWood = new("#5E4630");
    private static readonly Color Tyre = new("#202224");

    /// <summary>How high the bottom of the picture is, for a picture this tall.</summary>
    public static float Bottom(ScreenLook look, float height) => look switch
    {
        ScreenLook.Trailer => 2.2f + (height * 0.15f),
        _ => 1.8f + (height * 0.25f),
    };

    public static Vector3 PictureCentre(ScreenLook look, float height) => new(0, Bottom(look, height) + (height / 2f), 0.16f);

    /// <summary>The whole screen apart from the picture.</summary>
    public static void Add(MeshBuilder m, ScreenLook look, float width, float height, int speakers)
    {
        switch (look)
        {
            case ScreenLook.Scaffold:
                Scaffold(m, width, height, speakers);
                break;
            case ScreenLook.Trailer:
                Trailer(m, width, height, speakers);
                break;
            default:
                Programme(m, width, height, speakers);
                break;
        }
    }

    private static void Scaffold(MeshBuilder m, float w, float h, int speakers)
    {
        var bottom = Bottom(ScreenLook.Scaffold, h);
        var top = bottom + h + 0.5f;
        // A tower of tubes behind the screen: uprights every couple of metres, two deep, with ledgers and braces.
        var bays = Mathf.Max(1, Mathf.RoundToInt(w / 2f));
        for (var i = 0; i <= bays; i++)
        {
            var x = -w / 2f + (w * i / bays);
            foreach (var z in new[] { -0.35f, -1.6f })
            {
                m.Tube(new Vector3(x, top / 2f, z), new Vector3(0.06f, top, 0.06f), Steel);
                m.Box(new Vector3(x, 0.06f, z), new Vector3(0.5f, 0.12f, 0.5f), Concrete);
            }

            m.Box(new Vector3(x, 0.4f, -1.0f), new Vector3(0.7f, 0.5f, 0.7f), Concrete);
        }

        for (var y = 0.4f; y < top; y += 1.6f)
        {
            foreach (var z in new[] { -0.35f, -1.6f })
            {
                m.Tube(new Vector3(0, y, z), new Vector3(0.05f, w, 0.05f), Steel, new Vector3(0, 0, 90));
            }
        }

        m.Tube(new Vector3(0, top / 2f, -0.98f), new Vector3(0.05f, top * 1.2f, 0.05f), Steel, new Vector3(0, 0, 38));

        // The screen: black panel with a thin bezel.
        m.Box(new Vector3(0, bottom + (h / 2f), 0), new Vector3(w + 0.3f, h + 0.3f, 0.25f), Black);

        // Line arrays hung from a bar out to the side, curving down at the bottom, and subs on the ground.
        var boxes = 2 + speakers;
        foreach (var side in speakers > 1 ? new[] { -1, 1 } : new[] { 1 })
        {
            var x = side * ((w / 2f) + 0.9f);
            m.Tube(new Vector3(side * ((w / 2f) + 0.45f), top, -0.35f), new Vector3(0.06f, 1.0f, 0.06f), DarkSteel, new Vector3(0, 0, 90));
            var y = top - 0.35f;
            for (var b = 0; b < boxes; b++)
            {
                var tilt = 3f * b;
                m.Box(new Vector3(x, y, 0.03f * b), new Vector3(1.0f, 0.34f, 0.65f), Black, new Vector3(tilt, 0, 0));
                m.Box(new Vector3(x, y, 0.33f + (0.03f * b)), new Vector3(0.9f, 0.22f, 0.02f), Grille, new Vector3(tilt, 0, 0));
                y -= 0.36f;
            }

            m.Box(new Vector3(x, 0.4f, 0.4f), new Vector3(1.1f, 0.8f, 0.8f), Black);
            m.Tube(new Vector3(x, 0.4f, 0.81f), new Vector3(0.6f, 0.02f, 0.6f), Grille, new Vector3(90, 0, 0));
        }
    }

    private static void Trailer(MeshBuilder m, float w, float h, int speakers)
    {
        var bottom = Bottom(ScreenLook.Trailer, h);
        var length = Mathf.Max(3.2f, w * 0.85f);
        // The trailer: a white box body on a dark chassis, two wheels a side, a hitch at the front.
        m.Box(new Vector3(0, 0.55f, -0.7f), new Vector3(length, 0.18f, 1.9f), DarkSteel);
        m.Box(new Vector3(0, 0.95f, -0.7f), new Vector3(length, 0.62f, 1.8f), White);
        m.Box(new Vector3(0, 0.92f, 0.21f), new Vector3(length, 0.16f, 0.02f), Pink);
        m.Box(new Vector3(0, 1.08f, 0.21f), new Vector3(length, 0.06f, 0.02f), Sunflower);
        foreach (var wx in new[] { -0.45f, 0.45f })
        {
            foreach (var wz in new[] { 0.25f, -1.65f })
            {
                m.Tube(new Vector3(wx, 0.36f, wz), new Vector3(0.72f, 0.22f, 0.72f), Tyre, new Vector3(90, 0, 0));
            }
        }

        m.Box(new Vector3(-(length / 2f) - 0.6f, 0.55f, -0.7f), new Vector3(1.2f, 0.1f, 0.12f), DarkSteel);

        // Yellow outrigger legs at the four corners hold it steady.
        foreach (var sx in new[] { -1, 1 })
        {
            foreach (var sz in new[] { 0.4f, -1.8f })
            {
                m.Box(new Vector3(sx * ((length / 2f) + 0.35f), 0.7f, sz), new Vector3(0.7f, 0.1f, 0.1f), Sunflower);
                m.Tube(new Vector3(sx * ((length / 2f) + 0.65f), 0.35f, sz), new Vector3(0.1f, 0.7f, 0.1f), Sunflower);
                m.Box(new Vector3(sx * ((length / 2f) + 0.65f), 0.03f, sz), new Vector3(0.36f, 0.06f, 0.36f), DarkSteel);
            }
        }

        // A mast up the middle to the screen, with a grey frame round it.
        m.Box(new Vector3(0, (1.26f + bottom) / 2f, -0.45f), new Vector3(0.32f, bottom - 1.26f + 0.3f, 0.32f), Steel);
        m.Box(new Vector3(0, bottom + (h / 2f), -0.05f), new Vector3(w + 0.24f, h + 0.24f, 0.32f), DarkSteel);
        m.Box(new Vector3(0, bottom - 0.18f, 0.0f), new Vector3(w * 0.6f, 0.12f, 0.12f), Pink);

        // Speaker boxes on the trailer bed, facing the crowd.
        var count = speakers + 1;
        for (var i = 0; i < count; i++)
        {
            var x = count == 1 ? 0f : -(length / 2f) + 0.55f + ((length - 1.1f) * i / (count - 1));
            m.Box(new Vector3(x, 1.75f, -0.1f), new Vector3(0.75f, 1.0f, 0.65f), Black);
            m.Tube(new Vector3(x, 1.95f, 0.24f), new Vector3(0.28f, 0.02f, 0.28f), Grille, new Vector3(90, 0, 0));
            m.Tube(new Vector3(x, 1.55f, 0.24f), new Vector3(0.5f, 0.02f, 0.5f), Grille, new Vector3(90, 0, 0));
        }
    }

    private static void Programme(MeshBuilder m, float w, float h, int speakers)
    {
        var bottom = Bottom(ScreenLook.Programme, h);
        var top = bottom + h;
        // Two stout timber posts with feet, a cream painted frame with a pink edge round the picture.
        foreach (var side in new[] { -1, 1 })
        {
            var x = side * ((w / 2f) + 0.45f);
            m.Box(new Vector3(x, (top + 0.9f) / 2f, -0.1f), new Vector3(0.3f, top + 0.9f, 0.3f), Wood);
            m.Box(new Vector3(x, 0.08f, -0.1f), new Vector3(0.5f, 0.16f, 1.8f), DarkWood);
            m.Box(new Vector3(x, 0.9f, -0.75f), new Vector3(0.14f, 0.14f, 1.9f), Wood, new Vector3(45, 0, 0));
        }

        m.Box(new Vector3(0, bottom + (h / 2f), -0.02f), new Vector3(w + 0.6f, h + 0.6f, 0.22f), Cream);
        m.Box(new Vector3(0, bottom + (h / 2f), 0.02f), new Vector3(w + 0.16f, h + 0.16f, 0.22f), Pink);

        // A sunflower name board on top, with bunting from post to post above it.
        m.Box(new Vector3(0, top + 0.6f, 0), new Vector3(Mathf.Min(w * 0.55f, 4.5f), 0.5f + (h * 0.04f), 0.12f), Sunflower);
        m.Tube(new Vector3(0, top + 1.25f, 0.05f), new Vector3(0.02f, w + 0.9f, 0.02f), DarkWood, new Vector3(0, 0, 90));
        var flags = Mathf.RoundToInt((w + 0.8f) / 0.42f);
        var colours = new[] { Pink, Sunflower, Cream };
        for (var i = 0; i < flags; i++)
        {
            var x = -(w / 2f) - 0.35f + ((w + 0.7f) * (i + 0.5f) / flags);
            var sag = 0.18f * (1f - Mathf.Pow((2f * x) / (w + 0.9f), 2f));
            m.Cone(new Vector3(x, top + 1.1f - sag, 0.06f), new Vector3(0.3f, 0.32f, 0.03f), colours[i % 3], new Vector3(180, 0, 0));
        }

        // Wooden speaker cabinets on stands beside the posts, with a horn on each post.
        foreach (var side in speakers > 1 ? new[] { -1, 1 } : new[] { 1 })
        {
            var x = side * ((w / 2f) + 1.35f);
            var stack = 1 + (speakers / 2);
            m.Box(new Vector3(x, 0.3f, 0), new Vector3(0.95f, 0.6f, 0.8f), DarkWood);
            for (var s = 0; s < stack; s++)
            {
                var y = 1.05f + (s * 1.0f);
                m.Box(new Vector3(x, y, 0), new Vector3(0.9f, 0.95f, 0.7f), Wood);
                m.Tube(new Vector3(x, y + 0.18f, 0.36f), new Vector3(0.3f, 0.02f, 0.3f), Pink, new Vector3(90, 0, 0));
                m.Tube(new Vector3(x, y - 0.15f, 0.36f), new Vector3(0.5f, 0.02f, 0.5f), Pink, new Vector3(90, 0, 0));
            }

            var px = side * ((w / 2f) + 0.45f);
            m.Cone(new Vector3(px, top + 0.75f, 0.35f), new Vector3(0.5f, 0.6f, 0.5f), Cream, new Vector3(-75, 0, 0));
        }
    }
}
