using System;
using BlueprintFestival.Simulation.People;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>What a person is doing with their body.</summary>
public enum Pose
{
    Standing,
    Walking,
    Cheering,
    Dancing,

    /// <summary>Holding a ticket out for the gate staff to see.</summary>
    ShowingTicket,

    /// <summary>Clapping hard with hands in front, as at the end of a song.</summary>
    Clapping,

    /// <summary>Playing an instrument or singing: the arms reach where they are told (see <see cref="PersonView.Perform"/>).</summary>
    Performing,

    /// <summary>Sitting down, legs out in front, on a seat as high as <see cref="PersonView.SeatHeight"/>.</summary>
    Sitting,
}

/// <summary>
/// One person drawn in "style A", the look shared with Blueprint Hospital: a big round head with
/// dot eyes and a smile, a rounded body, thin legs and round shoes. Built from a
/// <see cref="PersonLook"/> as five meshes (body, two arms, two legs) so arms and legs can swing.
/// A typical adult stands about 1.6 m tall; the person faces +Z.
/// </summary>
public partial class PersonView : Node3D
{
    private static readonly Color Ink = new("#1D2B36");

    private const float HipY = 0.60f;
    private const float ShoulderY = 1.07f;
    private const float HeadY = 1.33f;
    private const float HeadR = 0.2f;
    private const float ArmLength = 0.42f;

    private Node3D _figure = null!;
    private Node3D _upper = null!;
    private Node3D _armLeft = null!;
    private Node3D _armRight = null!;
    private Node3D _legLeft = null!;
    private Node3D _legRight = null!;
    private float _stride;
    private float _cheer;
    private float _dance;
    private DanceMove _move;
    private float _through;
    private float _energy = 1;
    private float _show;
    private float _clap;
    private float _perform;
    private float _sit;
    private Vector3 _performLeft;
    private Vector3 _performRight;
    private float _performLift;
    private Vector3 _performLean;
    private float _shoulderX;
    private float _headY;
    private float _headR;
    private float _mouthY;
    private MeshInstance3D? _ticket;
    private MeshInstance3D? _held;
    private float _hold;

    /// <summary>What they have in their left hand.</summary>
    public BlueprintFestival.Simulation.Site.Carried Holding { get; private set; }

    /// <summary>
    /// Puts something in their left hand, or takes it away. Food and drink are held up in front of
    /// them; a bag of merchandise hangs from the hand at their side.
    /// </summary>
    public void Hold(BlueprintFestival.Simulation.Site.Carried item)
    {
        if (item == Holding)
        {
            return;
        }

        Holding = item;
        _held?.QueueFree();
        _held = null;
        if (item == BlueprintFestival.Simulation.Site.Carried.None)
        {
            return;
        }

        _held = SiteProps.CarriedItem(item, Look.Seed);
        _held.Position = new Vector3(0, -ArmLength, 0);
        _armLeft.AddChild(_held);
    }

    private bool HeldUp => Holding is not (BlueprintFestival.Simulation.Site.Carried.None or BlueprintFestival.Simulation.Site.Carried.Bag);
    private MeshInstance3D? _mouth;

    public PersonLook Look { get; private set; } = null!;

    public Pose Pose { get; set; } = Pose.Standing;

    /// <summary>How high off the ground the seat is while the pose is <see cref="Pose.Sitting"/>, in metres.</summary>
    public float SeatHeight { get; set; } = 0.45f;

    /// <summary>How tall this person stands, in metres.</summary>
    public float StandingHeight => (float)(1.6 * Look.Height);

    public static PersonView Build(PersonLook look)
    {
        var view = new PersonView { Look = look, Name = $"Person {look.Seed}" };
        view.Assemble();
        return view;
    }

