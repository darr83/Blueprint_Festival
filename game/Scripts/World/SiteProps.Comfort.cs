using System;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Seats, hand washing, shade and the charging tent: the comforts the player dots about the field.</summary>
public static partial class SiteProps
{
    private static readonly Color SoapBlue = new("#4FA8D8");
    private static readonly Color Sand = new("#EAD9A4");
    private static readonly Color SandDark = new("#D9C489");
    private static readonly Color Turf = new("#58B94E");
    private static readonly Color TurfDark = new("#49A541");
    private static readonly Color Pallet = new("#C8A46E");
    private static readonly Color PalletDark = new("#8F7045");
    private static readonly Color Mat = new("#34363B");
    private static readonly Color MatDark = new("#1F2023");
    private static readonly Color Board = new("#B98A55");
    private static readonly Color BoardDark = new("#94693C");

    /// <summary>A comfort built round its own middle, facing south (+Z).</summary>
    public static Node3D Comfort(ComfortKind kind)
    {
        var node = new Node3D { Name = Comforts.Of(kind).Name };
        var b = new MeshBuilder();
        switch (kind)
        {
            case ComfortKind.StrawBale:
                Bale(b, new Vector3(0, 0.22f, 0), true);
                break;
            case ComfortKind.Bench:
                Bench(b);
                break;
            case ComfortKind.Deckchairs:
                for (var i = 0; i < 4; i++)
                {
                    Deckchair(b, (i - 1.5f) * 0.8f, i % 2 == 0 ? Pink : Sunflower);
                }

                break;
            case ComfortKind.PicnicTable:
                PicnicTable(b, 0, 0);
                break;
            case ComfortKind.ParasolTable:
                ParasolTable(b);
                break;
            case ComfortKind.SeatingMarquee:
                SeatingMarquee(b, node);
                break;
            case ComfortKind.HandGel:
                HandGel(b);
                break;
            case ComfortKind.HandWash:
                HandWash(b, node);
                break;
            case ComfortKind.ShadeSail:
                ShadeSail(b);
                break;
            default:
                ChargingTent(b, node);
                break;
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    private static void Bench(MeshBuilder b)
    {
        const float w = 1.8f;
        // Three seat slats and two back slats on a pair of painted iron ends.
        for (var i = 0; i < 3; i++)
        {
            b.Box(new Vector3(0, 0.44f, -0.1f + (i * 0.14f)), new Vector3(w, 0.04f, 0.12f), i == 1 ? Deck : Wood);
        }

        for (var i = 0; i < 2; i++)
        {
            b.Box(new Vector3(0, 0.66f + (i * 0.16f), -0.21f - (i * 0.03f)), new Vector3(w, 0.12f, 0.04f), i == 0 ? Wood : Deck, new Vector3(-10, 0, 0));
        }

        foreach (var x in new[] { (-w / 2) + 0.12f, (w / 2) - 0.12f })
        {
            b.Box(new Vector3(x, 0.21f, 0.16f), new Vector3(0.06f, 0.42f, 0.06f), Pink);
            b.Box(new Vector3(x, 0.45f, -0.23f), new Vector3(0.06f, 0.9f, 0.06f), Pink, new Vector3(-8, 0, 0));
            b.Box(new Vector3(x, 0.4f, 0), new Vector3(0.06f, 0.05f, 0.46f), Pink);
            b.Box(new Vector3(x, 0.6f, 0.02f), new Vector3(0.07f, 0.04f, 0.44f), PinkDark);
            b.Box(new Vector3(x, 0.5f, 0.2f), new Vector3(0.05f, 0.2f, 0.05f), PinkDark);
        }
    }

    private static void Deckchair(MeshBuilder b, float x, Color stripe)
    {
        // A wooden frame leaning back, with a sling of striped canvas.
        foreach (var side in new[] { -0.27f, 0.27f })
        {
            b.Box(new Vector3(x + side, 0.48f, -0.12f), new Vector3(0.04f, 1.15f, 0.04f), Wood, new Vector3(-32, 0, 0));
            b.Box(new Vector3(x + side, 0.26f, 0.08f), new Vector3(0.04f, 0.95f, 0.04f), WoodDark, new Vector3(58, 0, 0));
            b.Box(new Vector3(x + side, 0.3f, -0.3f), new Vector3(0.04f, 0.62f, 0.04f), Wood, new Vector3(14, 0, 0));
        }

        b.Box(new Vector3(x, 0.95f, -0.42f), new Vector3(0.6f, 0.04f, 0.04f), Wood);
        b.Box(new Vector3(x, 0.07f, 0.44f), new Vector3(0.6f, 0.04f, 0.04f), Wood);
        for (var i = 0; i < 5; i++)
        {
            var sx = x - 0.2f + (i * 0.1f);
            var colour = i % 2 == 0 ? stripe : Canvas;
            b.Box(new Vector3(sx, 0.63f, -0.24f), new Vector3(0.1f, 0.7f, 0.015f), colour, new Vector3(-30, 0, 0));
            b.Box(new Vector3(sx, 0.3f, 0.12f), new Vector3(0.1f, 0.5f, 0.015f), colour, new Vector3(-74, 0, 0));
        }
    }

    /// <summary>A picnic table with a bench down each long side, its middle at (x, z).</summary>
    private static void PicnicTable(MeshBuilder b, float x, float z)
    {
        const float w = 1.8f;
        for (var i = 0; i < 5; i++)
        {
            b.Box(new Vector3(x, 0.74f, z - 0.3f + (i * 0.15f)), new Vector3(w, 0.04f, 0.14f), i % 2 == 0 ? Deck : Wood);
        }

        foreach (var side in new[] { -0.68f, 0.68f })
        {
            b.Box(new Vector3(x, 0.44f, z + side - 0.07f), new Vector3(w, 0.04f, 0.13f), Wood);
            b.Box(new Vector3(x, 0.44f, z + side + 0.07f), new Vector3(w, 0.04f, 0.13f), Deck);
        }

        foreach (var end in new[] { -0.65f, 0.65f })
        {
            // An A-frame at each end: two splayed legs, a rail under the seats and one under the top.
            b.Box(new Vector3(x + end, 0.36f, z - 0.42f), new Vector3(0.05f, 0.86f, 0.09f), WoodDark, new Vector3(-28, 0, 0));
            b.Box(new Vector3(x + end, 0.36f, z + 0.42f), new Vector3(0.05f, 0.86f, 0.09f), WoodDark, new Vector3(28, 0, 0));
            b.Box(new Vector3(x + end, 0.4f, z), new Vector3(0.05f, 0.08f, 1.6f), WoodDark);
            b.Box(new Vector3(x + end, 0.7f, z), new Vector3(0.05f, 0.06f, 0.7f), WoodDark);
        }
    }

    private static void ParasolTable(MeshBuilder b)
    {
        // A round table on a pole that carries on up into a big striped parasol.
        b.Tube(new Vector3(0, 0.72f, 0), new Vector3(1.0f, 0.05f, 1.0f), Canvas);
        b.Tube(new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.06f, 0.5f), SteelDark);
        b.Tube(new Vector3(0, 1.2f, 0), new Vector3(0.06f, 2.4f, 0.06f), Steel);
        Parasol(b, new Vector3(0, 2.2f, 0), 2.7f);

        // Four folding chairs, one on each side.
        for (var i = 0; i < 4; i++)
        {
            var turn = i * 90f;
            var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(turn), 0));
            var at = basis * new Vector3(0, 0, -0.8f);
            b.Box(at + new Vector3(0, 0.42f, 0), new Vector3(0.4f, 0.04f, 0.4f), i % 2 == 0 ? Pink : Sunflower, new Vector3(0, turn, 0));
            b.Box(at + (basis * new Vector3(0, 0.68f, -0.2f)), new Vector3(0.4f, 0.26f, 0.03f), i % 2 == 0 ? Pink : Sunflower, new Vector3(0, turn, 0));
            foreach (var lx in new[] { -0.17f, 0.17f })
            {
                b.Box(at + (basis * new Vector3(lx, 0.21f, 0.17f)), new Vector3(0.03f, 0.42f, 0.03f), SteelDark);
                b.Box(at + (basis * new Vector3(lx, 0.4f, -0.2f)), new Vector3(0.03f, 0.8f, 0.03f), SteelDark);
            }
        }
    }

    /// <summary>A parasol's cloth: eight wedges in two colours round a point, with a little cap on top.</summary>
    private static void Parasol(MeshBuilder b, Vector3 rim, float across)
    {
        const int wedges = 8;
        var rise = across * 0.17f;
        var half = across / 2;
        var slope = Mathf.RadToDeg(MathF.Atan2(rise, half));
        var length = MathF.Sqrt((rise * rise) + (half * half));
        var chord = 2 * half * MathF.Tan(MathF.PI / wedges);
        for (var i = 0; i < wedges; i++)
        {
            var turn = i * 360f / wedges;
            var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(turn), 0));
            // Three strips, each narrower than the last, make a wedge that comes to a point.
            for (var strip = 0; strip < 3; strip++)
            {
                var t = (strip + 0.5f) / 3f;
                var centre = rim + (basis * new Vector3(half * (1 - t), rise * t, 0));
                b.Box(centre, new Vector3((length / 3) + 0.02f, 0.025f, (chord * (1 - t)) + 0.05f), i % 2 == 0 ? Pink : Canvas, new Vector3(0, turn, -slope));
            }

            b.Box(rim + (basis * new Vector3(half * 0.98f, -0.07f, 0)), new Vector3(0.02f, 0.14f, chord * 0.98f), i % 2 == 0 ? PinkDark : Sunflower, new Vector3(0, turn, 0));
        }

        b.Ball(rim + new Vector3(0, rise + 0.05f, 0), 0.14f, Sunflower);
    }

    private static void SeatingMarquee(MeshBuilder b, Node3D node)
    {
        const float w = 8f, d = 6f, eaves = 2.3f, ridge = 3.6f;
        // Poles down both long sides and a taller one at each end of the ridge. No walls: people come in from any side.
        foreach (var z in new[] { -d / 2, d / 2 })
        {
            for (var i = 0; i < 4; i++)
            {
                b.Tube(new Vector3((-w / 2) + (i * w / 3), eaves / 2, z), new Vector3(0.09f, eaves, 0.09f), Canvas);
            }
        }

        foreach (var x in new[] { -w / 2, w / 2 })
        {
            b.Tube(new Vector3(x, ridge / 2, 0), new Vector3(0.09f, ridge, 0.09f), Canvas);
        }

        // The roof: striped cloth in two slopes, with a scalloped edge along the eaves.
        var slope = Mathf.RadToDeg(Mathf.Atan2(ridge - eaves, d / 2));
        var length = Mathf.Sqrt(((ridge - eaves) * (ridge - eaves)) + (d * d / 4)) + 0.25f;
        const int stripes = 10;
        for (var i = 0; i < stripes; i++)
        {
            var x = (-w / 2) + ((i + 0.5f) * (w / stripes));
            var colour = i % 2 == 0 ? Pink : Canvas;
            b.Box(new Vector3(x, (eaves + ridge) / 2, -d / 4), new Vector3((w / stripes) + 0.01f, 0.05f, length), colour, new Vector3(-slope, 0, 0));
            b.Box(new Vector3(x, (eaves + ridge) / 2, d / 4), new Vector3((w / stripes) + 0.01f, 0.05f, length), colour, new Vector3(slope, 0, 0));
            foreach (var z in new[] { (-d / 2) - 0.1f, (d / 2) + 0.1f })
            {
                b.Box(new Vector3(x, eaves - 0.16f, z), new Vector3((w / stripes) - 0.04f, 0.26f, 0.03f), i % 2 == 0 ? Sunflower : Canvas);
            }
        }

        // The gable ends are filled in above head height.
        foreach (var x in new[] { -w / 2, w / 2 })
        {
            for (var step = 0; step < 5; step++)
            {
                var t = (step + 0.5f) / 5f;
                b.Box(new Vector3(x, eaves + ((ridge - eaves) * t), 0), new Vector3(0.04f, ((ridge - eaves) / 5) + 0.01f, d * (1 - t)), Canvas);
            }
        }

        b.Box(new Vector3(0, eaves + 0.2f, (d / 2) + 0.14f), new Vector3(2.6f, 0.4f, 0.05f), Sunflower);
        Painted(node, "Sit down", new Vector3(0, eaves + 0.2f, (d / 2) + 0.175f), 2.3f, 0.28f, PinkDark);
        Bunting(b, new Vector3(-w / 2, ridge + 0.1f, 0), new Vector3(w / 2, ridge + 0.1f, 0), 0.25f);
        foreach (var (x, y) in Comforts.MarqueeTables)
        {
            PicnicTable(b, (float)x, (float)y);
        }
    }

    private static void HandGel(MeshBuilder b)
    {
        b.Tube(new Vector3(0, 0.02f, 0), new Vector3(0.36f, 0.04f, 0.36f), SteelDark);
        b.Tube(new Vector3(0, 0.6f, 0), new Vector3(0.06f, 1.2f, 0.06f), Steel);
        b.Box(new Vector3(0, 1.12f, 0.06f), new Vector3(0.2f, 0.3f, 0.12f), Colors.White);
        b.Box(new Vector3(0, 1.14f, 0.125f), new Vector3(0.12f, 0.14f, 0.01f), SoapBlue);
        b.Box(new Vector3(0, 0.95f, 0.1f), new Vector3(0.08f, 0.04f, 0.1f), SoapBlue);
        b.Box(new Vector3(0, 0.84f, 0.1f), new Vector3(0.24f, 0.02f, 0.2f), Steel);
        b.Tube(new Vector3(0, 1.48f, 0), new Vector3(0.34f, 0.34f, 0.03f), SoapBlue, new Vector3(90, 0, 0));
        Drop(b, new Vector3(0, 1.47f, 0.03f), 0.16f);
    }

    /// <summary>A white drop, as on a hand washing sign, drawn flat facing south.</summary>
    private static void Drop(MeshBuilder b, Vector3 at, float size)
    {
        b.Ball(at + new Vector3(0, -size * 0.15f, 0), new Vector3(size * 0.7f, size * 0.7f, 0.02f), Colors.White);
        b.Box(at + new Vector3(0, size * 0.18f, 0), new Vector3(size * 0.36f, size * 0.36f, 0.015f), Colors.White, new Vector3(0, 0, 45));
    }

    private static void HandWash(MeshBuilder b, Node3D node)
    {
        const float w = 1.7f;
        // A steel stand with two basins, a tap over each, and a blue water tank on the back.
        b.Box(new Vector3(0, 0.84f, 0.05f), new Vector3(w, 0.08f, 0.6f), Steel);
        foreach (var x in new[] { (-w / 2) + 0.06f, (w / 2) - 0.06f })
        {
            foreach (var z in new[] { -0.2f, 0.3f })
            {
                b.Box(new Vector3(x, 0.4f, z), new Vector3(0.05f, 0.8f, 0.05f), SteelDark);
            }
        }

        b.Box(new Vector3(0, 0.25f, 0.05f), new Vector3(w - 0.1f, 0.04f, 0.5f), SteelDark);
        b.Box(new Vector3(0, 1.1f, -0.33f), new Vector3(w - 0.2f, 0.75f, 0.3f), SoapBlue);
        b.Box(new Vector3(0, 1.5f, -0.33f), new Vector3(w - 0.28f, 0.06f, 0.24f), SoapBlue.Darkened(0.2f));
        foreach (var x in new[] { -0.42f, 0.42f })
        {
            b.Tube(new Vector3(x, 0.88f, 0.1f), new Vector3(0.5f, 0.03f, 0.4f), SteelDark);
            b.Tube(new Vector3(x, 0.885f, 0.1f), new Vector3(0.42f, 0.03f, 0.32f), new Color("#9ED8F0"));
            b.Tube(new Vector3(x, 1.08f, -0.12f), new Vector3(0.04f, 0.04f, 0.2f), Steel, new Vector3(90, 0, 0));
            b.Tube(new Vector3(x, 1.02f, -0.03f), new Vector3(0.04f, 0.12f, 0.04f), Steel);
            // A soap bottle beside each basin.
            b.Tube(new Vector3(x + 0.33f, 0.97f, 0.22f), new Vector3(0.09f, 0.18f, 0.09f), Pink);
            b.Tube(new Vector3(x + 0.33f, 1.09f, 0.22f), new Vector3(0.03f, 0.06f, 0.03f), Colors.White);
        }

        // A roll of paper towels on a peg, and a sign on a pole.
        b.Tube(new Vector3(0, 1.0f, 0.22f), new Vector3(0.14f, 0.24f, 0.14f), Colors.White);
        b.Tube(new Vector3((w / 2) - 0.1f, 1.1f, -0.36f), new Vector3(0.05f, 2.2f, 0.05f), Steel);
        b.Box(new Vector3((w / 2) - 0.45f, 2.0f, -0.36f), new Vector3(0.95f, 0.42f, 0.04f), SoapBlue);
        Painted(node, "Wash hands", new Vector3((w / 2) - 0.45f, 2.0f, -0.335f), 0.85f, 0.2f, Colors.White);
    }

    private static void ShadeSail(MeshBuilder b)
    {
        const float half = 2.4f;
        // Four poles, two tall and two short on opposite corners, so the cloth twists like a real sail.
        var corners = new[]
        {
            new Vector3(-half, 3.3f, -half), new Vector3(half, 2.5f, -half), new Vector3(half, 3.3f, half), new Vector3(-half, 2.5f, half),
        };
        foreach (var top in corners)
        {
            var lean = new Vector3(Math.Sign(top.X) * 0.25f, 0, Math.Sign(top.Z) * 0.25f);
            var foot = new Vector3(top.X, 0, top.Z) + lean;
            b.Tube((foot + top) / 2 + (lean * 0.02f), new Vector3(0.1f, top.Y + 0.1f, 0.1f), Wood, new Vector3(Mathf.RadToDeg(MathF.Atan2(-lean.Z, top.Y)), 0, Mathf.RadToDeg(MathF.Atan2(lean.X, top.Y))));
            b.Ball(top + (lean * 0.05f), 0.16f, Sunflower);
        }

        // The cloth is drawn as strips from one side to the other, each tilted a little more than the last.
        const int strips = 12;
        for (var i = 0; i < strips; i++)
        {
            var t = (i + 0.5f) / strips;
            var west = corners[0].Lerp(corners[3], t);
            var east = corners[1].Lerp(corners[2], t);
            var run = east - west;
            var inset = 0.25f + (0.5f * MathF.Sin(t * MathF.PI));
            var mid = (west + east) / 2;
            b.Box(mid + new Vector3(0, -0.12f, 0), new Vector3(run.Length() - inset, 0.03f, (2 * half / strips) + 0.02f), i % 2 == 0 ? Sunflower : Canvas,
                new Vector3(0, 0, Mathf.RadToDeg(MathF.Asin(run.Y / run.Length()))));
        }
    }

    private static void ChargingTent(MeshBuilder b, Node3D node)
    {
        const float w = 2.6f, d = 2.4f, eaves = 2.2f;
        Canopy(b, node, w, d, eaves, Sky, Canvas, "Charge your phone", Black, Sunflower);
        Counter(b, w - 0.2f, (d / 2) - 0.35f, Canvas, Sky);

        // A wall of little lockers at the back, each with a light: green for free, pink for taken.
        b.Box(new Vector3(0, 0.95f, (-d / 2) + 0.22f), new Vector3(w - 0.3f, 1.9f, 0.4f), Charcoal);
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                var x = (-(w - 0.3f) / 2) + 0.2f + (column * ((w - 0.7f) / 5));
                var y = 0.25f + (row * 0.36f);
                b.Box(new Vector3(x, y, (-d / 2) + 0.43f), new Vector3(0.32f, 0.3f, 0.02f), SteelDark);
                b.Ball(new Vector3(x + 0.1f, y + 0.08f, (-d / 2) + 0.45f), 0.05f, ((row * 7) + (column * 3)) % 3 == 0 ? Pink : new Color("#7BE06A"));
            }
        }

        // A big phone with a lightning flash on a pole, so the tent can be found from across the field.
        var at = new Vector3((w / 2) + 0.1f, 3.3f, d / 2);
        b.Tube(new Vector3(at.X, at.Y / 2, at.Z), new Vector3(0.07f, at.Y, 0.07f), Steel);
        b.Box(at + new Vector3(0, 0.45f, 0), new Vector3(0.62f, 1.1f, 0.08f), Black);
        b.Box(at + new Vector3(0, 0.47f, 0.045f), new Vector3(0.52f, 0.9f, 0.01f), new Color("#7BE06A"));
        b.Box(at + new Vector3(0.03f, 0.64f, 0.055f), new Vector3(0.11f, 0.42f, 0.01f), Colors.White, new Vector3(0, 0, -28));
        b.Box(at + new Vector3(-0.03f, 0.3f, 0.055f), new Vector3(0.11f, 0.42f, 0.01f), Colors.White, new Vector3(0, 0, -28));
        b.Box(at + new Vector3(0, 0.47f, 0.055f), new Vector3(0.34f, 0.1f, 0.01f), Colors.White, new Vector3(0, 0, 12));
    }

    /// <summary>
    /// One 2 m patch of laid floor: pallets, sand, rubber mats, artificial grass or a boardwalk.
    /// The pick is any whole number that differs from patch to patch, so no two look quite alike.
    /// </summary>
    public static void Flooring(MeshBuilder b, int kind, float x, float z, float size, int pick, bool planksAcross)
    {
        switch (kind)
        {
            case 0:
                // Four pallets, each a little askew: slats on top of three bearers.
                for (var i = 0; i < 4; i++)
                {
                    var px = x + ((i % 2 == 0 ? -1 : 1) * size / 4);
                    var pz = z + ((i < 2 ? -1 : 1) * size / 4);
                    var skew = ((((pick + i) * 37) % 9) - 4) * 0.8f;
                    var side = (size / 2) - 0.06f;
                    foreach (var bearer in new[] { -0.4f, 0f, 0.4f })
                    {
                        b.Box(new Vector3(px, 0.05f, pz) + (Basis.FromEuler(new Vector3(0, Mathf.DegToRad(skew), 0)) * new Vector3(bearer * side, 0, 0)), new Vector3(0.09f, 0.1f, side), PalletDark, new Vector3(0, skew, 0));
                    }

                    for (var slat = 0; slat < 5; slat++)
                    {
                        var offset = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(skew), 0)) * new Vector3(0, 0, (slat - 2) * side / 5.2f);
                        b.Box(new Vector3(px, 0.115f, pz) + offset, new Vector3(side, 0.03f, side / 6.6f), (pick + i + slat) % 4 == 0 ? Pallet.Darkened(0.12f) : Pallet, new Vector3(0, skew, 0));
                    }
                }

                break;
            case 1:
                // Sand: a pale bed with a few darker ripples.
                b.Box(new Vector3(x, 0.03f, z), new Vector3(size, 0.06f, size), Sand);
                for (var i = 0; i < 5; i++)
                {
                    var p = pick + (i * 53);
                    var dx = ((((p * 37) % 100) / 100f) - 0.5f) * (size - 0.7f);
                    var dz = ((((p * 61) % 100) / 100f) - 0.5f) * (size - 0.3f);
                    b.Box(new Vector3(x + dx, 0.062f, z + dz), new Vector3(0.6f, 0.01f, 0.05f), SandDark, new Vector3(0, (p * 7 % 30) - 15, 0));
                }

                break;
            case 2:
                // Rubber mats: four black squares, each with rows of holes.
                for (var i = 0; i < 4; i++)
                {
                    var px = x + ((i % 2 == 0 ? -1 : 1) * size / 4);
                    var pz = z + ((i < 2 ? -1 : 1) * size / 4);
                    b.Box(new Vector3(px, 0.02f, pz), new Vector3((size / 2) - 0.03f, 0.04f, (size / 2) - 0.03f), Mat);
                    for (var hole = 0; hole < 16; hole++)
                    {
                        b.Tube(new Vector3(px - 0.33f + ((hole % 4) * 0.22f), 0.041f, pz - 0.33f + ((hole / 4) * 0.22f)), new Vector3(0.13f, 0.004f, 0.13f), MatDark);
                    }
                }

                break;
            case 3:
                // Artificial grass: too green and too tidy, with a neat stitched edge.
                b.Box(new Vector3(x, 0.02f, z), new Vector3(size, 0.04f, size), Turf);
                b.Box(new Vector3(x, 0.041f, z), new Vector3(size / 2, 0.004f, size), TurfDark);
                break;
            default:
                // A boardwalk: planks across the way people walk, on two long bearers.
                var planks = 8;
                for (var i = 0; i < planks; i++)
                {
                    var along = (-size / 2) + ((i + 0.5f) * size / planks);
                    var colour = (pick + i) % 3 == 0 ? BoardDark : Board;
                    b.Box(planksAcross ? new Vector3(x + along, 0.1f, z) : new Vector3(x, 0.1f, z + along),
                        planksAcross ? new Vector3((size / planks) - 0.02f, 0.04f, size - 0.1f) : new Vector3(size - 0.1f, 0.04f, (size / planks) - 0.02f), colour);
                }

                foreach (var side in new[] { -0.7f, 0.7f })
                {
                    b.Box(planksAcross ? new Vector3(x, 0.04f, z + side) : new Vector3(x + side, 0.04f, z), planksAcross ? new Vector3(size, 0.08f, 0.1f) : new Vector3(0.1f, 0.08f, size), WoodDark);
                }

                break;
        }
    }
}
