using System;
using BlueprintFestival.Simulation.Site;
using Godot;

namespace BlueprintFestival.World;

/// <summary>
/// The fairground rides and the hook a duck stall. Each is built round its own middle with its
/// front to the south. The parts that move are nodes of their own ("Wheel", "Turntable", "Car3"...)
/// and <see cref="TurnRide"/> puts them where they are at a moment of the ride. Every place a
/// rider sits is a node called "Seat...": the person sits at it, facing its +Z.
/// </summary>
public static partial class SiteProps
{
    // Written out here: the colours in the other files of this class may not be set yet when these are made.
    private static readonly Color FairPink = new("#E94F7B");
    private static readonly Color FairPinkDark = new("#C73A64");
    private static readonly Color FairYellow = new("#F6B93B");
    private static readonly Color FairCream = new("#FFF6DD");
    private static readonly Color FairTeal = new("#2FB7A8");
    private static readonly Color FairLilac = new("#C59BE0");
    private static readonly Color FairSky = new("#6FC3E8");
    private static readonly Color FairDeck = new("#B8946A");
    private static readonly Color FairSteel = new("#8C949B");
    private static readonly Color FairDark = new("#3A3F46");
    private static readonly Color DuckYellow = new("#FFD23F");
    private static readonly Color DuckBeak = new("#F28C28");
    private static readonly Color[] CarColours = { FairPink, FairYellow, FairTeal, FairLilac };
    private static readonly Color[] TeddyFurs = { new("#C98F4E"), new("#F29BBB"), new("#F3E3C3"), new("#8E6B4A"), new("#9CCBE0") };

    /// <summary>How tall the big wheel's hub stands, and how far out its cars hang.</summary>
    private const float WheelHub = 10.7f;
    private const float WheelRadius = 7.6f;

    /// <summary>How tall the Skylark's tower stands to its hub, and how long each half of its arm is.</summary>
    private const float SkylarkHub = 14.4f;
    private const float SkylarkArm = 11.4f;

    /// <summary>A fairground ride or the hook a duck stall, as it stands.</summary>
    public static Node3D Ride(StallKind kind)
    {
        var ride = kind switch
        {
            StallKind.BigWheel => BigWheel(),
            StallKind.Waltzer => Waltzer(),
            StallKind.Twister => Twister(),
            StallKind.Carousel => Carousel(),
            StallKind.Skylark => Skylark(),
            _ => HookADuck(),
        };

        // Steps and pay boxes stick out at the front: slide the ride back so the whole of it sits on its ground.
        var slide = RideSlide(kind);
        if (slide == 0)
        {
            return ride;
        }

        var node = new Node3D { Name = ride.Name };
        ride.Name = "Ride";
        ride.Position = new Vector3(0, 0, slide);
        node.AddChild(ride);
        return node;
    }

    private static float RideSlide(StallKind kind) => kind switch
    {
        StallKind.BigWheel => -0.9f,
        StallKind.Skylark => -0.6f,
        StallKind.Waltzer => -0.2f,
        _ => 0,
    };

    /// <summary>Where the attendant stands, in the pay box (beside the stall, at hook a duck).</summary>
    private static Vector3[]? RideStaff(StallKind kind) => kind switch
    {
        StallKind.BigWheel => new[] { new Vector3(-7.6f, 0.8f, 1.5f + RideSlide(kind)) },
        StallKind.Waltzer => new[] { new Vector3(0, 0.75f, 1.6f + RideSlide(kind)) },
        StallKind.Twister => new[] { new Vector3(2.85f, 0, 6.72f) },
        StallKind.Carousel => new[] { new Vector3(-4.6f, 0, 4.4f) },
        StallKind.Skylark => new[] { new Vector3(-4.9f, 0.9f, -1.9f + RideSlide(kind)) },
        StallKind.HookADuck => new[] { new Vector3(-2.75f, 0, 2.0f) },
        _ => null,
    };

    /// <summary>Every place to sit on a ride, in the order the rules number them: four to each party.</summary>
    public static void RideSeats(Node node, System.Collections.Generic.List<Node3D> found)
    {
        foreach (var child in node.GetChildren())
        {
            var name = child.Name.ToString();
            if (child is Node3D seat && name.StartsWith("Seat", StringComparison.Ordinal) && !name.StartsWith("Seats", StringComparison.Ordinal))
            {
                found.Add(seat);
            }
            else
            {
                RideSeats(child, found);
            }
        }
    }

    /// <summary>
    /// Puts a ride's moving parts where they are a number of seconds into a ride. The same seconds
    /// always give the same picture, so the ride can be drawn from the game clock.
    /// </summary>
    public static void TurnRide(Node3D ride, StallKind kind, float seconds)
    {
        switch (kind)
        {
            case StallKind.BigWheel:
            {
                var turn = seconds * 0.22f;
                if (ride.FindChild("Wheel", true, false) is Node3D wheel)
                {
                    wheel.Rotation = new Vector3(0, 0, turn);
                    foreach (var child in wheel.GetChildren())
                    {
                        if (child is Node3D car && car.Name.ToString().StartsWith("Car", StringComparison.Ordinal))
                        {
                            car.Rotation = new Vector3(0, 0, -turn);
                        }
                    }
                }

                break;
            }

            case StallKind.Carousel:
            {
                if (ride.FindChild("Turntable", true, false) is Node3D table)
                {
                    table.Rotation = new Vector3(0, -seconds * 0.55f, 0);
                    var i = 0;
                    foreach (var child in table.GetChildren())
                    {
                        if (child is Node3D horse && horse.Name.ToString().StartsWith("Horse", StringComparison.Ordinal) && horse.GetNodeOrNull<Node3D>("Body") is { } body)
                        {
                            body.Position = new Vector3(0, 0.22f * MathF.Sin((seconds * 2.2f) + (i * 2.4f)), 0);
                            i++;
                        }
                    }
                }

                break;
            }

            case StallKind.Waltzer:
            {
                if (ride.FindChild("Turntable", true, false) is Node3D table)
                {
                    table.Rotation = new Vector3(0, seconds * 0.8f, 0);
                    var i = 0;
                    foreach (var child in table.GetChildren())
                    {
                        if (child is Node3D car && car.Name.ToString().StartsWith("Car", StringComparison.Ordinal))
                        {
                            // Each car spins at its own rate, some one way and some the other.
                            car.Rotation = new Vector3(0, (i * 1.9f) + (seconds * (2.2f + (0.5f * (i % 3))) * (i % 2 == 0 ? 1 : -1)), 0);
                            i++;
                        }
                    }
                }

                break;
            }

            case StallKind.Twister:
            {
                if (ride.FindChild("Spinner", true, false) is Node3D spinner)
                {
                    spinner.Rotation = new Vector3(0, seconds * 0.9f, 0);
                    foreach (var child in spinner.GetChildren())
                    {
                        if (child is Node3D cluster && cluster.Name.ToString().StartsWith("Cluster", StringComparison.Ordinal))
                        {
                            cluster.Rotation = new Vector3(0, -seconds * 2.3f, 0);
                        }
                    }
                }

                break;
            }

            case StallKind.Skylark:
            {
                if (ride.FindChild("Arm", true, false) is Node3D arm)
                {
                    var turn = seconds * 1.1f;
                    arm.Rotation = new Vector3(0, 0, turn);
                    foreach (var child in arm.GetChildren())
                    {
                        if (child is Node3D seats && seats.Name.ToString().StartsWith("Seats", StringComparison.Ordinal))
                        {
                            // The seats hang level, swinging a little behind the arm.
                            seats.Rotation = new Vector3(0, 0, -turn + (0.25f * MathF.Sin(turn)));
                        }
                    }
                }

                break;
            }
        }
    }

    // ---- shared pieces ----