    private void Assemble()
    {
        var look = Look;
        var kid = look.Age == AgeGroup.Child;
        var skin = new Color(look.Skin);
        var top = new Color(look.TopColour);
        var legColour = new Color(look.LegColour);
        var shoe = new Color(look.FeetColour);
        var hair = new Color(look.HairColour);
        var poncho = look.Poncho is { } p ? new Color(p) : (Color?)null;
        var build = (float)look.Build;

        _figure = new Node3D { Name = "Figure", Scale = Vector3.One * (float)look.Height };
        AddChild(_figure);

        // Everything above the hips hangs from here, so the body can lean and bend when dancing.
        _upper = new Node3D { Name = "Upper", Position = new Vector3(0, HipY, 0) };
        _figure.AddChild(_upper);

        // Children have bigger heads for their size, as real children do.
        var headR = HeadR * (kid ? 1.22f : look.Age == AgeGroup.Teenager ? 1.06f : 1f);
        var headY = HeadY + (headR - HeadR);
        var shoulder = 0.185f * (0.75f + (0.25f * build)) * (look.Female ? 0.95f : 1.03f);
        var depth = 0.25f * (0.85f + (0.15f * build));
        _shoulderX = shoulder + 0.045f;
        _headY = headY;
        _headR = headR;
        _mouthY = -headR * (look.Beard ? 0.3f : 0.36f);

        var body = new MeshBuilder();

        // ---- torso and hips
        var torsoSize = new Vector3((shoulder * 2) + 0.02f, 0.56f, depth);
        var torsoAt = new Vector3(0, 0.88f, 0);
        body.Pill(torsoAt, torsoSize, top);
        if (look.Top == TopStyle.Stripes && look.TopDetail is { } stripe)
        {
            for (var i = 0; i < 3; i++)
            {
                body.Tube(new Vector3(0, 0.76f + (i * 0.115f), 0), new Vector3(torsoSize.X * 1.02f, 0.05f, depth * 1.03f), new Color(stripe));
            }
        }
        else if (look.Top == TopStyle.TShirt && look.TopDetail is { } print)
        {
            // A round print on the chest.
            body.Ball(new Vector3(0, 0.93f, (depth / 2) - 0.012f), new Vector3(0.15f, 0.15f, 0.04f), new Color(print));
        }
        else if (look.Top == TopStyle.Hoodie)
        {
            // A hood lying on the shoulders, and a pocket on the front.
            body.Ball(new Vector3(0, 1.12f, -depth * 0.42f), new Vector3(0.27f, 0.17f, 0.19f), top.Darkened(0.1f));
            body.Box(new Vector3(0, 0.74f, (depth / 2) - 0.005f), new Vector3(0.2f, 0.09f, 0.03f), top.Darkened(0.12f));
        }
        else if (look.Top == TopStyle.Check && look.TopDetail is { } check)
        {
            // Bands across and down, crossing into a check.
            for (var i = 0; i < 4; i++)
            {
                body.Tube(new Vector3(0, 0.71f + (i * 0.11f), 0), new Vector3(torsoSize.X * 1.02f, 0.035f, depth * 1.03f), new Color(check));
            }

            foreach (var x in new[] { -0.11f, -0.04f, 0.04f, 0.11f })
            {
                body.Box(new Vector3(x * (shoulder / 0.185f), 0.88f, (depth / 2) - 0.008f), new Vector3(0.03f, 0.46f, 0.03f), new Color(check));
            }
        }

        BuildStageWear(body, look, torsoSize, torsoAt, depth, shoulder, top, legColour);

        var hipW = 0.30f * (0.8f + (0.2f * build));
        if (look.Legs == LegStyle.Skirt)
        {
            var hem = look.Top == TopStyle.Dress ? 0.36f : 0.3f;
            body.Cone(new Vector3(0, HipY + 0.1f - (hem / 2), 0), new Vector3(hipW * 1.5f, hem, depth * 1.45f), legColour);
        }
        else
        {
            body.Pill(new Vector3(0, HipY + 0.04f, 0), new Vector3(hipW, 0.24f, depth * 0.95f), legColour);
        }

        // ---- rain poncho: a cape over everything, with a hood
        if (poncho is { } cape)
        {
            body.Cone(new Vector3(0, 0.84f, 0), new Vector3(0.74f * (0.85f + (0.15f * build)), 0.66f, 0.6f), cape);
            body.Ball(new Vector3(0, headY + 0.03f, -0.035f), new Vector3(headR * 2.24f, headR * 2.2f, headR * 2.2f), cape);
        }

        // ---- the VIP lanyard: a ribbon from behind the neck down to a pass on the chest
        if (look.Lanyard is { } lanyardColour)
        {
            BuildLanyard(body, new Color(lanyardColour), poncho is null ? depth / 2 : 0.29f, shoulder);
        }

        // ---- head and face
        body.Ball(new Vector3(0, headY, 0), headR * 2, skin);
        BuildFace(body, look, headY, headR, hair);
        if (poncho is null)
        {
            BuildHair(body, look, headY, headR, hair);
            BuildHat(body, look, headY, headR);
            BuildHeadphones(body, look, headY, headR);
        }

        var bodyNode = body.BuildNode("Body");
        bodyNode.Position = new Vector3(0, -HipY, 0);
        _upper.AddChild(bodyNode);

        // ---- arms: sleeves to match the top, a hand, and the festival wristband on the right wrist
        var sleeve = poncho ?? (look.Jacket is { } jacket ? new Color(jacket) : look.Top == TopStyle.Check && look.TopDetail is { } c ? top.Lerp(new Color(c), 0.35f) : top);
        var covered = poncho is not null ? 0.55f : look.Jacket is not null ? 1f : look.Top switch
        {
            TopStyle.Vest => 0f,
            TopStyle.LongSleeve or TopStyle.Hoodie or TopStyle.Check => 1f,
            _ => 0.42f,
        };
        _armLeft = BuildArm("Arm left", -1, shoulder, sleeve, skin, covered, null);
        _armRight = BuildArm("Arm right", 1, shoulder, sleeve, skin, covered, new Color(look.Wristband));

        // ---- legs and feet
        _legLeft = BuildLeg("Leg left", -1, look, skin, legColour, shoe);
        _legRight = BuildLeg("Leg right", 1, look, skin, legColour, shoe);
    }

