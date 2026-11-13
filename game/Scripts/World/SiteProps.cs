using System;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// Builds the things the player puts in the field: stages, fences and gates. Each is made of simple
/// coloured shapes, in the same chunky style as the people. A thing is built round its own middle,
/// facing south (towards +Z), so the caller only has to place and turn it.
/// </summary>
public static class SiteProps
{
    private static readonly Color Pink = new("#E94F7B");
    private static readonly Color PinkDark = new("#C73A64");
    private static readonly Color Sunflower = new("#F6B93B");
    private static readonly Color Canvas = new("#F6F0E2");
    private static readonly Color Wood = new("#9A7648");
    private static readonly Color WoodDark = new("#6F5333");
    private static readonly Color Deck = new("#B8946A");
    private static readonly Color Steel = new("#B9C0C6");
    private static readonly Color SteelDark = new("#8C949B");
    private static readonly Color Black = new("#26292E");
    private static readonly Color Backdrop = new("#2C3B48");
    private static readonly Color Charcoal = new("#3A3F46");
    private static readonly Color Straw = new("#E3C56B");
    private static readonly Color StrawDark = new("#C9A94F");
    private static readonly Color Rubber = new("#2B2B2E");
    private static readonly Color TrailerGreen = new("#3E7A5A");
    private static readonly Color Rope = new("#E9DFC4");
    private static readonly Color Block = new("#A9A59B");

    private static readonly Color[] BuntingColours =
    {
        new("#E94F7B"), new("#F6B93B"), new("#F6F0E2"), new("#4FA8D8"), new("#8FD3A8"),
    };

    public static Node3D Stage(StageKind kind, string festivalName)
    {
        var node = new Node3D { Name = Catalogue.Of(kind).Name };
        var b = new MeshBuilder();
        switch (kind)
        {
            case StageKind.FarmTrailer:
                FarmTrailer(b, node, festivalName);
                break;
            case StageKind.Bandstand:
                Bandstand(b, node, festivalName);
                break;
            default:
                HiredStage(b, node, festivalName);
                break;
        }

        node.AddChild(b.BuildNode("Shapes"));

        // Sit the model at the back of its ground, so steps and bales at the front are inside it.
        var shift = kind switch { StageKind.FarmTrailer => -0.25f, StageKind.Bandstand => -0.5f, _ => 0f };
        foreach (var child in node.GetChildren())
        {
            if (child is Node3D part)
            {
                part.Position += new Vector3(0, 0, shift);
            }
        }

        return node;
    }

    private static void FarmTrailer(MeshBuilder b, Node3D node, string name)
    {
        const float w = 7f, d = 2.5f, top = 1.1f;
        // The bed, its frame and four fat tyres.
        b.Box(new Vector3(0, top - 0.09f, 0), new Vector3(w, 0.18f, d), Deck);
        b.Box(new Vector3(0, top - 0.3f, 0), new Vector3(w - 0.3f, 0.24f, d - 0.5f), TrailerGreen);
        foreach (var x in new[] { -0.6f, 0.6f })
        {
            foreach (var z in new[] { -1.05f, 1.05f })
            {
                b.Tube(new Vector3(x + 1.2f, 0.5f, z), new Vector3(1f, 0.36f, 1f), Rubber, new Vector3(90, 0, 0));
                b.Tube(new Vector3(x + 1.2f, 0.5f, z + (Math.Sign(z) * 0.19f)), new Vector3(0.45f, 0.04f, 0.45f), Sunflower, new Vector3(90, 0, 0));
            }
        }

        // Drawbar and a stand at the front of the trailer (the west end).
        b.Box(new Vector3((-w / 2) - 0.2f, 0.62f, 0), new Vector3(0.5f, 0.14f, 0.2f), TrailerGreen);
        b.Box(new Vector3((-w / 2) - 0.4f, 0.3f, 0), new Vector3(0.1f, 0.6f, 0.1f), SteelDark);
        b.Box(new Vector3((-w / 2) + 0.6f, 0.4f, 0), new Vector3(0.12f, 0.8f, 1.6f), SteelDark);

        // A tall board at the back with the name painted on.
        b.Box(new Vector3(0, top + 1.25f, (-d / 2) + 0.06f), new Vector3(w, 2.5f, 0.12f), Pink);
        b.Box(new Vector3(0, top + 2.42f, (-d / 2) + 0.09f), new Vector3(w, 0.16f, 0.14f), Sunflower);
        b.Box(new Vector3(0, top + 0.1f, (-d / 2) + 0.09f), new Vector3(w, 0.2f, 0.14f), Sunflower);
        Painted(node, name, new Vector3(0, top + 1.1f, (-d / 2) + 0.13f), 5.6f, 0.7f, Colors.White);

        // Straw bales: a skirt along the front and a stack to climb up at the east end.
        for (var i = 0; i < 6; i++)
        {
            Bale(b, new Vector3(-2.6f + (i * 1.04f), 0.22f, (d / 2) + 0.3f), i % 2 == 0);
        }

        Bale(b, new Vector3((w / 2) + 0.22f, 0.22f, 0.2f), true, 90);
        Bale(b, new Vector3((w / 2) + 0.22f, 0.66f, 0.2f), false, 90);

        // Poles at the corners with bunting strung between.
        Bunting(b, new Vector3((-w / 2) + 0.1f, top + 2.3f, (-d / 2) + 0.16f), new Vector3((w / 2) - 0.1f, top + 2.3f, (-d / 2) + 0.16f), 0.3f);
        Speaker(b, new Vector3((-w / 2) + 0.7f, top, 0.5f), 0.9f, onStand: true);
        Speaker(b, new Vector3((w / 2) - 0.7f, top, 0.5f), 0.9f, onStand: true);
    }