    /// <summary>A straight square bar between two points.</summary>
    private static void Beam(MeshBuilder b, Vector3 from, Vector3 to, float thick, Color colour, bool round = false)
    {
        var run = to - from;
        var length = run.Length();
        if (length < 0.001f)
        {
            return;
        }

        var along = run / length;
        var a = MathF.Abs(along.Y) > 0.99f ? Vector3.Right : along.Cross(Vector3.Up).Normalized();
        var c = a.Cross(along).Normalized();
        var euler = new Basis(a, along, c).GetEuler() * (180f / Mathf.Pi);
        if (round)
        {
            b.Tube(from + (run / 2), new Vector3(thick, length, thick), colour, euler);
        }
        else
        {
            b.Box(from + (run / 2), new Vector3(thick, length, thick), colour, euler);
        }
    }

    /// <summary>A round tent roof in two colours turn about, from its eaves up to a point.</summary>
    private static void RoundRoof(MeshBuilder b, Vector3 eaves, float radius, float rise, int panels, Color one, Color two)
    {
        var top = eaves + new Vector3(0, rise, 0);
        for (var i = 0; i < panels; i++)
        {
            var from = Mathf.Tau * i / panels;
            var to = Mathf.Tau * (i + 1) / panels;
            b.Triangle(
                top,
                eaves + new Vector3(radius * MathF.Cos(from), 0, radius * MathF.Sin(from)),
                eaves + new Vector3(radius * MathF.Cos(to), 0, radius * MathF.Sin(to)),
                i % 2 == 0 ? one : two);
        }
    }

    /// <summary>A flat round floor painted in wedges of two colours.</summary>
    private static void PaintedFloor(MeshBuilder b, Vector3 centre, float radius, int wedges, Color one, Color two)
    {
        for (var i = 0; i < wedges; i++)
        {
            var from = Mathf.Tau * i / wedges;
            var to = Mathf.Tau * (i + 1) / wedges;
            b.Triangle(
                centre,
                centre + new Vector3(radius * MathF.Cos(from), 0, radius * MathF.Sin(from)),
                centre + new Vector3(radius * MathF.Cos(to), 0, radius * MathF.Sin(to)),
                i % 2 == 0 ? one : two);
        }
    }

    /// <summary>A ring of boards hanging under a round roof's edge, in two colours, with a bulb under each join.</summary>
    private static void Valance(MeshBuilder b, Vector3 centre, float radius, float height, int boards, Color one, Color two)
    {
        var width = (Mathf.Tau * radius / boards) + 0.02f;
        for (var i = 0; i < boards; i++)
        {
            var a = Mathf.Tau * (i + 0.5f) / boards;
            var at = centre + new Vector3(radius * MathF.Cos(a), 0, radius * MathF.Sin(a));
            b.Box(at, new Vector3(width, height, 0.06f), i % 2 == 0 ? one : two, new Vector3(0, 90 - Mathf.RadToDeg(a), 0));
            var j = Mathf.Tau * i / boards;
            b.Ball(centre + new Vector3((radius + 0.04f) * MathF.Cos(j), -(height / 2) - 0.04f, (radius + 0.04f) * MathF.Sin(j)), 0.16f, i % 2 == 0 ? FairYellow : FairCream);
        }
    }