    private static void BuildFace(MeshBuilder body, PersonLook look, float headY, float headR, Color hair)
    {
        // A point on the front of the head, given how far across and up it is.
        Vector3 OnFace(float x, float y, float lift = 0.004f)
        {
            var z = Mathf.Sqrt(Mathf.Max(0.0001f, (headR * headR) - (x * x) - (y * y)));
            return new Vector3(x, headY + y, z + lift);
        }

        var eyeX = headR * 0.36f;
        var eyeY = headR * 0.12f;
        if (look.Sunglasses)
        {
            body.Ball(OnFace(-eyeX, eyeY), new Vector3(headR * 0.42f, headR * 0.3f, 0.03f), Ink);
            body.Ball(OnFace(eyeX, eyeY), new Vector3(headR * 0.42f, headR * 0.3f, 0.03f), Ink);
            body.Box(OnFace(0, eyeY, 0.012f), new Vector3(headR * 0.5f, 0.012f, 0.012f), Ink);
        }
        else
        {
            body.Ball(OnFace(-eyeX, eyeY), new Vector3(headR * 0.2f, headR * 0.23f, 0.02f), Ink);
            body.Ball(OnFace(eyeX, eyeY), new Vector3(headR * 0.2f, headR * 0.23f, 0.02f), Ink);
        }

        if (look.Beard)
        {
            body.Ball(new Vector3(0, headY - (headR * 0.52f), headR * 0.5f), new Vector3(headR * 1.25f, headR * 0.8f, headR * 0.95f), hair);
        }

        // The smile: a short curve of dots, higher at the ends.
        var smileY = -headR * (look.Beard ? 0.3f : 0.36f);
        var smile = look.Beard ? skin(look).Darkened(0.45f) : Ink.Lerp(skin(look), 0.25f);
        for (var i = -3; i <= 3; i++)
        {
            var t = i / 3f;
            body.Ball(OnFace(t * headR * 0.3f, smileY + (t * t * headR * 0.12f), look.Beard ? headR * 0.12f : 0.003f), new Vector3(0.022f, 0.016f, 0.014f), smile);
        }

        static Color skin(PersonLook l) => new(l.Skin);
    }

    private static void BuildHair(MeshBuilder body, PersonLook look, float headY, float headR, Color hair)
    {
        // The basic cap of hair: a ball a little bigger than the head, pushed up and back so the face shows.
        void Cap(float grow = 1.06f, float up = 0.2f, float back = 0.2f) =>
            body.Ball(new Vector3(0, headY + (headR * up), -headR * back), headR * 2 * grow, hair);

        switch (look.Hair)
        {
            case HairStyle.Bald:
                break;
            case HairStyle.Crop:
                Cap(1.03f, 0.18f, 0.14f);
                break;
            case HairStyle.Short:
                Cap();
                body.Ball(new Vector3(0, headY + (headR * 0.8f), headR * 0.25f), new Vector3(headR * 1.3f, headR * 0.5f, headR * 0.9f), hair);
                break;
            case HairStyle.Bob:
                Cap(1.08f);
                body.Ball(new Vector3(0, headY - (headR * 0.25f), -headR * 0.3f), new Vector3(headR * 2.3f, headR * 1.5f, headR * 1.7f), hair);
                break;
            case HairStyle.Long:
                Cap(1.08f);
                body.Ball(new Vector3(0, headY - (headR * 0.9f), -headR * 0.55f), new Vector3(headR * 2.1f, headR * 2.6f, headR * 1.1f), hair);
                break;
            case HairStyle.Ponytail:
                Cap();
                body.Ball(new Vector3(0, headY + (headR * 0.1f), -headR * 1.25f), new Vector3(headR * 0.6f, headR * 1.3f, headR * 0.6f), hair, new Vector3(-25, 0, 0));
                break;
            case HairStyle.Bun:
                Cap();
                body.Ball(new Vector3(0, headY + (headR * 1.15f), -headR * 0.35f), headR * 0.85f, hair);
                break;
            case HairStyle.Afro:
                body.Ball(new Vector3(0, headY + (headR * 0.4f), -headR * 0.28f), headR * 2 * 1.38f, hair);
                break;
            case HairStyle.Mane:
                // Big and loose, falling past the shoulders and framing the face.
                Cap(1.12f, 0.24f, 0.2f);
                body.Ball(new Vector3(0, headY - (headR * 1.05f), -headR * 0.45f), new Vector3(headR * 2.75f, headR * 3.3f, headR * 1.5f), hair);
                foreach (var side in new[] { -1f, 1f })
                {
                    body.Ball(new Vector3(side * headR * 0.92f, headY - (headR * 0.6f), headR * 0.05f), new Vector3(headR * 0.62f, headR * 2.1f, headR * 0.9f), hair);
                }

                break;
            case HairStyle.Quiff:
                Cap(1.03f, 0.18f, 0.16f);
                body.Ball(new Vector3(0, headY + (headR * 0.98f), headR * 0.32f), new Vector3(headR * 1.25f, headR * 0.8f, headR * 1.15f), hair, new Vector3(-22, 0, 0));
                break;
            case HairStyle.Curly:
                Cap(1.04f);
                for (var i = 0; i < 9; i++)
                {
                    var angle = i * Mathf.Tau / 9;
                    body.Ball(
                        new Vector3(Mathf.Cos(angle) * headR * 0.72f, headY + (headR * 0.72f), (Mathf.Sin(angle) * headR * 0.72f) - (headR * 0.22f)),
                        headR * 0.78f,
                        hair);
                }

                body.Ball(new Vector3(0, headY + (headR * 1.0f), -headR * 0.2f), headR * 0.9f, hair);
                break;
        }
    }

