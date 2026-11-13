using System;
using BlueprintFestival.Simulation.People;
using Godot;

namespace BlueprintFestival.World;

/// <summary>What a person is doing with their body.</summary>
public enum Pose
{
    Standing,
    Walking,
    Cheering,
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
    private Node3D _armLeft = null!;
    private Node3D _armRight = null!;
    private Node3D _legLeft = null!;
    private Node3D _legRight = null!;
    private float _stride;
    private float _cheer;

    public PersonLook Look { get; private set; } = null!;

    public Pose Pose { get; set; } = Pose.Standing;

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

        // Children have bigger heads for their size, as real children do.
        var headR = HeadR * (kid ? 1.22f : look.Age == AgeGroup.Teenager ? 1.06f : 1f);
        var headY = HeadY + (headR - HeadR);
        var shoulder = 0.185f * (0.75f + (0.25f * build)) * (look.Female ? 0.95f : 1.03f);
        var depth = 0.25f * (0.85f + (0.15f * build));

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

        // ---- head and face
        body.Ball(new Vector3(0, headY, 0), headR * 2, skin);
        BuildFace(body, look, headY, headR, hair);
        if (poncho is null)
        {
            BuildHair(body, look, headY, headR, hair);
            BuildHat(body, look, headY, headR);
        }

        _figure.AddChild(body.BuildNode("Body"));

        // ---- arms: sleeves to match the top, a hand, and the festival wristband on the right wrist
        var sleeve = poncho ?? top;
        var covered = poncho is not null ? 0.55f : look.Top switch
        {
            TopStyle.Vest => 0f,
            TopStyle.LongSleeve or TopStyle.Hoodie => 1f,
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

        var node = arm.BuildNode(name, new Vector3(side * (shoulder + 0.045f), ShoulderY, 0));
        node.RotationDegrees = new Vector3(0, 0, side * 7f);
        _figure.AddChild(node);
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
        ApplyPose();
    }

    /// <summary>Sets the pose straight away, with the walk frozen at a point in its stride (0 to 1).</summary>
    public void SetPose(Pose pose, float strideAt = 0.25f)
    {
        Pose = pose;
        _stride = pose == Pose.Walking ? strideAt * Mathf.Tau : 0;
        _cheer = pose == Pose.Cheering ? 1 : 0;
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
        _armLeft.RotationDegrees = new Vector3(Mathf.Lerp(-armSwing, 0, _cheer), 0, -7f - up - wave);
        _armRight.RotationDegrees = new Vector3(Mathf.Lerp(armSwing, 0, _cheer), 0, 7f + up - wave);
        _figure.Position = new Vector3(0, Mathf.Abs(Mathf.Cos(_stride)) * 0.012f * (Pose == Pose.Walking ? 1 : 0), 0);
    }
}
