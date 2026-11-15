using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Builds a big screen around a picture of a given width and height: a scaffold tower behind it,
/// weighted with concrete blocks, a black frame, line-array speakers hung from a bar beside it and
/// subwoofers on the ground. The picture itself is a separate quad (it shows a camera feed), placed
/// at <see cref="PictureCentre"/>. It faces +Z, standing on the ground at the origin.
/// </summary>
public static class ScreenModels
{
    private static readonly Color Steel = new("#9AA3A8");
    private static readonly Color DarkSteel = new("#3E464B");
    private static readonly Color Black = new("#1C1F22");
    private static readonly Color Grille = new("#2B3034");
    private static readonly Color Concrete = new("#B9B4AA");

    /// <summary>How high the bottom of the picture is, for a picture this tall.</summary>
    public static float Bottom(float height) => 1.8f + (height * 0.25f);

    /// <summary>The middle of the picture, just in front of the frame.</summary>
    public static Vector3 PictureCentre(float height) => new(0, Bottom(height) + (height / 2f), 0.16f);

    /// <summary>The whole screen apart from the picture.</summary>
    public static void Add(MeshBuilder m, float width, float height, int speakers) => Scaffold(m, width, height, speakers);

    private static void Scaffold(MeshBuilder m, float w, float h, int speakers)
    {
        var bottom = Bottom(h);
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
}