    private static void BuildHat(MeshBuilder body, PersonLook look, float headY, float headR)
    {
        var colour = new Color(look.HatColour);
        var brimY = headY + (headR * 0.55f);
        switch (look.Hat)
        {
            case Hat.Bucket:
                body.Cone(new Vector3(0, brimY + 0.01f, 0), new Vector3(headR * 3.0f, 0.1f, headR * 3.0f), colour);
                body.Tube(new Vector3(0, brimY + 0.1f, 0), new Vector3(headR * 1.85f, 0.14f, headR * 1.85f), colour.Darkened(0.06f));
                break;
            case Hat.Cowboy:
                body.Tube(new Vector3(0, brimY + 0.01f, 0), new Vector3(headR * 3.5f, 0.03f, headR * 3.2f), colour);
                body.Cone(new Vector3(0, brimY + 0.11f, 0), new Vector3(headR * 1.9f, 0.19f, headR * 1.8f), colour);
                body.Tube(new Vector3(0, brimY + 0.045f, 0), new Vector3(headR * 1.95f, 0.035f, headR * 1.85f), new Color("#6B4A2E"));
                break;
            case Hat.Sunhat:
                body.Tube(new Vector3(0, brimY, 0), new Vector3(headR * 3.8f, 0.025f, headR * 3.8f), colour);
                body.Ball(new Vector3(0, brimY + 0.03f, 0), new Vector3(headR * 2.05f, headR * 1.1f, headR * 2.05f), colour);
                body.Tube(new Vector3(0, brimY + 0.035f, 0), new Vector3(headR * 2.08f, 0.03f, headR * 2.08f), new Color("#E94F7B"));
                break;
            case Hat.Cap:
                body.Ball(new Vector3(0, brimY + 0.0f, -0.01f), new Vector3(headR * 2.12f, headR * 1.3f, headR * 2.12f), colour);
                body.Box(new Vector3(0, brimY + 0.0f, headR * 1.15f), new Vector3(headR * 1.3f, 0.022f, headR * 0.9f), colour.Darkened(0.1f));
                break;
            case Hat.CapBackwards:
                body.Ball(new Vector3(0, brimY + 0.0f, -0.01f), new Vector3(headR * 2.12f, headR * 1.3f, headR * 2.12f), colour);
                body.Box(new Vector3(0, brimY - 0.01f, -headR * 1.15f), new Vector3(headR * 1.3f, 0.022f, headR * 0.9f), colour.Darkened(0.1f), new Vector3(-12, 0, 0));
                break;
            case Hat.Beanie:
                body.Ball(new Vector3(0, brimY - 0.01f, -0.015f), new Vector3(headR * 2.18f, headR * 1.55f, headR * 2.18f), colour);
                body.Tube(new Vector3(0, brimY - 0.05f, -0.015f), new Vector3(headR * 2.24f, 0.07f, headR * 2.24f), colour.Darkened(0.12f));
                break;
        }
    }

    /// <summary>What stars wear on stage: an open jacket, dungarees, sparkles and a chain.</summary>
    private static void BuildStageWear(MeshBuilder body, PersonLook look, Vector3 torsoSize, Vector3 torsoAt, float depth, float shoulder, Color top, Color legColour)
    {
        var front = depth / 2;
        if (look.Jacket is { } jacketColour)
        {
            // The jacket over the top, open at the front so the top shows, with lapels.
            var jacket = new Color(jacketColour);
            var grown = new Vector3(torsoSize.X * 1.06f, torsoSize.Y * 0.98f, depth * 1.08f);
            body.Pill(torsoAt + new Vector3(0, -0.01f, 0), grown, jacket);
            var gap = shoulder * 0.42f;
            body.Box(new Vector3(0, 0.9f, front * 1.08f - 0.002f), new Vector3(gap * 2, 0.4f, 0.03f), top);
            if (look.TopDetail is { } print && look.Top == TopStyle.TShirt)
            {
                body.Ball(new Vector3(0, 0.95f, front * 1.08f + 0.012f), new Vector3(gap * 1.2f, gap * 1.2f, 0.03f), new Color(print));
            }

            foreach (var side in new[] { -1f, 1f })
            {
                body.Box(new Vector3(side * (gap + 0.025f), 1.02f, front * 1.08f + 0.006f), new Vector3(0.05f, 0.16f, 0.025f), jacket.Darkened(0.2f), new Vector3(0, 0, side * -18));
            }
        }

        if (look.Dungarees)
        {
            // A bib over the tummy, two straps over the shoulders and a button on each.
            body.Box(new Vector3(0, 0.79f, front - 0.004f), new Vector3(shoulder * 1.35f, 0.3f, 0.04f), legColour);
            body.Box(new Vector3(0, 0.86f, front + 0.014f), new Vector3(0.09f, 0.07f, 0.01f), legColour.Darkened(0.15f));
            foreach (var side in new[] { -1f, 1f })
            {
                body.Box(new Vector3(side * shoulder * 0.55f, 1.0f, front - 0.01f), new Vector3(0.045f, 0.2f, 0.035f), legColour);
                body.Box(new Vector3(side * shoulder * 0.55f, 0.88f, -front + 0.01f), new Vector3(0.045f, 0.46f, 0.035f), legColour);
                body.Box(new Vector3(side * shoulder * 0.55f, 1.15f, 0), new Vector3(0.045f, 0.03f, depth * 0.9f), legColour);
                body.Ball(new Vector3(side * shoulder * 0.55f, 0.92f, front + 0.02f), 0.03f, new Color("#F2C14E"));
            }
        }

        if (look.Sparkles is { } sparkleColour)
        {
            var sparkle = new Color(sparkleColour);
            var z = front * (look.Jacket is null ? 1f : 1.08f);
            for (var i = 0; i < 14; i++)
            {
                // Dots spread over the front, kept off the middle when a jacket hangs open.
                var x = ((i * 0.37f) % 1f) - 0.5f;
                if (look.Jacket is not null && Mathf.Abs(x) < 0.25f)
                {
                    x += x < 0 ? -0.22f : 0.22f;
                }

                var y = 0.68f + (((i * 0.61f) % 1f) * 0.42f);
                body.Ball(new Vector3(x * torsoSize.X * 0.95f, y, z - 0.002f), new Vector3(0.045f, 0.045f, 0.03f), sparkle);
            }
        }

        if (look.Chain is { } chainColour)
        {
            var chain = new Color(chainColour);
            for (var i = -3; i <= 3; i++)
            {
                var t = i / 3f;
                body.Ball(new Vector3(t * shoulder * 0.45f, 1.08f - ((1 - (t * t)) * 0.1f), front + 0.012f), 0.03f, chain);
            }

            body.Ball(new Vector3(0, 0.95f, front + 0.02f), 0.05f, chain);
        }
    }