    /// <summary>A row of fairground bulbs between two points.</summary>
    private static void Bulbs(MeshBuilder b, Vector3 from, Vector3 to, int count, float size = 0.14f)
    {
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? 0.5f : i / (float)(count - 1);
            b.Ball(from.Lerp(to, t), size, i % 2 == 0 ? FairYellow : FairCream);
        }
    }

    /// <summary>The festival's sunflower as a flat badge facing south: a dark middle and a ring of petals.</summary>
    private static void SunflowerBadge(MeshBuilder b, Vector3 centre, float size, float turn = 0)
    {
        var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(turn), 0));
        const int petals = 12;
        for (var i = 0; i < petals; i++)
        {
            var a = Mathf.Tau * i / petals;
            var at = centre + (basis * new Vector3(size * 0.36f * MathF.Cos(a), size * 0.36f * MathF.Sin(a), 0));
            b.Ball(at, new Vector3(size * 0.3f, size * 0.15f, 0.05f), i % 2 == 0 ? FairYellow : new Color("#E09A1F"), new Vector3(0, turn, Mathf.RadToDeg(a)));
        }

        b.Ball(centre + (basis * new Vector3(0, 0, 0.02f)), new Vector3(size * 0.46f, size * 0.46f, 0.09f), new Color("#5B3A1E"), new Vector3(0, turn, 0));
    }

    /// <summary>A little lark in flight, flat on a board facing south.</summary>
    private static void LarkBadge(MeshBuilder b, Vector3 centre, float size, bool flip)
    {
        var s = flip ? -1f : 1f;
        var brown = new Color("#8A6A45");
        b.Ball(centre, new Vector3(size * 0.6f, size * 0.26f, 0.06f), brown, new Vector3(0, 0, 12 * s));
        b.Ball(centre + new Vector3(size * 0.3f * s, size * 0.1f, 0.01f), new Vector3(size * 0.22f, size * 0.2f, 0.06f), brown);
        b.Ball(centre + new Vector3(0, -size * 0.03f, 0.03f), new Vector3(size * 0.4f, size * 0.14f, 0.04f), FairCream, new Vector3(0, 0, 12 * s));
        b.Triangle(centre + new Vector3(-size * 0.1f * s, size * 0.05f, 0.05f), centre + new Vector3(size * 0.12f * s, size * 0.05f, 0.05f), centre + new Vector3(-size * 0.2f * s, size * 0.5f, 0.05f), new Color("#A5835A"));
        b.Triangle(centre + new Vector3(-size * 0.25f * s, 0, 0.04f), centre + new Vector3(-size * 0.5f * s, size * 0.1f, 0.04f), centre + new Vector3(-size * 0.5f * s, -size * 0.1f, 0.04f), brown);
        b.Triangle(centre + new Vector3(size * 0.4f * s, size * 0.12f, 0.04f), centre + new Vector3(size * 0.52f * s, size * 0.08f, 0.04f), centre + new Vector3(size * 0.4f * s, size * 0.04f, 0.04f), FairYellow);
    }

    /// <summary>The booth where the ride's attendant takes the money.</summary>
    private static void PayBox(MeshBuilder b, Node3D node, Vector3 foot)
    {
        b.Box(foot + new Vector3(0, 0.55f, 0), new Vector3(1.3f, 1.1f, 1.3f), FairPink);
        b.Box(foot + new Vector3(0, 1.12f, 0), new Vector3(1.42f, 0.06f, 1.42f), FairCream);
        b.Box(foot + new Vector3(0, 1.6f, -0.6f), new Vector3(1.3f, 1.0f, 0.08f), FairPinkDark);
        foreach (var x in new[] { -0.6f, 0.6f })
        {
            foreach (var z in new[] { -0.6f, 0.6f })
            {
                b.Box(foot + new Vector3(x, 1.6f, z), new Vector3(0.08f, 1.0f, 0.08f), FairCream);
            }
        }

        b.Cone(foot + new Vector3(0, 2.32f, 0), new Vector3(1.9f, 0.45f, 1.9f), FairYellow);
        b.Box(foot + new Vector3(0, 2.1f, 0.7f), new Vector3(1.3f, 0.3f, 0.05f), FairCream);
        Painted(node, "Pay here", foot + new Vector3(0, 2.1f, 0.73f), 1.2f, 0.22f, FairPinkDark);
        SunflowerBadge(b, foot + new Vector3(0, 0.58f, 0.66f), 0.7f);
    }

    /// <summary>A flight of steps up to a platform, rising to the north.</summary>
    private static void Steps(MeshBuilder b, Vector3 foot, float width, float height, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var rise = height * (i + 1) / (count + 1);
            b.Box(foot + new Vector3(0, rise / 2, -0.3f * i), new Vector3(width, rise, 0.3f), i % 2 == 0 ? FairSteel : FairDark);
        }
    }

    /// <summary>A handrail on posts between two points.</summary>
    private static void Rail(MeshBuilder b, Vector3 from, Vector3 to, Color posts, float height = 1.05f)
    {
        var run = to - from;
        var count = Math.Max(1, (int)MathF.Round(run.Length() / 1.3f));
        for (var i = 0; i <= count; i++)
        {
            var at = from + (run * (i / (float)count));
            b.Box(at + new Vector3(0, height / 2, 0), new Vector3(0.07f, height, 0.07f), posts);
        }

        Beam(b, from + new Vector3(0, height, 0), to + new Vector3(0, height, 0), 0.07f, FairCream);
        Beam(b, from + new Vector3(0, height * 0.55f, 0), to + new Vector3(0, height * 0.55f, 0), 0.05f, FairCream);
    }

    /// <summary>A place for one rider: they sit at it, facing its +Z.</summary>
    private static Node3D Seat(string name, Vector3 at, float turn = 0) => new() { Name = name, Position = at, RotationDegrees = new Vector3(0, turn, 0) };

    // ---- the big wheel ----

    private static Node3D BigWheel()
    {
        var node = new Node3D { Name = "Big wheel" };
        var b = new MeshBuilder();
        const float deck = 0.8f;
        const float hub = WheelHub;
        const float r = WheelRadius;

        // The platform, its painted front and the steps up.
        b.Box(new Vector3(0, deck / 2, 0), new Vector3(17f, deck, 5.2f), FairDeck);
        b.Box(new Vector3(0, deck / 2, 2.62f), new Vector3(17.1f, deck, 0.06f), FairPink);
        b.Box(new Vector3(0, deck - 0.06f, 2.64f), new Vector3(17.1f, 0.1f, 0.06f), FairYellow);
        Painted(node, "Big Wheel", new Vector3(-4.6f, deck / 2, 2.66f), 4.6f, 0.5f, FairCream);
        Painted(node, "Big Wheel", new Vector3(4.6f, deck / 2, 2.66f), 4.6f, 0.5f, FairCream);
        Steps(b, new Vector3(0, 0, 3.5f), 2.6f, deck, 3);
        Rail(b, new Vector3(-8.4f, deck, 2.5f), new Vector3(-1.5f, deck, 2.5f), FairPink);
        Rail(b, new Vector3(1.5f, deck, 2.5f), new Vector3(8.4f, deck, 2.5f), FairPink);
        PayBox(b, node, new Vector3(-7.6f, deck, 1.5f));

        // Two A-frames carry the axle.
        foreach (var z in new[] { -1.25f, 1.25f })
        {
            foreach (var x in new[] { -5.6f, 5.6f })
            {
                Beam(b, new Vector3(x, deck, z), new Vector3(0, hub, z), 0.34f, FairCream);
                b.Box(new Vector3(x, deck + 0.1f, z), new Vector3(0.9f, 0.2f, 0.7f), FairDark);
            }

            Beam(b, new Vector3(-2.9f, deck + ((hub - deck) * 0.48f), z), new Vector3(2.9f, deck + ((hub - deck) * 0.48f), z), 0.2f, FairCream);
        }

        b.Tube(new Vector3(0, hub, 0), new Vector3(0.6f, 3.0f, 0.6f), FairDark, new Vector3(90, 0, 0));
        node.AddChild(b.BuildNode("Shapes"));

        // The wheel itself: two rims, spokes, and bulbs along every spoke.
        var wheel = new Node3D { Name = "Wheel", Position = new Vector3(0, hub, 0) };
        var w = new MeshBuilder();
        const int cars = 12;
        const int rim = 24;
        foreach (var z in new[] { -0.8f, 0.8f })
        {
            for (var i = 0; i < rim; i++)
            {
                var from = Mathf.Tau * i / rim;
                var to = Mathf.Tau * (i + 1) / rim;
                Beam(w, new Vector3(r * MathF.Cos(from), r * MathF.Sin(from), z), new Vector3(r * MathF.Cos(to), r * MathF.Sin(to), z), 0.16f, FairPink);
                Beam(w, new Vector3(r * 0.55f * MathF.Cos(from), r * 0.55f * MathF.Sin(from), z), new Vector3(r * 0.55f * MathF.Cos(to), r * 0.55f * MathF.Sin(to), z), 0.1f, FairYellow);
            }

            for (var i = 0; i < cars; i++)
            {
                var a = Mathf.Tau * i / cars;
                var out1 = new Vector3(MathF.Cos(a), MathF.Sin(a), 0);
                Beam(w, (out1 * 0.5f) + new Vector3(0, 0, z), (out1 * r) + new Vector3(0, 0, z), 0.1f, FairCream);
                if (z > 0)
                {
                    Bulbs(w, (out1 * 1.6f) + new Vector3(0, 0, z + 0.1f), (out1 * (r - 0.4f)) + new Vector3(0, 0, z + 0.1f), 7);
                }
            }
        }

        for (var i = 0; i < cars; i++)
        {
            var a = Mathf.Tau * i / cars;
            w.Tube(new Vector3(r * MathF.Cos(a), r * MathF.Sin(a), 0), new Vector3(0.1f, 1.7f, 0.1f), FairSteel, new Vector3(90, 0, 0));
        }

        w.Tube(Vector3.Zero, new Vector3(1.5f, 1.9f, 1.5f), FairPink, new Vector3(90, 0, 0));
        SunflowerBadge(w, new Vector3(0, 0, 0.97f), 2.6f);
        wheel.AddChild(w.BuildNode("Shapes"));

        for (var i = 0; i < cars; i++)
        {
            var a = Mathf.Tau * i / cars;
            var car = WheelCar(CarColours[i % CarColours.Length]);
            car.Name = $"Car{i}";
            car.Position = new Vector3(r * MathF.Cos(a), r * MathF.Sin(a), 0);
            wheel.AddChild(car);
        }

        node.AddChild(wheel);
        return node;
    }

    /// <summary>A round open car with a little roof, hanging from the pin at its top. Two people sit side by side.</summary>
    private static Node3D WheelCar(Color colour)
    {
        var car = new Node3D();
        var b = new MeshBuilder();
        b.Cone(new Vector3(0, -0.42f, 0), new Vector3(1.6f, 0.36f, 1.5f), colour);
        b.Ball(new Vector3(0, -0.22f, 0), 0.16f, FairCream);
        foreach (var x in new[] { -0.62f, 0.62f })
        {
            foreach (var z in new[] { -0.5f, 0.5f })
            {
                b.Tube(new Vector3(x, -0.98f, z), new Vector3(0.05f, 0.9f, 0.05f), FairCream);
            }
        }

        b.Tube(new Vector3(0, -1.7f, 0), new Vector3(1.5f, 0.6f, 1.4f), colour);
        b.Tube(new Vector3(0, -1.4f, 0), new Vector3(1.56f, 0.07f, 1.46f), FairCream);
        b.Tube(new Vector3(0, -2.02f, 0), new Vector3(1.3f, 0.06f, 1.2f), FairDark);
        car.AddChild(b.BuildNode("Shapes"));
        car.AddChild(Seat("Seat0", new Vector3(-0.3f, -1.98f, -0.12f)));
        car.AddChild(Seat("Seat1", new Vector3(0.3f, -1.98f, -0.12f)));
        return car;
    }

    // ---- the carousel ----

    private static Node3D Carousel()
    {
        var node = new Node3D { Name = "Carousel" };
        var b = new MeshBuilder();
        b.Drum(new Vector3(0, 0.1f, 0), new Vector3(10.2f, 0.2f, 10.2f), FairDeck);
        b.Drum(new Vector3(0, 0.22f, 0), new Vector3(9.5f, 0.44f, 9.5f), FairPinkDark);
        node.AddChild(b.BuildNode("Shapes"));
        PayBox(b = new MeshBuilder(), node, new Vector3(-4.6f, 0, 4.4f));
        node.AddChild(b.BuildNode("Pay box"));

        var table = new Node3D { Name = "Turntable", Position = new Vector3(0, 0.44f, 0) };
        var t = new MeshBuilder();
        t.Drum(new Vector3(0, 0.05f, 0), new Vector3(9.2f, 0.1f, 9.2f), FairDeck);
        PaintedFloor(t, new Vector3(0, 0.105f, 0), 4.5f, 16, new Color("#C9A27A"), FairDeck);

        // The middle drum, with painted panels and mirrors.
        t.Drum(new Vector3(0, 1.9f, 0), new Vector3(2.3f, 3.7f, 2.3f), FairCream);
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * (i + 0.5f) / 8;
            var at = new Vector3(1.16f * MathF.Cos(a), 0, 1.16f * MathF.Sin(a));
            var turn = new Vector3(0, 90 - Mathf.RadToDeg(a), 0);
            t.Box(at + new Vector3(0, 1.0f, 0), new Vector3(0.74f, 1.3f, 0.05f), i % 2 == 0 ? FairPink : FairTeal, turn);
            t.Box(at + new Vector3(0, 2.55f, 0), new Vector3(0.6f, 1.2f, 0.05f), new Color("#9CCBE0"), turn);
            t.Box(at + new Vector3(0, 3.35f, 0), new Vector3(0.74f, 0.25f, 0.06f), FairYellow, turn);
        }

        // The roof: a striped top, a scalloped edge with bulbs, a sunflower and a flag on the tip.
        const float eaves = 3.95f;
        RoundRoof(t, new Vector3(0, eaves, 0), 5.05f, 2.7f, 16, FairPink, FairCream);
        Valance(t, new Vector3(0, eaves - 0.22f, 0), 5.0f, 0.5f, 16, FairYellow, FairPink);
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * i / 8;
            Beam(t, new Vector3(1.1f * MathF.Cos(a), eaves - 0.3f, 1.1f * MathF.Sin(a)), new Vector3(4.95f * MathF.Cos(a), eaves - 0.05f, 4.95f * MathF.Sin(a)), 0.08f, FairCream);
        }

        t.Ball(new Vector3(0, eaves + 2.8f, 0), 0.5f, FairYellow);
        t.Tube(new Vector3(0, eaves + 3.5f, 0), new Vector3(0.05f, 1.2f, 0.05f), FairCream);
        t.Triangle(new Vector3(0, eaves + 4.1f, 0), new Vector3(0, eaves + 3.6f, 0), new Vector3(0.9f, eaves + 3.85f, 0), FairPink);
        table.AddChild(t.BuildNode("Shapes"));

        // Sixteen horses: ten round the outside and six inside.
        var coats = new[] { new Color("#F6F0E2"), new Color("#C9A27A"), new Color("#7A5638"), new Color("#4A4F57"), new Color("#FFF6DD") };
        for (var i = 0; i < 16; i++)
        {
            var outer = i < 10;
            var a = outer ? Mathf.Tau * i / 10 : Mathf.Tau * (i - 10 + 0.5f) / 6;
            var r = outer ? 3.75f : 2.35f;
            var horse = new Node3D { Name = $"Horse{i}", Position = new Vector3(r * MathF.Cos(a), 0.1f, r * MathF.Sin(a)), Rotation = new Vector3(0, -a, 0) };
            var pole = new MeshBuilder();
            pole.Tube(new Vector3(0, 1.85f, 0), new Vector3(0.06f, 3.7f, 0.06f), FairYellow);
            horse.AddChild(pole.BuildNode("Pole"));
            var body = new Node3D { Name = "Body" };
            var h = new MeshBuilder();
            Horse(h, new Vector3(0, 1.1f, 0), coats[i % coats.Length], CarColours[(i + (i / 4)) % CarColours.Length]);
            body.AddChild(h.BuildNode("Shapes"));
            body.AddChild(Seat("Seat", new Vector3(0, 0.86f, -0.05f)));
            horse.AddChild(body);
            table.AddChild(horse);
        }

        node.AddChild(table);
        return node;
    }

    /// <summary>A galloping fairground horse, its back at a height, heading +Z.</summary>
    private static void Horse(MeshBuilder b, Vector3 back, Color coat, Color tack)
    {
        var dark = coat.Darkened(0.35f);
        b.Pill(back + new Vector3(0, 0, 0), new Vector3(0.4f, 1.05f, 0.4f), coat, new Vector3(90, 0, 0));
        b.Box(back + new Vector3(0, 0.3f, 0.45f), new Vector3(0.2f, 0.55f, 0.24f), coat, new Vector3(35, 0, 0));
        b.Box(back + new Vector3(0, 0.56f, 0.68f), new Vector3(0.19f, 0.22f, 0.42f), coat, new Vector3(25, 0, 0));
        b.Box(back + new Vector3(0, 0.38f, 0.36f), new Vector3(0.07f, 0.5f, 0.12f), dark, new Vector3(35, 0, 0));
        foreach (var x in new[] { -0.07f, 0.07f })
        {
            b.Cone(back + new Vector3(x, 0.72f, 0.56f), new Vector3(0.07f, 0.12f, 0.07f), coat);
        }

        foreach (var x in new[] { -0.12f, 0.12f })
        {
            b.Tube(back + new Vector3(x, -0.38f, 0.42f), new Vector3(0.09f, 0.55f, 0.09f), coat, new Vector3(-35, 0, 0));
            b.Tube(back + new Vector3(x, -0.38f, -0.42f), new Vector3(0.09f, 0.55f, 0.09f), coat, new Vector3(30, 0, 0));
            b.Ball(back + new Vector3(x, -0.6f, 0.58f), 0.11f, dark);
            b.Ball(back + new Vector3(x, -0.62f, -0.56f), 0.11f, dark);
        }

        b.Pill(back + new Vector3(0, -0.05f, -0.62f), new Vector3(0.1f, 0.5f, 0.1f), dark, new Vector3(-40, 0, 0));
        b.Box(back + new Vector3(0, 0.2f, -0.04f), new Vector3(0.44f, 0.07f, 0.42f), tack);
        b.Box(back + new Vector3(0, 0.02f, -0.04f), new Vector3(0.42f, 0.3f, 0.1f), tack);
        b.Box(back + new Vector3(0, 0.5f, 0.6f), new Vector3(0.21f, 0.04f, 0.2f), tack, new Vector3(25, 0, 0));
    }

    // ---- the waltzer ----

    private static Node3D Waltzer()
    {
        var node = new Node3D { Name = "Waltzer" };
        var b = new MeshBuilder();
        const float deck = 0.75f;
        b.Drum(new Vector3(0, 0.16f, 0), new Vector3(15.2f, 0.32f, 15.2f), FairDeck);
        b.Drum(new Vector3(0, deck / 2, 0), new Vector3(14.2f, deck, 14.2f), FairPinkDark);
        b.Drum(new Vector3(0, deck - 0.03f, 0), new Vector3(14.3f, 0.07f, 14.3f), FairYellow);
        Steps(b, new Vector3(0, 0, 7.75f), 5f, deck, 2);

        // The pay box in the middle, glass all round.
        b.Drum(new Vector3(0, deck + 0.55f, 0), new Vector3(2.7f, 1.1f, 2.7f), FairTeal);
        b.Drum(new Vector3(0, deck + 1.55f, 0), new Vector3(2.6f, 0.9f, 2.6f), new Color("#9CCBE0"));
        b.Drum(new Vector3(0, deck + 2.15f, 0), new Vector3(2.9f, 0.3f, 2.9f), FairYellow);
        b.Tube(new Vector3(0, deck + 4.2f, 0), new Vector3(0.3f, 4.2f, 0.3f), FairCream);
        for (var i = 0; i < 12; i++)
        {
            var a = Mathf.Tau * i / 12;
            b.Ball(new Vector3(1.47f * MathF.Cos(a), deck + 2.15f, 1.47f * MathF.Sin(a)), 0.15f, i % 2 == 0 ? FairCream : FairPink);
        }

        // Ten posts carry the roof; painted boards close the back half.
        const float eaves = 5.4f;
        const float edge = 7.0f;
        for (var i = 0; i < 12; i++)
        {
            var a = Mathf.Tau * (i + 0.5f) / 12;
            var at = new Vector3(edge * MathF.Cos(a), 0, edge * MathF.Sin(a));
            b.Tube(at + new Vector3(0, (deck + eaves) / 2, 0), new Vector3(0.16f, eaves - deck, 0.16f), FairCream);
            Bulbs(b, at + new Vector3(0, deck + 0.6f, 0) + (at.Normalized() * 0.1f), at + new Vector3(0, eaves - 0.5f, 0) + (at.Normalized() * 0.1f), 6, 0.12f);
        }

        for (var i = 0; i < 6; i++)
        {
            // The six gaps round the back (north) side.
            var a = Mathf.Tau * (i + 7) / 12;
            var at = new Vector3((edge - 0.1f) * MathF.Cos(a), deck + 1.7f, (edge - 0.1f) * MathF.Sin(a));
            var turn = 90 - Mathf.RadToDeg(a);
            b.Box(at, new Vector3(3.55f, 3.2f, 0.1f), i % 2 == 0 ? FairSky : FairLilac, new Vector3(0, turn, 0));
            var inward = -new Vector3(MathF.Cos(a), 0, MathF.Sin(a));
            SunflowerBadge(b, at + (inward * 0.08f) + new Vector3(0, 0.1f, 0), 2.0f, turn + 180);
        }

        RoundRoof(b, new Vector3(0, eaves, 0), edge + 0.5f, 2.6f, 24, FairTeal, FairCream);
        Valance(b, new Vector3(0, eaves - 0.3f, 0), edge + 0.45f, 0.7f, 24, FairPink, FairYellow);
        b.Ball(new Vector3(0, eaves + 2.7f, 0), 0.6f, FairYellow);
        b.Box(new Vector3(0, eaves + 0.75f, edge + 0.5f), new Vector3(6.4f, 1.3f, 0.14f), FairPink);
        b.Box(new Vector3(0, eaves + 0.75f, edge + 0.53f), new Vector3(6.0f, 0.95f, 0.14f), FairPinkDark);
        Bulbs(b, new Vector3(-3.05f, eaves + 1.42f, edge + 0.6f), new Vector3(3.05f, eaves + 1.42f, edge + 0.6f), 16);
        Bulbs(b, new Vector3(-3.05f, eaves + 0.08f, edge + 0.6f), new Vector3(3.05f, eaves + 0.08f, edge + 0.6f), 16);
        Painted(node, "Waltzer", new Vector3(0, eaves + 0.75f, edge + 0.61f), 5.6f, 0.85f, FairCream);
        node.AddChild(b.BuildNode("Shapes"));

        // The floor turns, and each of the eight cars spins on it.
        var table = new Node3D { Name = "Turntable", Position = new Vector3(0, deck, 0) };
        var t = new MeshBuilder();
        t.Drum(new Vector3(0, 0.05f, 0), new Vector3(13.0f, 0.1f, 13.0f), FairDark);
        PaintedFloor(t, new Vector3(0, 0.105f, 0), 6.4f, 16, FairPink, FairYellow);
        table.AddChild(t.BuildNode("Shapes"));
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * i / 8;
            var car = WaltzerCar(CarColours[i % CarColours.Length]);
            car.Name = $"Car{i}";
            car.Position = new Vector3(4.35f * MathF.Cos(a), 0.11f, 4.35f * MathF.Sin(a));
            table.AddChild(car);
        }

        node.AddChild(table);
        return node;
    }

    /// <summary>A round tub with a high curved back, open to +Z. Four sit along the back.</summary>
    private static Node3D WaltzerCar(Color colour)
    {
        var car = new Node3D();
        var b = new MeshBuilder();
        b.Drum(new Vector3(0, 0.16f, 0), new Vector3(2.1f, 0.32f, 2.1f), colour.Darkened(0.25f));
        b.Drum(new Vector3(0, 0.33f, 0), new Vector3(2.0f, 0.04f, 2.0f), FairDark);
        const int boards = 9;
        for (var i = 0; i < boards; i++)
        {
            // Round the back from one side of the opening to the other.
            var a = Mathf.DegToRad(150 + (240f * i / (boards - 1)));
            var at = new Vector3(1.0f * MathF.Cos(a), 0, 1.0f * MathF.Sin(a));
            var tall = 1.25f - (0.5f * MathF.Abs((i - 4) / 4f));
            b.Box(at + new Vector3(0, 0.3f + (tall / 2), 0), new Vector3(0.62f, tall, 0.1f), colour, new Vector3(0, 90 - Mathf.RadToDeg(a), 0));
            b.Ball(at + new Vector3(0, 0.3f + tall, 0), new Vector3(0.5f, 0.16f, 0.2f), FairCream, new Vector3(0, 90 - Mathf.RadToDeg(a), 0));
            var seat = new Vector3(0.68f * MathF.Cos(a), 0.74f, 0.68f * MathF.Sin(a));
            b.Box(seat, new Vector3(0.5f, 0.1f, 0.5f), FairCream, new Vector3(0, 90 - Mathf.RadToDeg(a), 0));
        }

        SunflowerBadge(b, new Vector3(0, 1.0f, -1.08f), 0.8f, 180);
        b.Tube(new Vector3(0, 0.95f, 0.45f), new Vector3(0.06f, 1.5f, 0.06f), FairSteel, new Vector3(0, 0, 90));
        car.AddChild(b.BuildNode("Shapes"));
        for (var i = 0; i < 4; i++)
        {
            // Four along the bench, each looking in to the middle.
            var a = Mathf.DegToRad(204 + (44f * i));
            car.AddChild(Seat($"Seat{i}", new Vector3(0.6f * MathF.Cos(a), 0.32f, 0.6f * MathF.Sin(a)), Mathf.RadToDeg(MathF.Atan2(-MathF.Cos(a), -MathF.Sin(a)))));
        }
        return car;
    }

    // ---- the twister ----

    private static Node3D Twister()
    {
        var node = new Node3D { Name = "Twister" };
        var b = new MeshBuilder();
        b.Drum(new Vector3(0, 0.12f, 0), new Vector3(15.4f, 0.24f, 15.4f), FairDeck);
        PaintedFloor(b, new Vector3(0, 0.245f, 0), 7.2f, 12, FairDeck, new Color("#C9A27A"));

        // A fence all round, open at the front under the arch.
        const float edge = 7.5f;
        const int posts = 22;
        for (var i = 0; i < posts; i++)
        {
            var from = Mathf.DegToRad(105 + (315f * i / posts));
            var to = Mathf.DegToRad(105 + (315f * (i + 1) / posts));
            Rail(b, new Vector3(edge * MathF.Cos(from), 0.24f, edge * MathF.Sin(from)), new Vector3(edge * MathF.Cos(to), 0.24f, edge * MathF.Sin(to)), i % 2 == 0 ? FairPink : FairYellow, 1.0f);
        }

        foreach (var x in new[] { -1.95f, 1.95f })
        {
            b.Tube(new Vector3(x, 1.9f, edge - 0.25f), new Vector3(0.2f, 3.8f, 0.2f), FairCream);
            Bulbs(b, new Vector3(x, 0.5f, edge - 0.12f), new Vector3(x, 3.0f, edge - 0.12f), 7, 0.12f);
        }

        b.Box(new Vector3(0, 3.5f, edge - 0.25f), new Vector3(4.6f, 1.1f, 0.14f), FairTeal);
        b.Box(new Vector3(0, 3.5f, edge - 0.22f), new Vector3(4.3f, 0.82f, 0.14f), new Color("#1F8E82"));
        Painted(node, "Twister", new Vector3(0, 3.5f, edge - 0.14f), 4.0f, 0.72f, FairCream);
        Bulbs(b, new Vector3(-2.15f, 4.08f, edge - 0.15f), new Vector3(2.15f, 4.08f, edge - 0.15f), 12);
        PayBox(b, node, new Vector3(2.85f, 0, 6.72f));

        // The middle it all turns on.
        b.Cone(new Vector3(0, 0.95f, 0), new Vector3(2.6f, 1.45f, 2.6f), FairPink);
        b.Drum(new Vector3(0, 0.36f, 0), new Vector3(2.7f, 0.22f, 2.7f), FairYellow);
        node.AddChild(b.BuildNode("Shapes"));

        var spinner = new Node3D { Name = "Spinner", Position = new Vector3(0, 1.75f, 0) };
        var s = new MeshBuilder();
        s.Tube(new Vector3(0, 0.3f, 0), new Vector3(1.5f, 0.9f, 1.5f), FairYellow);
        s.Ball(new Vector3(0, 0.85f, 0), new Vector3(1.3f, 0.9f, 1.3f), FairPink);
        s.Tube(new Vector3(0, 1.8f, 0), new Vector3(0.12f, 1.4f, 0.12f), FairCream);
        s.Ball(new Vector3(0, 2.6f, 0), 0.45f, FairYellow);
        const float reach = 4.3f;
        const float lift = 1.5f;
        for (var i = 0; i < 3; i++)
        {
            var a = Mathf.Tau * i / 3;
            var end = new Vector3(reach * MathF.Cos(a), lift, reach * MathF.Sin(a));
            Beam(s, new Vector3(0.4f * MathF.Cos(a), 0.4f, 0.4f * MathF.Sin(a)), end, 0.3f, FairTeal);
            Bulbs(s, new Vector3(0.9f * MathF.Cos(a), 0.83f, 0.9f * MathF.Sin(a)), end + new Vector3(0, 0.2f, 0), 9);
            // A stay from the mast down to each arm.
            Beam(s, new Vector3(0, 2.4f, 0), end * 0.8f, 0.05f, FairCream);
        }

        spinner.AddChild(s.BuildNode("Shapes"));
        for (var i = 0; i < 3; i++)
        {
            var a = Mathf.Tau * i / 3;
            var cluster = new Node3D { Name = $"Cluster{i}", Position = new Vector3(reach * MathF.Cos(a), lift, reach * MathF.Sin(a)) };
            var c = new MeshBuilder();
            c.Tube(new Vector3(0, 0, 0), new Vector3(0.7f, 0.7f, 0.7f), FairYellow);
            c.Ball(new Vector3(0, 0.4f, 0), 0.6f, FairPink);
            for (var k = 0; k < 4; k++)
            {
                var ka = (Mathf.Tau * k / 4) + 0.5f;
                var outward = new Vector3(MathF.Cos(ka), 0, MathF.Sin(ka));
                Beam(c, new Vector3(0, -0.1f, 0), (outward * 1.75f) + new Vector3(0, -0.75f, 0), 0.16f, FairCream);
                Beam(c, (outward * 1.75f) + new Vector3(0, -0.7f, 0), (outward * 1.75f) + new Vector3(0, -1.75f, 0), 0.12f, FairSteel, true);
            }

            cluster.AddChild(c.BuildNode("Shapes"));
            for (var k = 0; k < 4; k++)
            {
                var ka = (Mathf.Tau * k / 4) + 0.5f;
                var car = TwisterCar(CarColours[(i + k) % CarColours.Length]);
                car.Name = $"Car{k}";
                car.Position = new Vector3(1.75f * MathF.Cos(ka), -2.7f, 1.75f * MathF.Sin(ka));
                car.Rotation = new Vector3(0, (Mathf.Pi / 2) - ka, 0);
                cluster.AddChild(car);
            }

            spinner.AddChild(cluster);
        }

        node.AddChild(spinner);
        return node;
    }

    /// <summary>A two-seat car with a high back, hanging from the post behind it and looking out to +Z.</summary>
    private static Node3D TwisterCar(Color colour)
    {
        var car = new Node3D();
        var b = new MeshBuilder();
        b.Box(new Vector3(0, 0.1f, 0.3f), new Vector3(1.4f, 0.2f, 1.2f), colour.Darkened(0.25f));
        b.Box(new Vector3(0, 0.75f, -0.24f), new Vector3(1.4f, 1.3f, 0.14f), colour);
        b.Ball(new Vector3(0, 1.4f, -0.24f), new Vector3(1.4f, 0.3f, 0.2f), FairCream);
        foreach (var x in new[] { -0.68f, 0.68f })
        {
            b.Box(new Vector3(x, 0.5f, 0.3f), new Vector3(0.1f, 0.75f, 1.1f), colour);
        }

        b.Box(new Vector3(0, 0.42f, 0.86f), new Vector3(1.4f, 0.6f, 0.1f), colour);
        b.Box(new Vector3(0, 0.6f, 0.1f), new Vector3(1.2f, 0.1f, 0.5f), FairCream);
        b.Tube(new Vector3(0, 0.95f, 0.62f), new Vector3(0.06f, 1.3f, 0.06f), FairSteel, new Vector3(0, 0, 90));
        SunflowerBadge(b, new Vector3(0, 0.42f, 0.92f), 0.6f);
        car.AddChild(b.BuildNode("Shapes"));
        car.AddChild(Seat("Seat0", new Vector3(-0.32f, 0.2f, 0.08f)));
        car.AddChild(Seat("Seat1", new Vector3(0.32f, 0.2f, 0.08f)));
        return car;
    }

    // ---- the Skylark ----

    private static Node3D Skylark()
    {
        var node = new Node3D { Name = "Skylark" };
        var b = new MeshBuilder();
        const float deck = 0.9f;
        const float hub = SkylarkHub;
        const float back = -1.5f;

        // The platform with steps at each end of its front, and a painted front.
        b.Box(new Vector3(0, deck / 2, 0), new Vector3(12f, deck, 6.6f), FairDeck);
        b.Box(new Vector3(0, deck / 2, 3.32f), new Vector3(12.1f, deck, 0.06f), FairPink);
        b.Box(new Vector3(0, deck - 0.06f, 3.34f), new Vector3(12.1f, 0.1f, 0.06f), FairYellow);
        Painted(node, "Skylark", new Vector3(0, deck / 2, 3.36f), 4.4f, 0.6f, FairCream);
        foreach (var x in new[] { -4.6f, 4.6f })
        {
            Steps(b, new Vector3(x, 0, 4.2f), 2.2f, deck, 3);
        }

        Rail(b, new Vector3(-3.3f, deck, 3.2f), new Vector3(3.3f, deck, 3.2f), FairPink);
        Rail(b, new Vector3(-5.9f, deck, 3.2f), new Vector3(-5.9f, deck, -3.2f), FairPink);
        Rail(b, new Vector3(5.9f, deck, 3.2f), new Vector3(5.9f, deck, -3.2f), FairPink);
        PayBox(b, node, new Vector3(-4.9f, deck, -1.9f));

        // Painted boards along the back: a summer sky with sunflowers and larks.
        for (var i = 0; i < 3; i++)
        {
            var x = (i - 1) * 3.95f;
            b.Box(new Vector3(x, deck + 1.95f, -3.15f), new Vector3(3.9f, 3.9f, 0.12f), FairSky);
            b.Box(new Vector3(x, deck + 0.45f, -3.1f), new Vector3(3.9f, 0.9f, 0.12f), new Color("#5DAE4B"));
            b.Box(new Vector3(x, deck + 3.98f, -3.1f), new Vector3(3.9f, 0.22f, 0.16f), FairYellow);
            b.Ball(new Vector3(x - 0.9f, deck + 3.1f, -3.08f), new Vector3(1.3f, 0.5f, 0.05f), FairCream);
            b.Ball(new Vector3(x + 0.7f, deck + 2.6f, -3.08f), new Vector3(1.0f, 0.4f, 0.05f), FairCream);
            foreach (var (sx, tall, size) in new[] { (-1.25f, 1.5f, 1.0f), (0.1f, 2.0f, 1.3f), (1.3f, 1.3f, 0.9f) })
            {
                if (i == 1 && sx == 0.1f)
                {
                    continue;
                }

                b.Box(new Vector3(x + sx, deck + (tall / 2), -3.05f), new Vector3(0.09f, tall, 0.05f), new Color("#3F8A3C"));
                SunflowerBadge(b, new Vector3(x + sx, deck + tall, -3.02f), size);
            }

            LarkBadge(b, new Vector3(x + (i == 1 ? 1.2f : -0.2f), deck + 3.25f, -3.04f), 0.9f, i == 2);
            LarkBadge(b, new Vector3(x + (i == 1 ? -1.3f : 1.1f), deck + 2.5f, -3.04f), 0.65f, i != 0);
        }

        Bulbs(b, new Vector3(-5.8f, deck + 4.12f, -3.0f), new Vector3(5.8f, deck + 4.12f, -3.0f), 30);

        // The tower in bands of colour, with two props behind it.
        var bands = new[] { FairPink, FairYellow, FairTeal, FairCream, FairLilac };
        const float band = 1.05f;
        var count = (int)MathF.Ceiling((hub + 0.9f - deck) / band);
        for (var i = 0; i < count; i++)
        {
            b.Box(new Vector3(0, deck + ((i + 0.5f) * band), back), new Vector3(1.15f, band + 0.01f, 1.15f), bands[i % bands.Length]);
        }

        foreach (var x in new[] { -0.52f, 0.52f })
        {
            Bulbs(b, new Vector3(x, deck + 0.4f, back + 0.62f), new Vector3(x, hub - 1.4f, back + 0.62f), 26, 0.12f);
        }

        foreach (var x in new[] { -3.4f, 3.4f })
        {
            Beam(b, new Vector3(x, deck, -2.7f), new Vector3(0, hub * 0.55f, back - 0.5f), 0.24f, FairCream);
        }

        b.Tube(new Vector3(0, hub, back + 0.75f), new Vector3(0.7f, 1.6f, 0.7f), FairDark, new Vector3(90, 0, 0));
        b.Ball(new Vector3(0, deck + (count * band) + 0.3f, back), 0.8f, FairYellow);
        node.AddChild(b.BuildNode("Shapes"));

        // The arm swings right over, in front of the tower, with a row of seats at each end.
        var arm = new Node3D { Name = "Arm", Position = new Vector3(0, hub, 0.1f) };
        var m = new MeshBuilder();
        const float half = SkylarkArm;
        m.Box(Vector3.Zero, new Vector3(0.9f, 5.0f, 0.55f), FairPink);
        foreach (var side in new[] { -1f, 1f })
        {
            m.Box(new Vector3(0, side * 5.0f, 0), new Vector3(0.72f, 5.0f, 0.5f), FairPinkDark);
            m.Box(new Vector3(0, side * 9.2f, 0), new Vector3(0.52f, 3.6f, 0.42f), FairPink);
            for (var i = 0; i < 9; i++)
            {
                // Yellow chevrons up the arm, pointing to its tip.
                var y = side * (1.4f + (i * 1.05f));
                m.Box(new Vector3(-0.15f, y, 0.3f), new Vector3(0.42f, 0.13f, 0.04f), FairYellow, new Vector3(0, 0, side * 40));
                m.Box(new Vector3(0.15f, y, 0.3f), new Vector3(0.42f, 0.13f, 0.04f), FairYellow, new Vector3(0, 0, side * -40));
            }

            foreach (var x in new[] { -0.4f, 0.4f })
            {
                Bulbs(m, new Vector3(x, side * 1.2f, 0.3f), new Vector3(x * 0.65f, side * (half - 0.4f), 0.26f), 22, 0.11f);
            }
        }

        m.Tube(new Vector3(0, 0, 0.3f), new Vector3(2.2f, 0.2f, 2.2f), FairTeal, new Vector3(90, 0, 0));
        SunflowerBadge(m, new Vector3(0, 0, 0.44f), 2.3f);
        arm.AddChild(m.BuildNode("Shapes"));
        for (var end = 0; end < 2; end++)
        {
            var seats = SkylarkSeats(end == 0 ? FairYellow : FairTeal);
            seats.Name = $"Seats{end}";
            seats.Position = new Vector3(0, end == 0 ? -half : half, 0);
            arm.AddChild(seats);
        }

        node.AddChild(arm);
        return node;
    }

    /// <summary>Four seats in a row, hanging from the pin through the end of the arm, with a hoop round them.</summary>
    private static Node3D SkylarkSeats(Color colour)
    {
        var node = new Node3D();
        var b = new MeshBuilder();
        b.Tube(Vector3.Zero, new Vector3(0.22f, 3.6f, 0.22f), FairDark, new Vector3(0, 0, 90));
        var places = new[] { -1.3f, -0.55f, 0.55f, 1.3f };
        for (var i = 0; i < places.Length; i++)
        {
            var x = places[i];
            Beam(b, new Vector3(x, 0, 0), new Vector3(x, -0.5f, -0.3f), 0.1f, FairDark);
            b.Box(new Vector3(x, -1.05f, -0.3f), new Vector3(0.62f, 1.3f, 0.12f), i % 2 == 0 ? FairPink : colour);
            b.Ball(new Vector3(x, -0.42f, -0.3f), new Vector3(0.5f, 0.4f, 0.16f), i % 2 == 0 ? FairPink : colour);
            b.Box(new Vector3(x, -1.62f, -0.02f), new Vector3(0.62f, 0.12f, 0.6f), FairDark);
            // The bar that comes down over the shoulders.
            foreach (var sx in new[] { -0.17f, 0.17f })
            {
                Beam(b, new Vector3(x + sx, -0.55f, -0.24f), new Vector3(x + sx, -0.75f, 0.22f), 0.07f, FairYellow, true);
                Beam(b, new Vector3(x + sx, -0.75f, 0.22f), new Vector3(x + sx, -1.3f, 0.26f), 0.07f, FairYellow, true);
            }

            node.AddChild(Seat($"Seat{i}", new Vector3(x, -2.07f, -0.05f)));
        }

        // The hoop round the feet.
        var corners = new[] { new Vector3(-1.75f, -1.75f, -0.5f), new Vector3(1.75f, -1.75f, -0.5f), new Vector3(1.75f, -1.75f, 0.75f), new Vector3(-1.75f, -1.75f, 0.75f) };
        for (var i = 0; i < 4; i++)
        {
            Beam(b, corners[i], corners[(i + 1) % 4], 0.07f, FairYellow, true);
        }

        foreach (var x in new[] { -1.75f, 1.75f })
        {
            Beam(b, new Vector3(x, 0, 0), new Vector3(x, -1.75f, -0.5f), 0.07f, FairYellow, true);
        }

        node.AddChild(b.BuildNode("Shapes"));
        return node;
    }

    // ---- hook a duck ----

    private static Node3D HookADuck()
    {
        var node = new Node3D { Name = "Hook a duck" };
        var b = new MeshBuilder();

        // A round counter with a ring of water in it, and an island of prizes in the middle.
        b.Drum(new Vector3(0, 0.45f, 0), new Vector3(4.7f, 0.9f, 4.7f), FairPink);
        b.Drum(new Vector3(0, 0.2f, 0), new Vector3(4.76f, 0.12f, 4.76f), FairYellow);
        b.Drum(new Vector3(0, 0.93f, 0), new Vector3(4.9f, 0.08f, 4.9f), FairCream);
        b.Drum(new Vector3(0, 0.98f, 0), new Vector3(4.3f, 0.05f, 4.3f), FairSky);
        b.Drum(new Vector3(0, 1.1f, 0), new Vector3(2.3f, 0.36f, 2.3f), FairCream);
        b.Drum(new Vector3(0, 1.3f, 0), new Vector3(2.36f, 0.06f, 2.36f), FairYellow);
        b.Tube(new Vector3(0, 2.1f, 0), new Vector3(0.14f, 2.6f, 0.14f), FairCream);
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * (i + 0.5f) / 8;
            SunflowerBadge(b, new Vector3(2.37f * MathF.Sin(a), 0.55f, 2.37f * MathF.Cos(a)), 0.55f, Mathf.RadToDeg(a));
        }

        // Ducks bobbing round, each with a ring on its head to hook.
        for (var i = 0; i < 15; i++)
        {
            var a = (Mathf.Tau * i / 15) + (0.13f * (i % 3));
            var r = 1.45f + (0.22f * (i % 3));
            Duck(b, new Vector3(r * MathF.Cos(a), 1.0f, r * MathF.Sin(a)), 0.3f, (i * 67) % 360);
        }

        // Rods waiting on the counter.
        foreach (var (x, z, turn) in new[] { (1.0f, 2.2f, 20f), (-1.3f, 2.05f, -35f), (2.25f, 0.6f, 80f) })
        {
            b.Tube(new Vector3(x, 1.0f, z), new Vector3(0.035f, 1.3f, 0.035f), new Color("#9A7648"), new Vector3(90, turn, 0));
        }

        // The prizes: big teddies sat round the island, and more hung from the roof.
        for (var i = 0; i < 6; i++)
        {
            var a = Mathf.Tau * i / 6;
            Teddy(b, new Vector3(0.72f * MathF.Sin(a), 1.33f, 0.72f * MathF.Cos(a)), 0.95f, TeddyFurs[i % TeddyFurs.Length], Mathf.RadToDeg(a));
        }

        const float eaves = 2.95f;
        const float edge = 2.9f;
        for (var i = 0; i < 8; i++)
        {
            var a = Mathf.Tau * i / 8;
            b.Tube(new Vector3(2.4f * MathF.Sin(a), (eaves + 0.9f) / 2, 2.4f * MathF.Cos(a)), new Vector3(0.09f, eaves - 0.9f, 0.09f), FairCream);
            var between = Mathf.Tau * (i + 0.5f) / 8;
            var hang = new Vector3(2.55f * MathF.Sin(between), eaves - 1.25f, 2.55f * MathF.Cos(between));
            b.Tube(hang + new Vector3(0, 0.9f, 0), new Vector3(0.025f, 0.5f, 0.025f), new Color("#E9DFC4"));
            Teddy(b, hang, 0.72f, TeddyFurs[(i + 2) % TeddyFurs.Length], Mathf.RadToDeg(between));
        }

        RoundRoof(b, new Vector3(0, eaves, 0), edge + 0.3f, 1.4f, 16, FairYellow, FairCream);
        Valance(b, new Vector3(0, eaves - 0.2f, 0), edge + 0.28f, 0.45f, 16, FairPink, FairCream);

        // A giant duck on the tip of the roof.
        Duck(b, new Vector3(0, eaves + 1.35f, 0), 1.5f, 0);
        node.AddChild(b.BuildNode("Shapes"));

        foreach (var turn in new[] { 0f, 180f })
        {
            var board = new Node3D { Name = "Board", RotationDegrees = new Vector3(0, turn, 0) };
            var s = new MeshBuilder();
            s.Box(new Vector3(0, eaves + 0.4f, edge + 0.32f), new Vector3(3.3f, 0.7f, 0.08f), FairPink);
            s.Box(new Vector3(0, eaves + 0.4f, edge + 0.34f), new Vector3(3.1f, 0.52f, 0.08f), FairPinkDark);
            board.AddChild(s.BuildNode("Shapes"));
            Painted(board, "Hook a duck", new Vector3(0, eaves + 0.4f, edge + 0.39f), 2.9f, 0.42f, FairCream);
            node.AddChild(board);
        }

        return node;
    }

    /// <summary>A plastic duck sitting at a height, a size across, turned a number of degrees.</summary>
    private static void Duck(MeshBuilder b, Vector3 foot, float size, float turn)
    {
        var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(turn), 0));
        var spin = new Vector3(0, turn, 0);
        b.Ball(foot + (basis * new Vector3(0, 0.3f * size, 0)), new Vector3(0.8f * size, 0.6f * size, 1.0f * size), DuckYellow, spin);
        b.Ball(foot + (basis * new Vector3(0, 0.42f * size, -0.5f * size)), new Vector3(0.4f * size, 0.3f * size, 0.4f * size), DuckYellow, spin + new Vector3(-30, 0, 0));
        b.Ball(foot + (basis * new Vector3(0, 0.72f * size, 0.3f * size)), 0.52f * size, DuckYellow);
        b.Ball(foot + (basis * new Vector3(0, 0.66f * size, 0.6f * size)), new Vector3(0.3f * size, 0.12f * size, 0.3f * size), DuckBeak, spin);
        foreach (var x in new[] { -0.14f, 0.14f })
        {
            b.Ball(foot + (basis * new Vector3(x * size, 0.8f * size, 0.5f * size)), 0.08f * size, new Color("#26292E"));
        }

        b.Tube(foot + (basis * new Vector3(0, 1.02f * size, 0.3f * size)), new Vector3(0.16f * size, 0.04f * size, 0.16f * size), new Color("#8C949B"), spin + new Vector3(90, 0, 0));
    }

    /// <summary>A big teddy bear sitting down, a size tall, turned a number of degrees.</summary>
    public static void Teddy(MeshBuilder b, Vector3 foot, float size, Color fur, float turn = 0)
    {
        var basis = Basis.FromEuler(new Vector3(0, Mathf.DegToRad(turn), 0));
        var spin = new Vector3(0, turn, 0);
        var pale = new Color("#FBEED3");
        var dark = new Color("#3A2A20");
        Vector3 At(float x, float y, float z) => foot + (basis * (new Vector3(x, y, z) * size));
        b.Ball(At(0, 0.32f, 0), new Vector3(0.52f, 0.58f, 0.44f) * size, fur, spin);
        b.Ball(At(0, 0.3f, 0.16f), new Vector3(0.3f, 0.34f, 0.16f) * size, pale, spin);
        b.Ball(At(0, 0.74f, 0), 0.42f * size, fur);
        foreach (var x in new[] { -1f, 1f })
        {
            b.Ball(At(0.16f * x, 0.93f, 0), 0.16f * size, fur);
            b.Ball(At(0.16f * x, 0.93f, 0.04f), 0.09f * size, pale);
            b.Ball(At(0.08f * x, 0.79f, 0.19f), 0.045f * size, dark);
            b.Pill(At(0.3f * x, 0.4f, 0.08f), new Vector3(0.16f, 0.38f, 0.16f) * size, fur, spin + new Vector3(-25, 0, 35 * x));
            b.Pill(At(0.16f * x, 0.1f, 0.24f), new Vector3(0.18f, 0.4f, 0.18f) * size, fur, spin + new Vector3(90, 0, 0));
            b.Ball(At(0.16f * x, 0.1f, 0.44f), new Vector3(0.14f, 0.14f, 0.05f) * size, pale, spin);
        }

        b.Ball(At(0, 0.69f, 0.19f), new Vector3(0.2f, 0.15f, 0.12f) * size, pale, spin);
        b.Ball(At(0, 0.72f, 0.25f), 0.06f * size, dark);
        foreach (var x in new[] { -1f, 1f })
        {
            b.Ball(At(0.09f * x, 0.56f, 0.2f), new Vector3(0.16f, 0.1f, 0.05f) * size, FairPink, spin + new Vector3(0, 0, 20 * x));
        }

        b.Ball(At(0, 0.56f, 0.21f), 0.06f * size, FairPinkDark);
    }

    /// <summary>A teddy won at hook a duck, sat on the hand of whoever carries it.</summary>
    public static void WonTeddy(MeshBuilder b, Vector3 foot, int pick) =>
        Teddy(b, foot, 0.42f, TeddyFurs[((pick % TeddyFurs.Length) + TeddyFurs.Length) % TeddyFurs.Length]);
}
