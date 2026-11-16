using System;
using Godot;

namespace BlueprintFestival.World;

public static partial class SiteProps
{
    /// <summary>The colour of sick: a pale yellow-green nobody mistakes for anything else.</summary>
    public static readonly Color SickColour = new("#A3A843");

    private static readonly Color SickDark = new("#878C30");
    private static readonly Color SickBits = new("#C98F3A");
    private static readonly Color MopHead = new("#E9E4D4");
    private static readonly Color BucketYellow = new("#F3C623");

    /// <summary>
    /// Where someone has been sick: a splat on the grass, a slightly different shape each time,
    /// about this many metres across.
    /// </summary>
    public static MeshInstance3D SickPuddle(int seed, float across = 0.7f)
    {
        var random = new Random(seed);
        var b = new MeshBuilder();
        b.Drum(new Vector3(0, 0.012f, 0), new Vector3(across, 0.02f, across * 0.82f), SickColour, new Vector3(0, random.Next(360), 0));
        for (var i = 0; i < 6; i++)
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            var away = across * (0.3f + (0.35f * (float)random.NextDouble()));
            var size = across * (0.16f + (0.26f * (float)random.NextDouble()));
            b.Drum(new Vector3(Mathf.Cos(angle) * away, 0.011f + (i * 0.001f), Mathf.Sin(angle) * away), new Vector3(size, 0.02f, size), i % 3 == 0 ? SickDark : SickColour);
        }

        for (var i = 0; i < 5; i++)
        {
            var angle = (float)(random.NextDouble() * Math.Tau);
            var away = across * 0.3f * (float)random.NextDouble();
            b.Box(new Vector3(Mathf.Cos(angle) * away, 0.028f, Mathf.Sin(angle) * away), new Vector3(0.035f, 0.02f, 0.035f), SickBits, new Vector3(0, random.Next(90), 0));
        }

        var node = b.BuildNode("Sick");
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }

    /// <summary>
    /// What a cleaner brings to a puddle of sick: a yellow mop bucket on wheels and a yellow
    /// "caution" board stood beside the mess while they work.
    /// </summary>
    public static MeshInstance3D MopBucket()
    {
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 0.2f, 0), new Vector3(0.34f, 0.3f, 0.26f), BucketYellow);
        b.Box(new Vector3(0, 0.345f, 0), new Vector3(0.3f, 0.02f, 0.22f), new Color("#6FA8C9"));
        b.Box(new Vector3(0.1f, 0.4f, 0), new Vector3(0.12f, 0.1f, 0.24f), new Color("#D9A91A"));
        foreach (var (x, z) in new[] { (-0.14f, -0.1f), (0.14f, -0.1f), (-0.14f, 0.1f), (0.14f, 0.1f) })
        {
            b.Ball(new Vector3(x, 0.035f, z), 0.07f, Black);
        }

        // The caution board: two yellow panels leaning together like a tent.
        foreach (var side in new[] { -1f, 1f })
        {
            b.Box(new Vector3(0.62f, 0.3f, side * 0.09f), new Vector3(0.3f, 0.62f, 0.02f), BucketYellow, new Vector3(side * 16f, 0, 0));
            b.Triangle(new Vector3(0.62f, 0.46f, side * 0.075f), new Vector3(0.53f, 0.3f, side * 0.125f), new Vector3(0.71f, 0.3f, side * 0.125f), Black);
        }

        return b.BuildNode("Mop bucket");
    }

    /// <summary>
    /// What a cleaner has while mopping, in place of the litter picker and sack: the orange tabard
    /// and a mop held in both hands, its head on the ground in front of them.
    /// </summary>
    public static MeshInstance3D MoppingKit(float h)
    {
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 0.9f * h, 0), new Vector3(0.37f, 0.4f * h, 0.275f), CleanerOrange);
        b.Box(new Vector3(0, 0.86f * h, 0), new Vector3(0.378f, 0.05f * h, 0.283f), Reflective);
        b.Tube(new Vector3(0.05f, 0.62f * h, 0.42f), new Vector3(0.028f, 1.35f * h, 0.028f), Wood, new Vector3(-32, 0, 0));
        b.Ball(new Vector3(0.05f, 0.05f, 0.8f), new Vector3(0.32f, 0.09f, 0.2f), MopHead);
        return b.BuildNode("Mopping");
    }

    /// <summary>Where a mopping cleaner's hands go on the handle, for <see cref="PersonView.Perform"/>.</summary>
    public static (Vector3 Left, Vector3 Right) MopHands(float h) => (new Vector3(0.05f, 0.87f * h, 0.26f), new Vector3(0.05f, 1.04f * h, 0.15f));

    /// <summary>A plastic pint glass on its side where someone dropped it.</summary>
    public static MeshInstance3D DroppedGlass()
    {
        var b = new MeshBuilder();
        b.Drum(new Vector3(0, 0.04f, 0), new Vector3(0.075f, 0.13f, 0.075f), new Color("#F2E9C8"), new Vector3(0, 0, 90));
        b.Drum(new Vector3(0.14f, 0.008f, 0.02f), new Vector3(0.2f, 0.012f, 0.14f), new Color("#D9A441"));
        var node = b.BuildNode("Dropped glass");
        node.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return node;
    }
}