    /// <summary>The VIP pass: a ribbon round the neck in a V down the chest and a white card with a black band.</summary>
    private static void BuildLanyard(MeshBuilder body, Color ribbon, float front, float shoulder)
    {
        var z = front + 0.014f;
        var neck = shoulder * 0.42f;
        foreach (var side in new[] { -1f, 1f })
        {
            // Over the shoulder, then down and in to meet at the clip.
            body.Box(new Vector3(side * neck, 1.15f, 0), new Vector3(0.035f, 0.022f, (front * 2) + 0.02f), ribbon);
            var top = new Vector3(side * neck, 1.15f, z);
            var bottom = new Vector3(side * 0.012f, 0.92f, z);
            var mid = (top + bottom) / 2;
            var tilt = Mathf.RadToDeg(Mathf.Atan2(top.X - bottom.X, top.Y - bottom.Y));
            body.Box(mid, new Vector3(0.035f, (top - bottom).Length(), 0.012f), ribbon, new Vector3(0, 0, -tilt));
        }

        body.Box(new Vector3(0, 0.9f, z + 0.004f), new Vector3(0.03f, 0.03f, 0.016f), new Color("#C9CDD2"));
        body.Box(new Vector3(0, 0.81f, z + 0.008f), new Vector3(0.11f, 0.14f, 0.012f), Colors.White);
        body.Box(new Vector3(0, 0.845f, z + 0.015f), new Vector3(0.11f, 0.04f, 0.004f), new Color("#1E1E24"));
        body.Box(new Vector3(0, 0.79f, z + 0.015f), new Vector3(0.06f, 0.02f, 0.004f), ribbon);
    }

    private static void BuildHeadphones(MeshBuilder body, PersonLook look, float headY, float headR)
    {
        if (look.Headphones is not { } colour)
        {
            return;
        }

        var c = new Color(colour);
        foreach (var side in new[] { -1f, 1f })
        {
            body.Tube(new Vector3(side * headR * 1.02f, headY, 0), new Vector3(headR * 0.75f, 0.07f, headR * 0.75f), new Color("#26292E"), new Vector3(0, 0, 90));
            body.Tube(new Vector3(side * headR * 1.09f, headY, 0), new Vector3(headR * 0.6f, 0.03f, headR * 0.6f), c, new Vector3(0, 0, 90));
        }

        // The band over the top of the head, in short straight pieces.
        const int pieces = 7;
        for (var i = 0; i < pieces; i++)
        {
            var a = Mathf.Pi * (i + 0.5f) / pieces;
            var at = new Vector3(Mathf.Cos(a) * headR * 1.08f, headY + (Mathf.Sin(a) * headR * 1.3f) + 0.01f, 0);
            var tilt = Mathf.RadToDeg(Mathf.Atan2(Mathf.Cos(a) * 1.3f, -Mathf.Sin(a) * 1.08f));
            body.Box(at, new Vector3(headR * 0.62f, 0.035f, 0.06f), new Color("#26292E"), new Vector3(0, 0, tilt));
        }
    }

    private Node3D BuildArm(string name, int side, float shoulder, Color sleeve, Color skin, float covered, Color? wristband)
    {
        const float thick = 0.105f;
        var arm = new MeshBuilder();
        arm.Pill(new Vector3(0, -ArmLength / 2, 0), new Vector3(thick, ArmLength, thick), skin);
        if (covered > 0)
        {
            var length = ArmLength * covered;
            arm.Pill(new Vector3(0, -length / 2, 0), new Vector3(thick * 1.14f, length + 0.03f, thick * 1.14f), sleeve);
        }

        arm.Ball(new Vector3(0, -ArmLength, 0), 0.115f, skin);
        if (wristband is { } band)
        {
            arm.Tube(new Vector3(0, -ArmLength + 0.085f, 0), new Vector3(thick * 1.2f, 0.035f, thick * 1.2f), band);
        }

        var node = arm.BuildNode(name, new Vector3(side * (shoulder + 0.045f), ShoulderY - HipY, 0));
        node.RotationDegrees = new Vector3(0, 0, side * 7f);
        _upper.AddChild(node);
        return node;
    }