    private static void Bandstand(MeshBuilder b, Node3D node, string name)
    {
        const float w = 6f, d = 4.5f, top = 0.6f, eaves = 3.0f, ridge = 4.3f;
        // Deck on a skirt of boards.
        b.Box(new Vector3(0, top - 0.07f, 0), new Vector3(w, 0.14f, d), Deck);
        b.Box(new Vector3(0, (top - 0.14f) / 2, 0), new Vector3(w - 0.1f, top - 0.14f, d - 0.1f), WoodDark);
        for (var i = 0; i < 12; i++)
        {
            b.Box(new Vector3((-w / 2) + 0.25f + (i * 0.5f), (top - 0.14f) / 2, (d / 2) - 0.03f), new Vector3(0.42f, top - 0.18f, 0.05f), i % 2 == 0 ? Canvas : Pink);
        }

        // Steps at the front.
        b.Box(new Vector3(0, 0.15f, (d / 2) + 0.55f), new Vector3(1.6f, 0.3f, 0.5f), Deck);
        b.Box(new Vector3(0, 0.22f, (d / 2) + 0.2f), new Vector3(1.6f, 0.44f, 0.4f), Deck);

        // Poles.
        foreach (var x in new[] { (-w / 2) + 0.12f, (w / 2) - 0.12f })
        {
            foreach (var z in new[] { (-d / 2) + 0.12f, (d / 2) - 0.12f })
            {
                b.Tube(new Vector3(x, top + ((eaves - top) / 2), z), new Vector3(0.14f, eaves - top, 0.14f), Canvas);
            }
        }

        // A pitched canvas roof in pink and cream stripes, the ridge running front to back.
        const int stripes = 9;
        var half = (w / 2) + 0.35f;
        var rise = ridge - eaves;
        var slope = MathF.Sqrt((half * half) + (rise * rise));
        var angle = Mathf.RadToDeg(MathF.Atan2(rise, half));
        var stripeDepth = (d + 0.7f) / stripes;
        for (var i = 0; i < stripes; i++)
        {
            var z = (-(d + 0.7f) / 2) + ((i + 0.5f) * stripeDepth);
            var colour = i % 2 == 0 ? Pink : Canvas;
            b.Box(new Vector3(-half / 2, eaves + (rise / 2), z), new Vector3(slope, 0.07f, stripeDepth + 0.005f), colour, new Vector3(0, 0, angle));
            b.Box(new Vector3(half / 2, eaves + (rise / 2), z), new Vector3(slope, 0.07f, stripeDepth + 0.005f), colour, new Vector3(0, 0, -angle));
        }

        // A scalloped edge along both eaves.
        for (var i = 0; i < stripes; i++)
        {
            var z = (-(d + 0.7f) / 2) + ((i + 0.5f) * stripeDepth);
            var colour = i % 2 == 0 ? PinkDark : Canvas;
            b.Box(new Vector3(-half, eaves - 0.14f, z), new Vector3(0.05f, 0.3f, stripeDepth - 0.04f), colour);
            b.Box(new Vector3(half, eaves - 0.14f, z), new Vector3(0.05f, 0.3f, stripeDepth - 0.04f), colour);
        }

        // Canvas back wall, and a gable board with the name on.
        b.Box(new Vector3(0, top + ((eaves - top) / 2), (-d / 2) + 0.1f), new Vector3(w - 0.3f, eaves - top, 0.05f), Canvas);
        b.Box(new Vector3(0, eaves + 0.28f, (d / 2) + 0.3f), new Vector3(3.6f, 0.62f, 0.08f), Sunflower);
        Painted(node, name, new Vector3(0, eaves + 0.28f, (d / 2) + 0.35f), 3.2f, 0.42f, new Color("#1D2B36"));
        b.Ball(new Vector3(0, ridge + 0.1f, (d / 2) + 0.3f), 0.26f, Sunflower);

        Bunting(b, new Vector3((-w / 2) + 0.12f, eaves - 0.35f, (d / 2) - 0.1f), new Vector3((w / 2) - 0.12f, eaves - 0.35f, (d / 2) - 0.1f), 0.4f);
        Speaker(b, new Vector3((-w / 2) + 0.6f, top, (d / 2) - 0.6f), 0.8f, onStand: true);
        Speaker(b, new Vector3((w / 2) - 0.6f, top, (d / 2) - 0.6f), 0.8f, onStand: true);
    }

