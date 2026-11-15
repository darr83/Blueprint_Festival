using System;
using System.Collections.Generic;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>Decorations: things that do nothing but make the field a nicer place to be.</summary>
public static partial class SiteProps
{
    // Written out here: the colours in the other files of this class may not be set yet when these are made.
    private static readonly Color Petal = new("#F6B93B");
    private static readonly Color PetalDark = new("#E09A1F");
    private static readonly Color Stem = new("#4E9A46");
    private static readonly Color Leaf = new("#3F8A3C");
    private static readonly Color Seedhead = new("#5B3A1E");
    private static readonly Color Flame = new("#F28C28");
    private static readonly Color FlameHot = new("#FFD23F");
    private static readonly Color Stone = new("#A9A59B");
    private static readonly Color StoneDark = new("#8B877E");
    private static readonly Color Barrel = new("#9A7648");
    private static readonly Color Hoop = new("#5A5F66");
    private static readonly Color Soil = new("#5B4630");
    private static readonly Color[] Blooms = { new("#E94F7B"), new("#F6B93B"), new("#FFFFFF"), new("#C59BE0"), new("#D9483B") };

    /// <summary>A decoration as it stands, built round its own middle and facing south. A fire pit has a light, "Glow", for after dark.</summary>
    public static Node3D Decoration(PlacedDecor decor)
    {
        switch (decor.Kind)
        {
            case DecorKind.Scarecrow:
                return Scarecrow();
            case DecorKind.FlowerPlanter:
                return FlowerPlanter((decor.X * 3) + decor.Y);
            case DecorKind.FlagPole:
                return FlagPole(((decor.X + decor.Y) & 1) == 0 ? Pink : Sunflower);
            case DecorKind.FirePit:
            {
                var pit = FirePit(true);
                pit.AddChild(new OmniLight3D { Name = "Glow", LightColor = new Color("#FFB45A"), LightEnergy = 0, OmniRange = 7f, Position = new Vector3(0, 0.9f, 0), ShadowEnabled = false });
                return pit;
            }

            case DecorKind.GiantSunflower:
                return GiantSunflower();
            default:
                return GiantLetters(decor.Word ?? "FESTIVAL");
        }
    }