    private Node3D BuildLeg(string name, int side, PersonLook look, Color skin, Color legColour, Color shoe)
    {
        const float thick = 0.115f;
        const float length = HipY - 0.05f;
        var leg = new MeshBuilder();
        var bare = look.Legs != LegStyle.Trousers;
        leg.Tube(new Vector3(0, -length / 2, 0), new Vector3(thick, length, thick), bare ? skin : legColour);
        if (look.Legs == LegStyle.Shorts)
        {
            leg.Tube(new Vector3(0, -0.11f, 0), new Vector3(thick * 1.25f, 0.24f, thick * 1.25f), legColour);
        }

        switch (look.Feet)
        {
            case Footwear.Wellies:
                leg.Tube(new Vector3(0, -length + 0.12f, 0), new Vector3(thick * 1.3f, 0.3f, thick * 1.3f), shoe);
                leg.Ball(new Vector3(0, -length + 0.0f, 0.045f), new Vector3(0.15f, 0.13f, 0.25f), shoe);
                break;
            case Footwear.Boots:
                leg.Tube(new Vector3(0, -length + 0.07f, 0), new Vector3(thick * 1.22f, 0.16f, thick * 1.22f), shoe);
                leg.Ball(new Vector3(0, -length + 0.0f, 0.04f), new Vector3(0.15f, 0.13f, 0.24f), shoe);
                break;
            case Footwear.Sandals:
                leg.Ball(new Vector3(0, -length + 0.0f, 0.04f), new Vector3(0.13f, 0.1f, 0.22f), skin);
                leg.Box(new Vector3(0, -length - 0.035f, 0.04f), new Vector3(0.14f, 0.03f, 0.24f), shoe);
                leg.Box(new Vector3(0, -length + 0.025f, 0.06f), new Vector3(0.145f, 0.03f, 0.04f), shoe);
                break;
            default:
                leg.Ball(new Vector3(0, -length + 0.0f, 0.045f), new Vector3(0.15f, 0.13f, 0.25f), shoe);
                leg.Box(new Vector3(0, -length - 0.04f, 0.045f), new Vector3(0.14f, 0.03f, 0.23f), Colors.White);
                break;
        }

        var node = leg.BuildNode(name, new Vector3(side * 0.078f, HipY, 0));
        _figure.AddChild(node);
        return node;
    }

    /// <summary>Moves the arms and legs for the current pose. Call every frame with the time passed.</summary>
    public void Animate(double delta, float walkSpeed = 1.3f)
    {
        var step = (float)delta;
        var walking = Pose == Pose.Walking;
        if (walking)
        {
            _stride += step * walkSpeed * 5.2f / (float)Math.Max(0.5, Look.Height);
        }
        else
        {
            // Come to rest with feet together.
            _stride = Mathf.Lerp(_stride, Mathf.Round(_stride / Mathf.Pi) * Mathf.Pi, Mathf.Min(1f, step * 10f));
        }

        _cheer = Mathf.MoveToward(_cheer, Pose == Pose.Cheering ? 1f : 0f, step * 4f);
        _dance = Mathf.MoveToward(_dance, Pose == Pose.Dancing ? 1f : 0f, step * 3f);
        _show = Mathf.MoveToward(_show, Pose == Pose.ShowingTicket ? 1f : 0f, step * 5f);
        _clap = Mathf.MoveToward(_clap, Pose == Pose.Clapping ? 1f : 0f, step * 5f);
        _perform = Mathf.MoveToward(_perform, Pose == Pose.Performing ? 1f : 0f, step * 6f);
        _sit = Mathf.MoveToward(_sit, Pose == Pose.Sitting ? 1f : 0f, step * 4f);
        _hold = Mathf.MoveToward(_hold, HeldUp ? 1f : 0f, step * 5f);
        ApplyPose();
    }

    /// <summary>
    /// Sets where the hands go while the pose is <see cref="Pose.Performing"/>: a point for each
    /// hand to reach towards, in metres in this person's own space (they face +Z, feet at 0), how
    /// far the body is lifted, and how the upper body leans (degrees).
    /// </summary>
    public void Perform(Vector3 leftHand, Vector3 rightHand, float lift = 0, Vector3 lean = default)
    {
        _performLeft = Aim(-1, leftHand);
        _performRight = Aim(1, rightHand);
        _performLift = lift;
        _performLean = lean;
    }

    /// <summary>Where a hand is now, in the world.</summary>
    public Vector3 HandAt(bool right) => (right ? _armRight : _armLeft).GlobalTransform * new Vector3(0, -ArmLength, 0);

