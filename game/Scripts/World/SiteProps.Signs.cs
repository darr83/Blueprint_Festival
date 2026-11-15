using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Signs the player writes on: a sandwich board, a finger post, a banner and a site map board.</summary>
public static partial class SiteProps
{
    /// <summary>The colours a sign can be painted, in the order they are offered.</summary>
    // Written out here: the colours in the other files of this class may not be set yet when this list is made.
    public static readonly Color[] SignColours = { new("#E94F7B"), new("#F6B93B"), new("#2FB7A8"), new("#F6F0E2"), new("#3A3F46") };

    /// <summary>
    /// A sign as it stands, with its words, built round its own middle and facing south. A site
    /// map board draws the layout it is given; with none it shows an empty field.
    /// </summary>
    public static Node3D Sign(PlacedSign sign, SiteLayout? site)
    {
        var colour = SignColours[Math.Clamp(sign.Colour, 0, SignColours.Length - 1)];
        switch (sign.Kind)
        {
            case SignKind.SandwichBoard:
                return SandwichBoard(sign.Lines ?? new List<string>(), colour);
            case SignKind.FingerPost:
            {
                var fingers = new List<(string, bool)>();
                for (var i = 0; i < 3; i++)
                {
                    fingers.Add((sign.Line(i), sign.PointsRight(i)));
                }

                return FingerPost(fingers, colour);
            }

            case SignKind.Banner:
                return Banner(sign.Line(0), colour);
            default:
                return MapBoard(sign.Line(0), site is null ? new List<(float, float, float, float, Color)>() : PlanOf(site), site is null ? (0.5f, 0.5f) : Spot(site, sign));
        }
    }

    private static (float X, float Y) Spot(SiteLayout site, PlacedSign sign)
    {
        var f = site.FieldRect;
        var r = sign.Rect;
        return (Math.Clamp((r.X + (r.Width / 2f) - f.X) / f.Width, 0.03f, 0.97f), Math.Clamp((r.Y + (r.Height / 2f) - f.Y) / f.Height, 0.03f, 0.97f));
    }

    /// <summary>What is built on a site, as blocks from 0 to 1 across and down its field, for the plan on a map board.</summary>
    private static List<(float X, float Y, float W, float H, Color Colour)> PlanOf(SiteLayout site)
    {
        var f = site.FieldRect;
        var blocks = new List<(float, float, float, float, Color)>();
        void Add(TileRect r, Color colour)
        {
            var x = Math.Clamp((r.X - f.X) / (float)f.Width, 0, 1);
            var y = Math.Clamp((r.Y - f.Y) / (float)f.Height, 0, 1);
            blocks.Add((x, y, Math.Min(1 - x, r.Width / (float)f.Width), Math.Min(1 - y, r.Height / (float)f.Height), colour));
        }

        foreach (var s in site.Stages)
        {
            Add(s.Rect, Charcoal);
        }

        foreach (var s in site.Stalls)
        {
            var info = Catalogue.Of(s.Kind);
            Add(s.Rect, info.Serves == Need.Toilet ? Teal : info.Serves == Need.Food ? Tomato : info.Serves == Need.Drink ? Sunflower : Pink);
        }

        foreach (var c in site.Comforts)
        {
            Add(c.Rect, Comforts.Of(c.Kind).Washes ? SoapBlue : Wood);
        }

        return blocks;
    }

    private static Color Ink(Color board) => board.Luminance > 0.6f ? Black : Colors.White;