    private static void HiredStage(MeshBuilder b, Node3D node, string name)
    {
        const float w = 9f, d = 6f, top = 1.3f, front = 6.6f, back = 5.2f;
        // Deck with a black skirt, and steps at the east side.
        b.Box(new Vector3(0, top - 0.06f, 0), new Vector3(w, 0.12f, d), new Color("#70757C"));
        b.Box(new Vector3(0, (top - 0.12f) / 2, 0), new Vector3(w - 0.06f, top - 0.12f, d - 0.06f), Black);
        for (var i = 0; i < 4; i++)
        {
            b.Box(new Vector3((w / 2) + 0.2f + (i * 0.32f), (top * (4 - i) / 5f) / 2, (-d / 2) + 1f), new Vector3(0.32f, top * (4 - i) / 5f, 1.1f), SteelDark);
        }

        // Four truss towers and the roof beams.
        var tx = (w / 2) - 0.2f;
        var tz = (d / 2) - 0.2f;
        Truss(b, new Vector3(-tx, top, tz), new Vector3(-tx, front, tz));
        Truss(b, new Vector3(tx, top, tz), new Vector3(tx, front, tz));
        Truss(b, new Vector3(-tx, top, -tz), new Vector3(-tx, back, -tz));
        Truss(b, new Vector3(tx, top, -tz), new Vector3(tx, back, -tz));
        Truss(b, new Vector3(-tx, front, tz), new Vector3(tx, front, tz));
        Truss(b, new Vector3(-tx, back, -tz), new Vector3(tx, back, -tz));
        Truss(b, new Vector3(-tx, front, tz), new Vector3(-tx, back, -tz));
        Truss(b, new Vector3(tx, front, tz), new Vector3(tx, back, -tz));

        // The roof skin, sloping down to the back, with a pink front edge.
        var tiltRadians = MathF.Atan2(front - back, d - 0.4f);
        var tilt = -Mathf.RadToDeg(tiltRadians);
        var length = (d + 0.5f) / MathF.Cos(tiltRadians);
        b.Box(new Vector3(0, ((front + back) / 2) + 0.3f, 0), new Vector3(w + 0.7f, 0.1f, length), new Color("#56606A"), new Vector3(tilt, 0, 0));
        b.Box(new Vector3(0, ((front + back) / 2) + 0.33f, 0), new Vector3(w + 0.1f, 0.1f, length - 0.6f), new Color("#687480"), new Vector3(tilt, 0, 0));
        b.Box(new Vector3(0, front - 0.1f, tz + 0.34f), new Vector3(w + 0.1f, 0.6f, 0.08f), Pink);
        Painted(node, name, new Vector3(0, front - 0.1f, tz + 0.39f), 6.2f, 0.42f, Colors.White);

        // Black backdrop and side wings.
        b.Box(new Vector3(0, (top + back) / 2, -tz - 0.05f), new Vector3(w - 0.5f, back - top, 0.06f), Backdrop);
        b.Box(new Vector3(-tx - 0.02f, (top + back) / 2, -1.3f), new Vector3(0.06f, back - top, 2.9f), Backdrop);
        b.Box(new Vector3(tx + 0.02f, (top + back) / 2, -1.3f), new Vector3(0.06f, back - top, 2.9f), Backdrop);
        b.Box(new Vector3(0, top + 2.3f, -tz + 0.02f), new Vector3(4.6f, 1.5f, 0.05f), Sunflower);
        b.Box(new Vector3(0, top + 2.3f, -tz + 0.05f), new Vector3(4.3f, 1.2f, 0.05f), Pink);

        // Lights hung along the front beam.
        var cans = new[] { new Color("#FFE08A"), new Color("#F29BBB"), new Color("#8FD3F0"), new Color("#FFE08A"), new Color("#B9F0A8"), new Color("#F29BBB"), new Color("#8FD3F0"), new Color("#FFE08A") };
        for (var i = 0; i < cans.Length; i++)
        {
            var x = -3.5f + (i * 1f);
            b.Tube(new Vector3(x, front - 0.62f, tz - 0.05f), new Vector3(0.3f, 0.36f, 0.3f), Black, new Vector3(55, 0, 0));
            b.Tube(new Vector3(x, front - 0.73f, tz + 0.11f), new Vector3(0.24f, 0.03f, 0.24f), cans[i], new Vector3(55, 0, 0));
        }

        // Speaker stacks on wings either side, and kit on the stage.
        foreach (var side in new[] { -1f, 1f })
        {
            var x = side * ((w / 2) + 0.75f);
            b.Box(new Vector3(x, top / 2, tz - 0.6f), new Vector3(1.3f, top, 1.3f), Black);
            Speaker(b, new Vector3(x, top, tz - 0.6f), 1.1f, onStand: false);
            Speaker(b, new Vector3(x, top + 1.1f, tz - 0.6f), 1.1f, onStand: false);
        }

        b.Box(new Vector3(-0.2f, top + 0.35f, -1.4f), new Vector3(1.9f, 0.3f, 1.5f), Charcoal);
        b.Box(new Vector3(-2.9f, top + 0.4f, -1.7f), new Vector3(0.8f, 0.8f, 0.5f), Black);
        b.Box(new Vector3(2.9f, top + 0.4f, -1.7f), new Vector3(0.8f, 0.8f, 0.5f), Black);
        MicStand(b, new Vector3(-2.2f, top, 1.2f));
        MicStand(b, new Vector3(2.2f, top, 1.2f));
        foreach (var x in new[] { -1.1f, 1.1f })
        {
            b.Box(new Vector3(x, top + 0.16f, 2.3f), new Vector3(0.7f, 0.3f, 0.45f), Black, new Vector3(-25, 0, 0));
        }
    }

