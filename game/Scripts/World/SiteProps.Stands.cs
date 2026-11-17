using System.Collections.Generic;
using System.Linq;
using BlueprintFestival.Simulation.Site;
using BlueprintFestival.Simulation.World;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The stands: rows of timber tiers with pink, white and sunflower seats, grey standing steps,
/// decks on scaffold, an accessible deck with a ramp, a rail all round with gaps for the steps, a
/// pink back wall on a tiered stand and a striped roof over it.
/// </summary>
public static partial class SiteProps
{
    private static readonly Color StandTimber = new("#D6A26B");
    private static readonly Color StandRiser = new("#B9874F");
    private static readonly Color Concrete = new("#BDB8C2");
    private static readonly Color ConcreteDark = new("#A49EAB");
    private static readonly Color SeatWhite = new("#FBF7F2");

    /// <summary>The model of a stand, drawn where it stands: the node sits at the field's corner.</summary>
    public static Node3D Stand(PlacedStand stand)
    {
        var b = new MeshBuilder();
        const float t = (float)FieldMap.TileMetres;
        var steps = new HashSet<TilePoint>(stand.Steps.SelectMany(s => SiteLayout.StepTilesOf(stand, s)));
        var tiered = Stands.Tiered(stand.Kind);
        var top = (float)Stands.TopOf(stand);
        var seatColours = new[] { new Color("#E94F7B"), SeatWhite, new Color("#F6B93B") };

        // The middle (on the ground) and size across and along the field of the tiles from (u0, v0) to (u1, v1).
        (Vector3 Middle, Vector2 Size) Span(int u0, int v0, int u1, int v1)
        {
            var a = stand.At(u0, v0);
            var c = stand.At(u1, v1);
            var (x0, x1) = (Mathf.Min(a.X, c.X), Mathf.Max(a.X, c.X) + 1);
            var (y0, y1) = (Mathf.Min(a.Y, c.Y), Mathf.Max(a.Y, c.Y) + 1);
            return (new Vector3((x0 + x1) * t / 2, 0, (y0 + y1) * t / 2), new Vector2((x1 - x0) * t, (y1 - y0) * t));
        }

        void Block(int u0, int v0, int u1, int v1, float bottom, float height, Color colour, float inset = 0)
        {
            var (middle, size) = Span(u0, v0, u1, v1);
            b.Box(middle + new Vector3(0, bottom + (height / 2), 0), new Vector3(size.X - inset, height, size.Y - inset), colour);
        }

        var length = stand.Length;
        var depth = stand.Depth;
        if (tiered)
        {
            // One tier for each row, from the ground up, so the end shows the steps.
            for (var row = 0; row < stand.Rows; row++)
            {
                var floor = (float)Stands.FloorOf(stand.Kind, row);
                var v0 = row * Stands.RowTiles;
                var v1 = v0 + Stands.RowTiles - 1;
                var tread = stand.Kind == StandKind.Seated ? (row % 2 == 0 ? StandTimber : StandTimber.Darkened(0.06f)) : (row % 2 == 0 ? Concrete : Concrete.Lightened(0.06f));
                Block(0, v0, length - 1, v1, 0, floor - 0.03f, stand.Kind == StandKind.Seated ? StandRiser : ConcreteDark);
                Block(0, v0, length - 1, v1, floor - 0.03f, 0.03f, tread);
                if (stand.Kind == StandKind.Seated)
                {
                    // A seat on the back half of each row, one for each place.
                    var colour = seatColours[row % seatColours.Length];
                    for (var u = 0; u < length; u++)
                    {
                        Block(u, v1, u, v1, floor, (float)Stands.SeatHeight - 0.05f, colour, 0.12f);
                    }
                }
                else
                {
                    // A yellow line along each step's edge.
                    var (middle, size) = Span(0, v0, length - 1, v0);
                    var along = stand.Along;
                    var (lx, ly) = stand.Looking;
                    b.Box(middle + new Vector3(lx * t * 0.42f, floor + 0.005f, ly * t * 0.42f), along.X != 0 ? new Vector3(size.X, 0.012f, 0.05f) : new Vector3(0.05f, 0.012f, size.Y), new Color("#F2C230"));
                }
            }

            // A pink wall up the back, as high as a rail above the top row.
            Block(0, depth, length - 1, depth, 0, top + 1.05f, new Color("#E94F7B"));
            Block(0, depth, length - 1, depth, top + 1.0f, 0.08f, new Color("#F6B93B"));
        }
        else
        {
            var deck = stand.Kind == StandKind.Deck;
            var height = (float)Stands.FloorOf(stand.Kind, 0);

            // Scaffold legs every two metres, and the deck on top.
            for (var u = 0; u <= length; u += 4)
            {
                for (var v = 0; v <= depth; v += 4)
                {
                    var (middle, _) = Span(Mathf.Min(u, length - 1), Mathf.Min(v, depth - 1), Mathf.Min(u, length - 1), Mathf.Min(v, depth - 1));
                    b.Tube(middle + new Vector3(0, height / 2, 0), new Vector3(0.07f, height, 0.07f), Steel);
                }
            }

            Block(0, 0, length - 1, depth - 1, height - 0.12f, 0.12f, deck ? Deck : Concrete);
            if (!deck)
            {
                // The accessible deck has a blue edge and a wheelchair sign on the front.
                Block(0, 0, length - 1, 0, height - 0.02f, 0.03f, new Color("#3E7FD6"), 0.2f);
            }
        }

        // The rail round the edge, at the height of the floor beside it, open where the steps are.
        var railColour = stand.Use == StandUse.Vip ? VipGold : SteelDark;
        foreach (var p in Tiles(stand.Rect))
        {
            var (u, v) = stand.LocalOf(p);
            var row = Mathf.Clamp(v, 0, depth - 1) / Stands.RowTiles;
            var floor = (float)Stands.FloorOf(stand.Kind, row);
            var (x, z) = ((p.X + 0.5f) * t, (p.Y + 0.5f) * t);
            if (!stand.IsEdge(p))
            {
                continue;
            }

            if (steps.Contains(p))
            {
                // Steps up from the grass: two treads to the floor beside them.
                var up = floor / 2;
                b.Box(new Vector3(x, up / 4, z), new Vector3(t, up / 2, t), ConcreteDark);
                b.Box(new Vector3(x, up * 0.75f, z), new Vector3(t * 0.6f, up / 2, t * 0.6f), Concrete);
                continue;
            }

            if (tiered && v >= depth)
            {
                // The back wall is the rail there.
                continue;
            }

            var alongX = p.Y == stand.Rect.Y || p.Y == stand.Rect.Bottom - 1;
            var corner = alongX && (p.X == stand.Rect.X || p.X == stand.Rect.Right - 1);
            b.Box(new Vector3(x, floor + 1.0f, z), corner ? new Vector3(0.06f, 0.06f, 0.06f) : alongX ? new Vector3(t + 0.01f, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, t + 0.01f), railColour);
            b.Box(new Vector3(x, floor + 0.5f, z), alongX ? new Vector3(t, 0.03f, 0.03f) : new Vector3(0.03f, 0.03f, t), railColour);
            if ((p.X + p.Y) % 2 == 0)
            {
                // A post down to the floor or the grass.
                var bottom = tiered && v < 0 ? 0 : floor;
                b.Box(new Vector3(x, (bottom + floor + 1.0f) / 2, z), new Vector3(0.05f, floor + 1.0f - bottom, 0.05f), railColour);
            }

            if (!tiered && (v < 0 || v >= depth || u < 0 || u >= length))
            {
                // A deck's skirt hides the scaffold on its outer edge.
                b.Box(new Vector3(x, (floor - 0.12f) / 2, z), alongX ? new Vector3(t, floor - 0.12f, 0.03f) : new Vector3(0.03f, floor - 0.12f, t), stand.Use == StandUse.Vip ? VipInk : new Color("#E94F7B"));
            }
        }

        if (stand.Roof != StandRoof.None)
        {
            // Posts up the back, and a striped roof over all of it or the back half.
            var from = stand.Roof == StandRoof.Full ? 0 : depth / 2;
            var eaves = top + 2.6f;
            for (var u = 0; u < length; u += 8)
            {
                var (middle, _) = Span(u, depth, u, depth);
                b.Tube(middle + new Vector3(0, eaves / 2, 0), new Vector3(0.1f, eaves, 0.1f), Charcoal);
            }

            var (endMiddle, _) = Span(length - 1, depth, length - 1, depth);
            b.Tube(endMiddle + new Vector3(0, eaves / 2, 0), new Vector3(0.1f, eaves, 0.1f), Charcoal);
            for (var u = 0; u < length; u += 2)
            {
                var stripe = (u / 2) % 2 == 0 ? new Color("#E94F7B") : SeatWhite;
                var (middle, size) = Span(u, from, Mathf.Min(u + 1, length - 1), depth);
                b.Box(middle + new Vector3(0, eaves, 0), new Vector3(size.X + 0.02f, 0.08f, size.Y + 0.02f), stripe);
            }
        }

        var node = b.BuildNode("Stand");
        node.Name = "Stand";
        return node;
    }

    private static IEnumerable<TilePoint> Tiles(TileRect r)
    {
        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                yield return new TilePoint(x, y);
            }
        }
    }
}