    /// <summary>Words on a sign, on the face looking along a turn about the upright (0 faces south).</summary>
    private static void Words(Node3D node, string text, Vector3 at, float width, float height, Color colour, float turn = 0, float lean = 0)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        const int fontSize = 96;
        var pixel = Math.Min(height / (fontSize * 0.78f), width / (Math.Max(1, text.Length) * fontSize * 0.62f));
        node.AddChild(new Label3D
        {
            Text = text,
            FontSize = fontSize,
            PixelSize = pixel,
            Modulate = colour,
            OutlineSize = 0,
            Position = at,
            RotationDegrees = new Vector3(lean, turn, 0),
            DoubleSided = false,
        });
    }

    /// <summary>A chalk board on an A-frame, with up to three lines on each face.</summary>
    public static Node3D SandwichBoard(IReadOnlyList<string> lines, Color frame)
    {
        var node = new Node3D { Name = "Sandwich board" };
        var b = new MeshBuilder();
        const float lean = 14f;
        foreach (var side in new[] { 1f, -1f })
        {
            var tilt = new Vector3(-lean * side, 0, 0);
            b.Box(new Vector3(0, 0.52f, 0.14f * side), new Vector3(0.66f, 1.04f, 0.04f), frame, tilt);
            b.Box(new Vector3(0, 0.56f, 0.165f * side), new Vector3(0.56f, 0.84f, 0.02f), Black, tilt);
        }

        b.Box(new Vector3(0, 1.03f, 0), new Vector3(0.68f, 0.05f, 0.1f), frame);
        node.AddChild(b.BuildNode("Shapes"));
        for (var i = 0; i < Math.Min(3, lines.Count); i++)
        {
            var y = 0.8f - (i * 0.24f);
            var z = 0.18f + ((0.56f - y) * MathF.Tan(Mathf.DegToRad(lean)));
            Words(node, lines[i], new Vector3(0, y, z), 0.5f, i == 0 ? 0.2f : 0.15f, i == 0 ? Sunflower : Colors.White, 0, -lean);
            Words(node, lines[i], new Vector3(0, y, -z), 0.5f, i == 0 ? 0.2f : 0.15f, i == 0 ? Sunflower : Colors.White, 180, -lean);
        }

        return node;
    }

    /// <summary>A wooden post with up to three pointing fingers, each with its own words, pointing left or right.</summary>
    public static Node3D FingerPost(IReadOnlyList<(string Words, bool Right)> fingers, Color board)
    {
        var node = new Node3D { Name = "Finger post" };
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 1.3f, 0), new Vector3(0.12f, 2.6f, 0.12f), Wood);
        b.Box(new Vector3(0, 2.64f, 0), new Vector3(0.18f, 0.08f, 0.18f), WoodDark);
        b.Cone(new Vector3(0, 2.76f, 0), new Vector3(0.16f, 0.18f, 0.16f), Sunflower);
        var ink = Ink(board);
        for (var i = 0; i < Math.Min(3, fingers.Count); i++)
        {
            var (words, right) = fingers[i];
            if (string.IsNullOrWhiteSpace(words))
            {
                continue;
            }

            var side = right ? 1f : -1f;
            var y = 2.36f - (i * 0.34f);
            const float length = 1.25f;
            b.Box(new Vector3(side * ((length / 2) + 0.02f), y, 0.08f), new Vector3(length, 0.26f, 0.04f), board);
            // The pointed end.
            b.Box(new Vector3(side * (length + 0.02f), y, 0.08f), new Vector3(0.184f, 0.184f, 0.04f), board, new Vector3(0, 0, 45));
            Words(node, words, new Vector3(side * ((length / 2) + 0.04f), y, 0.102f), length - 0.1f, 0.17f, ink);
            Words(node, words, new Vector3(side * ((length / 2) + 0.04f), y, 0.058f), length - 0.1f, 0.17f, ink, 180);
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A cloth banner strung between two poles, high enough to walk under, with a line of words on each side.</summary>
    public static Node3D Banner(string words, Color cloth)
    {
        var node = new Node3D { Name = "Banner" };
        var b = new MeshBuilder();
        const float w = 4.2f;
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            b.Tube(new Vector3(x, 1.75f, 0), new Vector3(0.09f, 3.5f, 0.09f), Wood);
            b.Ball(new Vector3(x, 3.55f, 0), 0.16f, Sunflower);
            b.Tube(new Vector3(x, 0.03f, 0), new Vector3(0.4f, 0.06f, 0.4f), Block);
        }

        b.Box(new Vector3(0, 2.85f, 0), new Vector3(w - 0.3f, 0.8f, 0.03f), cloth);
        var trim = cloth.Luminance > 0.6f ? Pink : Canvas;
        b.Box(new Vector3(0, 3.22f, 0), new Vector3(w - 0.3f, 0.06f, 0.035f), trim);
        b.Box(new Vector3(0, 2.48f, 0), new Vector3(w - 0.3f, 0.06f, 0.035f), trim);
        foreach (var x in new[] { (-w / 2) + 0.08f, (w / 2) - 0.08f })
        {
            b.Box(new Vector3(x, 3.2f, 0), new Vector3(0.16f, 0.02f, 0.02f), Rope);
            b.Box(new Vector3(x, 2.5f, 0), new Vector3(0.16f, 0.02f, 0.02f), Rope);
        }

        node.AddChild(b.BuildNode("Shapes"));
        Words(node, words, new Vector3(0, 2.85f, 0.02f), w - 0.7f, 0.5f, Ink(cloth));
        Words(node, words, new Vector3(0, 2.85f, -0.02f), w - 0.7f, 0.5f, Ink(cloth), 180);
        return node;
    }

    /// <summary>
    /// A big board under a little roof with a plan of the site on it: a block for each thing built,
    /// given as places from 0 to 1 across and down the field, and a pink spot for "you are here".
    /// </summary>
    public static Node3D MapBoard(string title, IReadOnlyList<(float X, float Y, float W, float H, Color Colour)> blocks, (float X, float Y) here)
    {
        var node = new Node3D { Name = "Site map" };
        var b = new MeshBuilder();
        const float w = 2.2f;
        const float h = 1.5f;
        const float mid = 1.45f;
        foreach (var x in new[] { (-w / 2) - 0.02f, (w / 2) + 0.02f })
        {
            b.Box(new Vector3(x, 1.2f, 0), new Vector3(0.12f, 2.4f, 0.12f), Wood);
        }

        b.Box(new Vector3(0, mid, 0), new Vector3(w, h, 0.06f), Canvas);
        b.Box(new Vector3(0, 2.42f, 0.12f), new Vector3(w + 0.5f, 0.06f, 0.5f), Pink, new Vector3(18, 0, 0));
        b.Box(new Vector3(0, 2.42f, -0.12f), new Vector3(w + 0.5f, 0.06f, 0.5f), PinkDark, new Vector3(-18, 0, 0));

        // The plan: a green field with a hedge round it, and what is built on it.
        const float pw = 1.9f;
        const float ph = 1.0f;
        var top = mid - 0.14f + (ph / 2);
        b.Box(new Vector3(0, mid - 0.14f, 0.032f), new Vector3(pw + 0.08f, ph + 0.08f, 0.01f), TrailerGreen);
        b.Box(new Vector3(0, mid - 0.14f, 0.036f), new Vector3(pw, ph, 0.01f), new Color("#8FD46F"));
        foreach (var (x, y, bw, bh, colour) in blocks)
        {
            b.Box(new Vector3((-pw / 2) + ((x + (bw / 2)) * pw), top - ((y + (bh / 2)) * ph), 0.042f), new Vector3(Math.Max(0.04f, bw * pw), Math.Max(0.04f, bh * ph), 0.01f), colour);
        }

        var spot = new Vector3((-pw / 2) + (here.X * pw), top - (here.Y * ph), 0.05f);
        b.Tube(spot, new Vector3(0.11f, 0.11f, 0.012f), Colors.White, new Vector3(90, 0, 0));
        b.Tube(spot + new Vector3(0, 0, 0.004f), new Vector3(0.075f, 0.075f, 0.012f), Pink, new Vector3(90, 0, 0));
        node.AddChild(b.BuildNode("Shapes"));
        Words(node, title, new Vector3(0, mid + 0.58f, 0.035f), w - 0.2f, 0.2f, Black);
        Words(node, "You are here", new Vector3(Math.Clamp(spot.X, -0.6f, 0.6f), spot.Y - 0.1f, 0.052f), 0.6f, 0.07f, PinkDark);
        return node;
    }
}