    /// <summary>
    /// Opens the mouth, from 0 (the usual smile) to 1 (wide open, singing a big note). Only made
    /// the first time a mouth opens.
    /// </summary>
    public void Mouth(float open)
    {
        if (_mouth is null)
        {
            if (open < 0.08f)
            {
                return;
            }

            var y = _headY + _mouthY + (_headR * 0.04f);
            var z = Mathf.Sqrt(Mathf.Max(0.0001f, (_headR * _headR) - (_mouthY * _mouthY))) + 0.004f;
            _mouth = new MeshInstance3D
            {
                Name = "Mouth",
                Mesh = new SphereMesh { Radius = 0.5f, Height = 1f, RadialSegments = 12, Rings = 6 },
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("#5A2330"), Roughness = 0.9f },
                Position = new Vector3(0, y - HipY, z - 0.008f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _upper.AddChild(_mouth);
        }

        _mouth.Visible = open >= 0.08f;
        _mouth.Scale = new Vector3(_headR * (0.36f + (0.12f * open)), _headR * (0.1f + (0.42f * open)), 0.03f);
    }

    /// <summary>
    /// The arm angles (degrees) that point an arm from its shoulder towards a point in this
    /// person's space. An arm hangs straight down; X swings it forward (negative) and Z out to the side.
    /// </summary>
    private Vector3 Aim(int side, Vector3 target)
    {
        var scale = (float)Look.Height;
        var shoulder = new Vector3(side * _shoulderX * scale, ShoulderY * scale, 0);
        var d = target - shoulder;
        if (d.LengthSquared() < 0.0001f)
        {
            return new Vector3(0, 0, side * 7f);
        }

        d = d.Normalized();
        var z = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(d.X, -1f, 1f)));
        var x = Mathf.RadToDeg(Mathf.Atan2(-d.Z, -d.Y));
        return new Vector3(x, 0, z);
    }

    /// <summary>
    /// Sets how this person dances while their pose is <see cref="Pose.Dancing"/>: the move, how far
    /// through it they are (0 is the moment the beat lands), and how hard they are going at it.
    /// </summary>
    public void Dance(DanceMove move, float through, float energy)
    {
        _move = move;
        _through = through;
        _energy = energy;
    }

    /// <summary>Sets the pose straight away, with the walk frozen at a point in its stride (0 to 1).</summary>
    public void SetPose(Pose pose, float strideAt = 0.25f)
    {
        Pose = pose;
        _stride = pose == Pose.Walking ? strideAt * Mathf.Tau : 0;
        _cheer = pose == Pose.Cheering ? 1 : 0;
        _dance = pose == Pose.Dancing ? 1 : 0;
        _show = pose == Pose.ShowingTicket ? 1 : 0;
        _clap = pose == Pose.Clapping ? 1 : 0;
        _perform = pose == Pose.Performing ? 1 : 0;
        _sit = pose == Pose.Sitting ? 1 : 0;
        _hold = HeldUp ? 1 : 0;
        ApplyPose();
    }

    private void ApplyPose()
    {
        var swing = Mathf.Sin(_stride);
        var legSwing = swing * 28f;
        var armSwing = swing * 24f;
        _legLeft.RotationDegrees = new Vector3(legSwing, 0, 0);
        _legRight.RotationDegrees = new Vector3(-legSwing, 0, 0);

        // Cheering: both arms up and out, with a little wave.
        var wave = Mathf.Sin((float)Time.GetTicksMsec() / 140f + Look.Seed) * 8f * _cheer;
        var up = 155f * _cheer;
        var left = new Vector3(Mathf.Lerp(-armSwing, 0, _cheer), 0, -7f - up - wave);
        var right = new Vector3(Mathf.Lerp(armSwing, 0, _cheer), 0, 7f + up - wave);
        var lift = Mathf.Abs(Mathf.Cos(_stride)) * 0.012f * (Pose == Pose.Walking ? 1 : 0);
        var lean = Vector3.Zero;
        var turn = 0f;
        if (_dance > 0)
        {
            var (danceLeft, danceRight, danceLift, danceLean, danceTurn) = DancePose(_move, _through, _energy);
            left = left.Lerp(danceLeft, _dance);
            right = right.Lerp(danceRight, _dance);
            lift = Mathf.Lerp(lift, danceLift, _dance);
            lean = danceLean * _dance;
            turn = danceTurn * _dance;
        }

        if (_clap > 0)
        {
            // Clapping fast, each person in their own time: hands apart, then snapping together in front.
            var near = 0.5f + (0.5f * Mathf.Sin(((float)Time.GetTicksMsec() / 1000f * Mathf.Tau * 3.2f) + (Look.Seed * 1.3f)));
            var open = 30f * (1 - Clapped(near));
            left = left.Lerp(new Vector3(-82, 0, 21 - open), _clap);
            right = right.Lerp(new Vector3(-82, 0, -21 + open), _clap);
        }

        if (_perform > 0)
        {
            left = left.Lerp(_performLeft, _perform);
            right = right.Lerp(_performRight, _perform);
            lift = Mathf.Lerp(lift, _performLift, _perform);
            lean = lean.Lerp(_performLean, _perform);
            turn *= 1 - _perform;
        }

        if (_hold > 0)
        {
            // Whatever else they are doing, the hand with the drink or the food in it stays up and steady.
            left = left.Lerp(new Vector3(-72, 0, -6), _hold);
        }
        else if (Holding == BlueprintFestival.Simulation.Site.Carried.Bag)
        {
            // A bag swings less than an empty hand.
            left = new Vector3(left.X * 0.35f, 0, Mathf.Clamp(left.Z, -14f, 0f));
        }

        _armLeft.RotationDegrees = left;
        if (_held is not null)
        {
            // Held level, whichever way the arm is pointing.
            _held.RotationDegrees = HeldUp ? new Vector3(-left.X, 0, -left.Z) : Vector3.Zero;
        }

        right.X = Mathf.Lerp(right.X, -100f, _show);
        _armRight.RotationDegrees = right;

        // The ticket is only made the first time this person holds one out.
        if (_show > 0.05f && _ticket is null)
        {
            var card = new MeshBuilder();
            card.Box(Vector3.Zero, new Vector3(0.15f, 0.24f, 0.016f), new Color("#FFF6D8"));
            card.Box(new Vector3(0, 0.06f, 0), new Vector3(0.152f, 0.05f, 0.018f), new Color("#E8457E"));
            _ticket = card.BuildNode("Ticket", new Vector3(0, -ArmLength - 0.1f, 0.02f));
            _armRight.AddChild(_ticket);
        }

        if (_ticket is not null)
        {
            _ticket.Visible = _show > 0.05f;
        }

        if (_sit > 0)
        {
            // Sitting: the legs swing out in front and the body comes down until the hips are on the seat.
            _legLeft.RotationDegrees = new Vector3(Mathf.Lerp(legSwing, -72f, _sit), 0, -3f * _sit);
            _legRight.RotationDegrees = new Vector3(Mathf.Lerp(-legSwing, -72f, _sit), 0, 3f * _sit);
            lift = Mathf.Lerp(lift, SeatHeight + 0.07f - (HipY * (float)Look.Height), _sit);
        }

        _upper.RotationDegrees = lean;
        _figure.Position = new Vector3(0, lift, 0);
        _figure.RotationDegrees = new Vector3(0, turn, 0);
    }

    /// <summary>
    /// Where the arms are, how high the body is lifted, and how the upper body leans, partway
    /// through a dance move. Arm angles are in degrees: X swings an arm forward (negative) or back,
    /// and Z lifts it out to the side (the left arm negative, the right positive).
    /// </summary>
    /// <summary>
    /// How shut the hands are in a clap, from 0 (apart) to 1 (palms together), given how near the
    /// clap is (1 is the moment of the clap). The hands come together quickly and only touch right
    /// at the clap, so the eye reads the moment of contact where the sound is.
    /// </summary>
    private static float Clapped(float near) => Mathf.Pow(Mathf.Clamp(near, 0f, 1f), 6f);

    private static (Vector3 Left, Vector3 Right, float Lift, Vector3 Lean, float Turn) DancePose(DanceMove move, float through, float energy)
    {
        // How far through the beat itself, for moves that take two beats.
        var beats = Dancing.BeatsAMove(move);
        var inBeat = Mathf.PosMod(through * beats, 1f);
        var second = through * beats >= 1f;

        // The hit: a smooth swell that winds up, peaks at 1 exactly as the beat lands and eases away
        // again, with no sudden snap. A little narrower than a plain wave, so the beat still reads.
        var swell = 0.5f + (0.5f * Mathf.Cos(inBeat * Mathf.Tau));
        var hit = swell * swell * (3f - (2f * swell));

        // A smooth rise and fall once a beat: 0 on the beat, 1 halfway between.
        var wave = 0.5f - (0.5f * Mathf.Cos(inBeat * Mathf.Tau));
        var e = energy;
        switch (move)
        {
            case DanceMove.Bounce:
            {
                // Knees give on the beat and push back up; hands up, swaying the other way each beat.
                var side = Mathf.Sin(through * Mathf.Tau) * 22f * e;
                return (new Vector3(-20, 0, -150 - side), new Vector3(-20, 0, 150 - side), (0.07f * e * (1 - wave)) - 0.035f, new Vector3(4 * hit * e, 0, side * 0.25f), 0);
            }

            case DanceMove.HeadBang:
            {
                // Head and shoulders down on the beat, right fist punching up as it lands.
                var nod = 26f * e * hit;
                return (new Vector3(-25 - (10 * hit), 0, -18), new Vector3(-20, 0, 120 + (45 * (1 - hit) * e)), -0.03f * hit, new Vector3(nod, 0, 0), 0);
            }

            case DanceMove.Horns:
            {
                // A deep bang from the waist, both hands up making horns, pushed up on the beat.
                var nod = 38f * e * hit;
                var push = 20f * hit * e;
                return (new Vector3(-30, 0, -140 - push), new Vector3(-30, 0, 140 + push), -0.05f * hit, new Vector3(nod, 0, 0), 0);
            }

            case DanceMove.Jump:
            {
                // A jump that lands on the hit, alternate fists punching the air.
                // The jump itself takes one beat of music: up on the beat between, down on the hit.
                var air = inBeat > 0.5f ? Mathf.Sin((inBeat - 0.5f) * Mathf.Tau) * 0.22f * e : 0f;
                var punch = 165f - (35f * (1 - hit));
                return (new Vector3(-10, 0, second ? -punch : -55), new Vector3(-10, 0, second ? 55 : punch), air, new Vector3(-4 * hit, 0, 0), 0);
            }

            case DanceMove.Sway:
            {
                // One way and back over two hits, clapping in front on each hit: the hands meet only
                // right on the hit (when the clap is heard) and are apart the rest of the time.
                var sway = Mathf.Sin(through * Mathf.Tau) * 9f * e;
                var open = 36f * (1 - Clapped(swell));
                return (new Vector3(-75, 0, 20 - open), new Vector3(-75, 0, -20 + open), 0, new Vector3(0, 0, sway), sway * 0.8f);
            }

            case DanceMove.Nod:
            {
                // A dip and a nod on the beat, right hand out in front pumping down with it.
                var pump = 30f * hit * e;
                return (new Vector3(5, 0, -10), new Vector3(-95 + pump, 0, 18), -0.04f * hit * e, new Vector3(12 * hit * e, 0, 0), second ? -4f : 4f);
            }

            default:
                // Nodding along on the beat with hands by their sides.
                return (new Vector3(0, 0, -7), new Vector3(0, 0, 7), -0.012f * hit, new Vector3(8 * hit * e, 0, 0), 0);
        }
    }
}