    /// <summary>A line of bunting between its two poles, in the field's own metres.</summary>
    public static Node3D BuntingBetween(BuntingRun run)
    {
        const float t = (float)FieldMap.TileMetres;
        var from = new Vector3((run.From.X + 0.5f) * t, 0, (run.From.Y + 0.5f) * t);
        var to = new Vector3((run.To.X + 0.5f) * t, 0, (run.To.Y + 0.5f) * t);
        var node = new Node3D { Name = "Bunting" };
        var b = new MeshBuilder();
        foreach (var at in new[] { from, to })
        {
            b.Tube(at + new Vector3(0, 1.5f, 0), new Vector3(0.07f, 3f, 0.07f), Wood);
            b.Ball(at + new Vector3(0, 3.04f, 0), 0.13f, Sunflower);
        }

        Bunting(b, from + new Vector3(0, 2.9f, 0), to + new Vector3(0, 2.9f, 0), 0.35f);
        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>The 26 letters and 10 digits as five-by-seven blocks, a row to each five characters, top row first.</summary>
    private static readonly Dictionary<char, string[]> BlockFont = new()
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
        ['C'] = new[] { ".####", "#....", "#....", "#....", "#....", "#....", ".####" },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
        ['G'] = new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".###." },
        ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
        ['J'] = new[] { "..###", "....#", "....#", "....#", "....#", "#...#", ".###." },
        ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "##..#", "#.#.#", "#.#.#", "#..##", "#...#", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
        ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" },
        ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
        ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
        ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
        ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['3'] = new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." },
        ['4'] = new[] { "#...#", "#...#", "#...#", "#####", "....#", "....#", "....#" },
        ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
        ['6'] = new[] { ".###.", "#....", "####.", "#...#", "#...#", "#...#", ".###." },
        ['7'] = new[] { "#####", "....#", "...#.", "..#..", "..#..", "..#..", "..#.." },
        ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
        ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." },
    };

    /// <summary>How wide a word in giant letters stands, in metres.</summary>
    public static float LettersWidth(string word) => (Math.Max(1, word.Length) * 6 * LetterBlock) - LetterBlock;

    private const float LetterBlock = 0.24f;

    /// <summary>A word in giant block letters, taller than a person, pink and yellow turn about, on a low plinth.</summary>
    public static Node3D GiantLetters(string word)
    {
        var node = new Node3D { Name = "Giant letters" };
        var b = new MeshBuilder();
        word = word.ToUpperInvariant();
        var width = LettersWidth(word);
        const float s = LetterBlock;
        b.Box(new Vector3(0, 0.09f, 0), new Vector3(width + 0.3f, 0.18f, 0.9f), Canvas);
        b.Box(new Vector3(0, 0.02f, 0), new Vector3(width + 0.5f, 0.04f, 1.1f), Block);
        for (var i = 0; i < word.Length; i++)
        {
            if (!BlockFont.TryGetValue(word[i], out var rows))
            {
                continue;
            }

            var colour = i % 2 == 0 ? Pink : Sunflower;
            var shade = i % 2 == 0 ? PinkDark : PetalDark;
            var left = (-width / 2) + (i * 6 * s) + (s / 2);
            for (var r = 0; r < 7; r++)
            {
                // Runs of blocks along a row are one long box.
                var c = 0;
                while (c < 5)
                {
                    if (rows[r][c] != '#')
                    {
                        c++;
                        continue;
                    }

                    var start = c;
                    while (c < 5 && rows[r][c] == '#')
                    {
                        c++;
                    }

                    var run = c - start;
                    var x = left + ((start + ((run - 1) / 2f)) * s) + (s / 2);
                    var y = 0.18f + ((6 - r) * s) + (s / 2);
                    b.Box(new Vector3(x, y, 0), new Vector3(run * s, s, 0.5f), colour);
                    b.Box(new Vector3(x, y, 0), new Vector3((run * s) - 0.04f, s - 0.04f, 0.54f), shade);
                }
            }
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A sunflower four metres tall.</summary>
    public static Node3D GiantSunflower()
    {
        var node = new Node3D { Name = "Giant sunflower" };
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0.08f, 0), new Vector3(0.9f, 0.16f, 0.9f), Soil);
        b.Tube(new Vector3(0, 1.8f, 0), new Vector3(0.16f, 3.6f, 0.16f), Stem);
        b.Ball(new Vector3(0.42f, 1.5f, 0), new Vector3(0.8f, 0.08f, 0.4f), Leaf, new Vector3(0, 0, 24));
        b.Ball(new Vector3(-0.42f, 2.1f, 0), new Vector3(0.8f, 0.08f, 0.4f), Leaf, new Vector3(0, 0, -24));
        b.Ball(new Vector3(0.3f, 2.7f, 0.1f), new Vector3(0.55f, 0.07f, 0.3f), Leaf, new Vector3(0, 30, 28));
        var head = new Vector3(0, 3.7f, 0.12f);
        for (var i = 0; i < 16; i++)
        {
            var turn = i * 22.5f;
            var a = Mathf.DegToRad(turn);
            b.Ball(head + new Vector3(MathF.Sin(a) * 0.62f, MathF.Cos(a) * 0.62f, i % 2 == 0 ? 0 : -0.03f), new Vector3(0.26f, 0.56f, 0.06f), i % 2 == 0 ? Petal : PetalDark, new Vector3(0, 0, -turn));
        }

        b.Tube(head, new Vector3(0.9f, 0.14f, 0.9f), Seedhead, new Vector3(90, 0, 0));
        b.Tube(head + new Vector3(0, 0, 0.03f), new Vector3(0.5f, 0.14f, 0.5f), new Color("#3F2713"), new Vector3(90, 0, 0));
        b.Ball(head + new Vector3(0, 0, -0.1f), new Vector3(0.7f, 0.7f, 0.2f), Stem);
        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A ring of stones with logs burning in it. The flames are on a node of their own, "Flames", so they can be lit or put out.</summary>
    public static Node3D FirePit(bool lit)
    {
        var node = new Node3D { Name = "Fire pit" };
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0.02f, 0), new Vector3(1.5f, 0.04f, 1.5f), Soil);
        for (var i = 0; i < 10; i++)
        {
            var a = i * MathF.Tau / 10;
            b.Ball(new Vector3(MathF.Sin(a) * 0.68f, 0.12f, MathF.Cos(a) * 0.68f), new Vector3(0.34f, 0.26f, 0.3f), i % 2 == 0 ? Stone : StoneDark, new Vector3(0, Mathf.RadToDeg(a), 0));
        }

        for (var i = 0; i < 3; i++)
        {
            b.Tube(new Vector3(0, 0.2f, 0), new Vector3(0.14f, 0.9f, 0.14f), WoodDark, new Vector3(68, i * 60, 0));
        }

        node.AddChild(b.BuildNode("Shapes"));
        var flames = new MeshBuilder();
        // Tongues of flame: tall thin drops, hot in the middle.
        foreach (var (x, z, tall, wide, hot) in new[] { (0f, 0f, 0.95f, 0.3f, false), (0.16f, 0.06f, 0.7f, 0.24f, false), (-0.15f, -0.08f, 0.62f, 0.22f, false), (-0.04f, 0.16f, 0.55f, 0.2f, false), (0.02f, 0.02f, 0.6f, 0.16f, true), (0.12f, -0.1f, 0.42f, 0.13f, true), (-0.1f, 0.06f, 0.4f, 0.12f, true) })
        {
            flames.Ball(new Vector3(x, 0.26f + (tall * 0.36f), z), new Vector3(wide, tall * 0.75f, wide), hot ? FlameHot : Flame);
            flames.Cone(new Vector3(x, 0.26f + (tall * 0.74f), z), new Vector3(wide * 0.8f, tall * 0.55f, wide * 0.8f), hot ? FlameHot : Flame);
        }

        var fire = flames.BuildNode("Flames");
        // Flames give their own light: they do not go dark at night.
        fire.MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true };
        fire.Visible = lit;
        node.AddChild(fire);
        return node;
    }

    /// <summary>A flag on a tall pole.</summary>
    public static Node3D FlagPole(Color flag)
    {
        var node = new Node3D { Name = "Flag pole" };
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0.05f, 0), new Vector3(0.44f, 0.1f, 0.44f), Block);
        b.Tube(new Vector3(0, 2.6f, 0), new Vector3(0.08f, 5.2f, 0.08f), Canvas);
        b.Ball(new Vector3(0, 5.26f, 0), 0.18f, Sunflower);
        // A long pennant in three steps, as if it were lifting in the wind.
        for (var i = 0; i < 4; i++)
        {
            b.Box(new Vector3(0.3f + (i * 0.42f), 4.75f - (i * 0.03f), 0.02f * (i % 2 == 0 ? 1 : -1)), new Vector3(0.44f, 0.7f - (i * 0.15f), 0.03f), i % 2 == 0 ? flag : Canvas, new Vector3(0, i % 2 == 0 ? 8 : -8, -4));
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>Half a barrel full of flowers.</summary>
    public static Node3D FlowerPlanter(int pick)
    {
        var node = new Node3D { Name = "Flower planter" };
        var b = new MeshBuilder();
        b.Tube(new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.9f), Barrel);
        b.Tube(new Vector3(0, 0.12f, 0), new Vector3(0.93f, 0.05f, 0.93f), Hoop);
        b.Tube(new Vector3(0, 0.4f, 0), new Vector3(0.93f, 0.05f, 0.93f), Hoop);
        b.Tube(new Vector3(0, 0.5f, 0), new Vector3(0.8f, 0.04f, 0.8f), Soil);
        for (var i = 0; i < 9; i++)
        {
            var a = i * 2.4f;
            var r = 0.1f + (0.26f * ((i * 7 % 5) / 4f));
            var at = new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r);
            var tall = 0.22f + (0.16f * ((i * 3 % 4) / 3f));
            b.Tube(at + new Vector3(0, 0.5f + (tall / 2), 0), new Vector3(0.03f, tall, 0.03f), Stem);
            b.Ball(at + new Vector3(0, 0.52f + tall, 0), 0.2f, Blooms[(i + pick) % Blooms.Length]);
            b.Ball(at + new Vector3(0, 0.56f, 0), new Vector3(0.24f, 0.1f, 0.24f), Leaf);
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A scarecrow in a checked shirt and a floppy hat, arms out.</summary>
    public static Node3D Scarecrow()
    {
        var node = new Node3D { Name = "Scarecrow" };
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 1.0f, 0), new Vector3(0.08f, 2.0f, 0.08f), WoodDark);
        b.Box(new Vector3(0, 1.45f, 0), new Vector3(1.6f, 0.07f, 0.07f), WoodDark);
        // Shirt and sleeves, with a few patches.
        b.Box(new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.7f, 0.3f), new Color("#D9483B"));
        b.Box(new Vector3(-0.5f, 1.45f, 0), new Vector3(0.55f, 0.2f, 0.22f), new Color("#D9483B"));
        b.Box(new Vector3(0.5f, 1.45f, 0), new Vector3(0.55f, 0.2f, 0.22f), new Color("#D9483B"));
        b.Box(new Vector3(0.1f, 1.25f, 0.155f), new Vector3(0.16f, 0.16f, 0.01f), Sunflower);
        b.Box(new Vector3(-0.12f, 1.02f, 0.155f), new Vector3(0.12f, 0.12f, 0.01f), new Color("#6FC3E8"));
        // Trousers, and straw sticking out at the cuffs.
        b.Box(new Vector3(-0.12f, 0.62f, 0), new Vector3(0.2f, 0.5f, 0.24f), new Color("#3F5E8C"));
        b.Box(new Vector3(0.12f, 0.62f, 0), new Vector3(0.2f, 0.5f, 0.24f), new Color("#3F5E8C"));
        foreach (var x in new[] { -0.82f, 0.82f })
        {
            b.Cone(new Vector3(x, 1.45f, 0), new Vector3(0.2f, 0.22f, 0.2f), Straw, new Vector3(0, 0, x < 0 ? 90 : -90));
        }

        foreach (var x in new[] { -0.12f, 0.12f })
        {
            b.Cone(new Vector3(x, 0.3f, 0), new Vector3(0.2f, 0.2f, 0.2f), Straw, new Vector3(180, 0, 0));
        }

        // A sack head with button eyes, and a hat.
        b.Ball(new Vector3(0, 1.78f, 0), new Vector3(0.42f, 0.44f, 0.4f), new Color("#E6CF9A"));
        b.Ball(new Vector3(-0.08f, 1.82f, 0.19f), 0.06f, Black);
        b.Ball(new Vector3(0.08f, 1.82f, 0.19f), 0.06f, Black);
        b.Cone(new Vector3(0, 1.75f, 0.24f), new Vector3(0.07f, 0.16f, 0.07f), Flame, new Vector3(90, 0, 0));
        b.Tube(new Vector3(0, 1.97f, 0), new Vector3(0.7f, 0.04f, 0.7f), StrawDark, new Vector3(6, 0, 4));
        b.Cone(new Vector3(0, 2.14f, 0), new Vector3(0.36f, 0.34f, 0.36f), StrawDark, new Vector3(6, 0, 4));
        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    /// <summary>A line of bunting between two poles, a given number of metres apart, running east from the first pole.</summary>
    public static Node3D BuntingLine(float length)
    {
        var node = new Node3D { Name = "Bunting" };
        var b = new MeshBuilder();
        foreach (var x in new[] { 0f, length })
        {
            b.Tube(new Vector3(x, 1.5f, 0), new Vector3(0.07f, 3f, 0.07f), Wood);
            b.Ball(new Vector3(x, 3.04f, 0), 0.13f, Sunflower);
        }

        Bunting(b, new Vector3(0, 2.9f, 0), new Vector3(length, 2.9f, 0), 0.35f);
        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }
}