    /// <summary>A run of fence between two points on the ground.</summary>
    public static Node3D Fence(FenceKind kind, Vector3 from, Vector3 to)
    {
        var b = new MeshBuilder();
        AddFence(b, kind, from, to);
        return b.BuildNode(Catalogue.Of(kind).Name);
    }

    /// <summary>Adds a run of fence to a builder, so many runs can share one mesh.</summary>
    public static void AddFence(MeshBuilder b, FenceKind kind, Vector3 from, Vector3 to)
    {
        var run = to - from;
        var length = run.Length();
        if (length < 0.05f)
        {
            return;
        }

        var along = run / length;
        var turn = new Vector3(0, Mathf.RadToDeg(MathF.Atan2(-along.Z, along.X)), 0);
        switch (kind)
        {
            case FenceKind.Rope:
            {
                var posts = Math.Max(1, (int)MathF.Round(length / 2f));
                for (var i = 0; i <= posts; i++)
                {
                    var at = from + (along * (length * i / posts));
                    b.Tube(at + new Vector3(0, 0.55f, 0), new Vector3(0.1f, 1.1f, 0.1f), Wood);
                    b.Ball(at + new Vector3(0, 1.12f, 0), 0.13f, Sunflower);
                }

                var mid = from + (run / 2);
                b.Box(mid + new Vector3(0, 0.95f, 0), new Vector3(length, 0.045f, 0.045f), Rope, turn);
                b.Box(mid + new Vector3(0, 0.55f, 0), new Vector3(length, 0.045f, 0.045f), Rope, turn);
                break;
            }

            case FenceKind.Paling:
            {
                var pales = Math.Max(2, (int)MathF.Round(length / 0.14f));
                for (var i = 0; i <= pales; i++)
                {
                    var at = from + (along * (length * i / pales));
                    var post = i % 14 == 0;
                    var height = post ? 1.35f : 1.15f + (0.04f * ((i * 7) % 3));
                    b.Box(at + new Vector3(0, height / 2, 0), post ? new Vector3(0.1f, height, 0.1f) : new Vector3(0.07f, height, 0.03f), post ? WoodDark : (i % 3 == 0 ? Wood : Deck), turn);
                }

                var mid = from + (run / 2);
                b.Box(mid + new Vector3(0, 0.95f, 0), new Vector3(length, 0.03f, 0.05f), SteelDark, turn);
                b.Box(mid + new Vector3(0, 0.3f, 0), new Vector3(length, 0.03f, 0.05f), SteelDark, turn);
                break;
            }

            case FenceKind.CrowdBarrier:
            {
                var panels = Math.Max(1, (int)MathF.Round(length / 2.3f));
                var panel = length / panels;
                for (var i = 0; i < panels; i++)
                {
                    var start = from + (along * (panel * i));
                    var mid = start + (along * (panel / 2));
                    b.Box(mid + new Vector3(0, 1.08f, 0), new Vector3(panel - 0.06f, 0.05f, 0.05f), Steel, turn);
                    b.Box(mid + new Vector3(0, 0.2f, 0), new Vector3(panel - 0.06f, 0.05f, 0.05f), Steel, turn);
                    for (var bar = 0; bar <= 12; bar++)
                    {
                        var at = start + (along * (0.05f + ((panel - 0.1f) * bar / 12f)));
                        var end = bar is 0 or 12;
                        b.Box(at + new Vector3(0, 0.64f, 0), new Vector3(end ? 0.05f : 0.025f, 0.9f, end ? 0.05f : 0.025f), end ? Steel : SteelDark, turn);
                    }

                    foreach (var foot in new[] { 0.25f, panel - 0.25f })
                    {
                        b.Box(start + (along * foot) + new Vector3(0, 0.1f, 0), new Vector3(0.05f, 0.2f, 0.05f), Steel);
                        b.Box(start + (along * foot) + new Vector3(0, 0.03f, 0), new Vector3(0.06f, 0.05f, 0.6f), Steel, turn);
                    }
                }

                break;
            }

            default:
            {
                var panels = Math.Max(1, (int)MathF.Round(length / 3.5f));
                var panel = length / panels;
                for (var i = 0; i < panels; i++)
                {
                    var start = from + (along * (panel * i));
                    var mid = start + (along * (panel / 2));
                    foreach (var y in new[] { 0.22f, 2.05f })
                    {
                        b.Box(mid + new Vector3(0, y, 0), new Vector3(panel - 0.08f, 0.05f, 0.05f), Steel, turn);
                    }

                    b.Box(mid + new Vector3(0, 1.15f, 0), new Vector3(panel - 0.08f, 0.025f, 0.03f), SteelDark, turn);
                    var bars = (int)MathF.Round(panel / 0.2f);
                    for (var bar = 0; bar <= bars; bar++)
                    {
                        var at = start + (along * (0.04f + ((panel - 0.08f) * bar / bars)));
                        var end = bar == 0 || bar == bars;
                        b.Box(at + new Vector3(0, end ? 1.07f : 1.13f, 0), new Vector3(end ? 0.05f : 0.018f, end ? 2.14f : 1.84f, end ? 0.05f : 0.018f), end ? Steel : SteelDark, turn);
                    }
                }

                // A block under each join.
                for (var i = 0; i <= panels; i++)
                {
                    var at = from + (along * (panel * i));
                    b.Box(at + new Vector3(0, 0.08f, 0), new Vector3(0.24f, 0.16f, 0.7f), Block, turn);
                }

                break;
            }
        }
    }

    /// <summary>A way in, built round the middle of its opening, facing south to meet people arriving.</summary>
    public static Node3D Gate(GateKind kind, string festivalName)
    {
        var node = new Node3D { Name = Catalogue.Of(kind).Name };
        var b = new MeshBuilder();
        if (kind == GateKind.VillageGate)
        {
            // Two tall posts and a painted board across the top.
            const float half = 2f;
            foreach (var x in new[] { -half, half })
            {
                b.Box(new Vector3(x, 1.75f, 0), new Vector3(0.24f, 3.5f, 0.24f), Wood);
                b.Ball(new Vector3(x, 3.6f, 0), 0.34f, Sunflower);
                // A tub of sunflowers at the foot of each post.
                var tub = new Vector3(x + (Math.Sign(x) * 0.55f), 0, 0.5f);
                b.Cone(tub + new Vector3(0, 0.22f, 0), new Vector3(0.6f, 0.44f, 0.6f), WoodDark, new Vector3(180, 0, 0));
                for (var i = 0; i < 3; i++)
                {
                    var stem = tub + new Vector3((i - 1) * 0.16f, 0, (i % 2) * 0.1f);
                    b.Box(stem + new Vector3(0, 0.75f + (i * 0.1f), 0), new Vector3(0.035f, 0.9f, 0.035f), new Color("#4E8B3A"));
                    b.Tube(stem + new Vector3(0, 1.22f + (i * 0.1f), 0.03f), new Vector3(0.3f, 0.04f, 0.3f), Sunflower, new Vector3(75, 0, 0));
                    b.Tube(stem + new Vector3(0, 1.22f + (i * 0.1f), 0.055f), new Vector3(0.13f, 0.04f, 0.13f), WoodDark, new Vector3(75, 0, 0));
                }
            }

            b.Box(new Vector3(0, 3.0f, 0), new Vector3((half * 2) + 0.5f, 0.8f, 0.1f), Pink);
            b.Box(new Vector3(0, 2.56f, 0.01f), new Vector3((half * 2) + 0.5f, 0.1f, 0.12f), Sunflower);
            Painted(node, festivalName, new Vector3(0, 3.03f, 0.07f), 3.9f, 0.5f, Colors.White);
            Bunting(b, new Vector3(-half, 2.45f, 0.05f), new Vector3(half, 2.45f, 0.05f), 0.3f);

            // A trestle table to check tickets at, with a cash tin and a roll of wristbands.
            var table = new Vector3(half + 1.6f, 0, 0.9f);
            b.Box(table + new Vector3(0, 0.76f, 0), new Vector3(1.8f, 0.06f, 0.75f), Canvas);
            b.Box(table + new Vector3(0, 0.55f, 0.36f), new Vector3(1.8f, 0.4f, 0.03f), Pink);
            foreach (var x in new[] { -0.75f, 0.75f })
            {
                b.Box(table + new Vector3(x, 0.37f, 0), new Vector3(0.06f, 0.74f, 0.6f), WoodDark);
            }

            b.Box(table + new Vector3(-0.4f, 0.87f, 0), new Vector3(0.36f, 0.16f, 0.26f), new Color("#3C6E9E"));
            b.Tube(table + new Vector3(0.35f, 0.83f, 0.05f), new Vector3(0.2f, 0.08f, 0.2f), Sunflower);
            // A sandwich board that says where to queue.
            var board = new Vector3(-half - 1.3f, 0, 1.1f);
            b.Box(board + new Vector3(0, 0.55f, 0.12f), new Vector3(0.7f, 1.1f, 0.05f), Black, new Vector3(-12, 0, 0));
            b.Box(board + new Vector3(0, 0.55f, -0.12f), new Vector3(0.7f, 1.1f, 0.05f), Black, new Vector3(12, 0, 0));
            Painted(node, "TICKETS", board + new Vector3(0, 0.72f, 0.19f), 0.6f, 0.2f, Colors.White, -12);
        }
        else
        {
            // A steel arch over two lanes, with a banner across.
            const float half = 3.0f, high = 4.4f;
            Truss(b, new Vector3(-half, 0, 0), new Vector3(-half, high, 0));
            Truss(b, new Vector3(half, 0, 0), new Vector3(half, high, 0));
            Truss(b, new Vector3(-half, high, 0), new Vector3(half, high, 0));
            foreach (var x in new[] { -half, half })
            {
                b.Box(new Vector3(x, 0.04f, 0), new Vector3(0.9f, 0.08f, 0.9f), SteelDark);
            }

            b.Box(new Vector3(0, high - 0.85f, 0.2f), new Vector3((half * 2) - 0.5f, 1.1f, 0.06f), Pink);
            b.Box(new Vector3(0, high - 1.36f, 0.21f), new Vector3((half * 2) - 0.5f, 0.1f, 0.07f), Sunflower);
            Painted(node, festivalName, new Vector3(0, high - 0.8f, 0.25f), 5.2f, 0.62f, Colors.White);
            foreach (var x in new[] { -half, half })
            {
                // A flag on each tower.
                b.Box(new Vector3(x, high + 0.9f, 0), new Vector3(0.05f, 1.6f, 0.05f), Steel);
                b.Box(new Vector3(x + 0.45f, high + 1.4f, 0), new Vector3(0.85f, 0.5f, 0.03f), Sunflower);
            }

            // Barriers that split the way in into two lanes and funnel the queue.
            AddFence(b, FenceKind.CrowdBarrier, new Vector3(0, 0, 0.3f), new Vector3(0, 0, 5.7f));
            AddFence(b, FenceKind.CrowdBarrier, new Vector3(-half + 0.1f, 0, 0.4f), new Vector3(-half + 0.1f, 0, 5f));
            AddFence(b, FenceKind.CrowdBarrier, new Vector3(half - 0.1f, 0, 0.4f), new Vector3(half - 0.1f, 0, 5f));

            // A striped ticket hut beside the arch.
            var hut = new Vector3(half + 2.1f, 0, 0.6f);
            b.Box(hut + new Vector3(0, 1.1f, 0), new Vector3(2.2f, 2.2f, 1.8f), Canvas);
            for (var i = 0; i < 5; i++)
            {
                b.Box(hut + new Vector3(-0.88f + (i * 0.44f), 0.5f, 0.91f), new Vector3(0.22f, 1.0f, 0.02f), Pink);
            }

            b.Box(hut + new Vector3(0, 1.55f, 0.91f), new Vector3(1.5f, 0.8f, 0.03f), new Color("#34424D"));
            b.Box(hut + new Vector3(0, 1.1f, 1.05f), new Vector3(1.7f, 0.07f, 0.34f), Deck);
            b.Box(hut + new Vector3(0, 2.32f, 0.2f), new Vector3(2.5f, 0.1f, 2.5f), Pink, new Vector3(8, 0, 0));
            b.Box(hut + new Vector3(0, 2.72f, 0.95f), new Vector3(1.9f, 0.5f, 0.06f), Sunflower);
            Painted(node, "TICKETS", hut + new Vector3(0, 2.72f, 0.99f), 1.6f, 0.32f, new Color("#1D2B36"));
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A length of square steel truss between two points.</summary>
    private static void Truss(MeshBuilder b, Vector3 from, Vector3 to)
    {
        const float side = 0.34f;
        var run = to - from;
        var length = run.Length();
        var along = run / length;
        // Two directions square to the run.
        var a = MathF.Abs(along.Y) > 0.9f ? Vector3.Right : along.Cross(Vector3.Up).Normalized();
        var c = a.Cross(along).Normalized();
        var basis = new Basis(a, along, c);
        var euler = basis.GetEuler() * (180f / Mathf.Pi);
        var mid = from + (run / 2);
        foreach (var sa in new[] { -1f, 1f })
        {
            foreach (var sc in new[] { -1f, 1f })
            {
                b.Box(mid + (a * sa * side / 2) + (c * sc * side / 2), new Vector3(0.055f, length + side, 0.055f), Steel, euler);
            }
        }

        var rungs = Math.Max(1, (int)MathF.Round(length / 0.5f));
        for (var i = 0; i <= rungs; i++)
        {
            var at = from + (along * (length * i / rungs));
            b.Box(at, new Vector3(side, 0.035f, side), SteelDark, euler);
        }
    }

    private static void Bale(MeshBuilder b, Vector3 centre, bool light, float turn = 0)
    {
        b.Box(centre, new Vector3(1f, 0.44f, 0.5f), light ? Straw : StrawDark, new Vector3(0, turn, 0));
        foreach (var x in new[] { -0.25f, 0.25f })
        {
            var offset = turn == 0 ? new Vector3(x, 0, 0) : new Vector3(0, 0, x);
            b.Box(centre + offset, turn == 0 ? new Vector3(0.03f, 0.46f, 0.52f) : new Vector3(0.52f, 0.46f, 0.03f), new Color("#4E7FB5"));
        }
    }

    private static void Speaker(MeshBuilder b, Vector3 foot, float size, bool onStand)
    {
        var lift = 0f;
        if (onStand)
        {
            lift = 1.1f;
            b.Tube(foot + new Vector3(0, lift / 2, 0), new Vector3(0.06f, lift, 0.06f), SteelDark);
            foreach (var turn in new[] { 0f, 120f, 240f })
            {
                b.Box(foot + new Vector3(0, 0.2f, 0), new Vector3(0.04f, 0.04f, 0.8f), SteelDark, new Vector3(20, turn, 0));
            }
        }

        var centre = foot + new Vector3(0, lift + (size / 2), 0);
        b.Box(centre, new Vector3(size * 0.7f, size, size * 0.6f), Black);
        b.Tube(centre + new Vector3(0, -size * 0.17f, size * 0.3f), new Vector3(size * 0.46f, 0.03f, size * 0.46f), Charcoal, new Vector3(90, 0, 0));
        b.Tube(centre + new Vector3(0, size * 0.27f, size * 0.3f), new Vector3(size * 0.24f, 0.03f, size * 0.24f), Charcoal, new Vector3(90, 0, 0));
    }

    private static void MicStand(MeshBuilder b, Vector3 foot)
    {
        b.Tube(foot + new Vector3(0, 0.02f, 0), new Vector3(0.3f, 0.04f, 0.3f), Black);
        b.Tube(foot + new Vector3(0, 0.72f, 0), new Vector3(0.035f, 1.44f, 0.035f), SteelDark);
        b.Ball(foot + new Vector3(0, 1.48f, 0.04f), 0.1f, Black);
    }

    /// <summary>A sagging line of little flags between two points.</summary>
    private static void Bunting(MeshBuilder b, Vector3 from, Vector3 to, float sag)
    {
        var run = to - from;
        var length = run.Length();
        var count = Math.Max(3, (int)MathF.Round(length / 0.38f));
        var turn = Mathf.RadToDeg(MathF.Atan2(-run.Z, run.X));
        var last = from;
        for (var i = 1; i <= count; i++)
        {
            var t = i / (float)count;
            var at = from + (run * t) + new Vector3(0, -sag * 4 * t * (1 - t), 0);
            var mid = (last + at) / 2;
            var piece = at - last;
            b.Box(mid, new Vector3(piece.Length() + 0.01f, 0.018f, 0.018f), Rope, new Vector3(0, turn, Mathf.RadToDeg(MathF.Asin(piece.Y / piece.Length()))));
            b.Box(mid + new Vector3(0, -0.15f, 0), new Vector3(0.2f, 0.2f, 0.015f), BuntingColours[i % BuntingColours.Length], new Vector3(0, turn, 45));
            last = at;
        }
    }

    /// <summary>Words painted on a board: sized to fit a width, never taller than a given height.</summary>
    private static void Painted(Node3D node, string text, Vector3 at, float width, float height, Color colour, float lean = 0)
    {
        const int fontSize = 96;
        var pixel = Math.Min(height / (fontSize * 0.78f), width / (Math.Max(1, text.Length) * fontSize * 0.7f));
        node.AddChild(new Label3D
        {
            Text = text.ToUpperInvariant(),
            FontSize = fontSize,
            PixelSize = pixel,
            Modulate = colour,
            OutlineSize = 0,
            Position = at,
            RotationDegrees = new Vector3(lean, 0, 0),
            DoubleSided = false,
        });
    }
}
