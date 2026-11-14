using System.Collections.Generic;
using BlueprintFestival.Simulation.Show;
using Godot;

namespace BlueprintFestival.World;

/// <summary>One member of the act on stage, as drawn: the performer, what they play, and their sticks or bow.</summary>
public sealed class BandMember
{
    public BandMember(Node3D node, BandRole role, Vector3 at, int place)
    {
        Node = node;
        Role = role;
        At = at;
        Place = place;
        Person = node.GetNode<PersonView>("Person");
        Home = Person.Position;
        if (node.GetNodeOrNull<Node3D>("Kit") is { } kit)
        {
            foreach (var child in kit.GetChildren())
            {
                if (child is Node3D piece)
                {
                    Kit[piece.Name] = piece;
                }
            }
        }

        if (role == BandRole.Drummer)
        {
            Left = Performers.Stick();
            Right = Performers.Stick();
            node.AddChild(Left);
            node.AddChild(Right);
        }
        else if (role == BandRole.Fiddler)
        {
            Right = Performers.Bow();
            node.AddChild(Right);
        }
    }

    public Node3D Node { get; }

    public PersonView Person { get; }

    public BandRole Role { get; }

    /// <summary>Where they stand on the stage.</summary>
    public Vector3 At { get; }

    /// <summary>Their place in the act (0 for the first).</summary>
    public int Place { get; }

    /// <summary>Where the person stands (or sits) within their spot.</summary>
    public Vector3 Home { get; }

    /// <summary>The drums and cymbals, by name, for a drummer.</summary>
    public Dictionary<string, Node3D> Kit { get; } = new();

    /// <summary>The left stick, for a drummer.</summary>
    public Node3D? Left { get; }

    /// <summary>The right stick, or a fiddler's bow.</summary>
    public Node3D? Right { get; }

    /// <summary>Where each hand is reaching, smoothed so a change of move does not snap.</summary>
    public Vector3 LeftHand { get; set; }

    public Vector3 RightHand { get; set; }

    public bool Started { get; set; }

    /// <summary>The tip of each stick, in the member's own space, as last drawn.</summary>
    public Vector3 LeftTip { get; set; }

    public Vector3 RightTip { get; set; }

    /// <summary>How wide the singer's mouth is open, from 0 to 1, as last drawn.</summary>
    public float MouthOpen { get; set; }
}

/// <summary>
/// How each member of an act plays along with the music: the drummer's sticks come down on the
/// drums on the beat (hi-hat, snare on two and four, a fill round the toms every fourth bar) and the
/// drums shake as they are hit; guitars are strummed down on each beat and up between; the singer
/// sings into the mic with their mouth moving to the singing, waves, points and claps over their
/// head; the keyboard player plays; the DJ scratches and punches the air. Between songs they stop
/// and thank the crowd, and at the end of the set they wave and bow.
/// </summary>
public static class BandMoves
{
    private static readonly Drum[] Drums = { Drum.HiHat, Drum.Snare, Drum.HighTom, Drum.LowTom, Drum.FloorTom, Drum.Crash };

    public static void Animate(BandMember m, StageBeat stage, double time, double delta)
    {
        var beat = stage.Beat;
        var moment = stage.Moment;
        var h = (float)m.Person.Look.Height;
        var t = (float)time + (m.Place * 0.7f);
        // A smooth bob that peaks on every beat.
        var bob = 0.5f + (0.5f * Mathf.Cos((float)(beat - System.Math.Floor(beat)) * Mathf.Tau));
        var playing = moment == StageMoment.Song;
        Vector3 left, right;
        var lift = 0f;
        var lean = Vector3.Zero;
        var nodeAt = m.At;
        var turn = 0f;
        var personAt = m.Home;
        var quick = 14f;
        var mouth = 0f;

        if (moment == StageMoment.Applause && m.Role != BandRole.Drummer)
        {
            // Arms up and waving to the crowd, with a deep bow every few seconds.
            var cycle = Mathf.PosMod(t, 4.5f);
            var bowing = cycle < 1.4f ? Mathf.Sin(cycle / 1.4f * Mathf.Pi) : 0f;
            var wave = Mathf.Sin(t * 7f) * 0.08f;
            left = new Vector3(-0.38f + wave, 1.95f * h, 0.25f).Lerp(new Vector3(-0.22f, 0.55f * h, 0.2f), bowing);
            right = new Vector3(0.38f - wave, 1.95f * h, 0.25f).Lerp(new Vector3(0.22f, 0.55f * h, 0.2f), bowing);
            lean = new Vector3(40f * bowing, 0, 0);
            m.Person.Pose = Pose.Performing;
        }
        else
        {
            switch (m.Role)
            {
                case BandRole.Drummer:
                    (left, right) = Drummer(m, beat, moment, t, out personAt);
                    lean = new Vector3(playing ? 7f * bob : 0f, 0, 0);
                    quick = 45f;
                    break;

                case BandRole.Guitarist:
                case BandRole.Bassist:
                    (left, right) = Guitar(m, beat, moment, t, h);
                    if (playing)
                    {
                        nodeAt += new Vector3(Mathf.Sin((float)beat * Mathf.Pi * 0.5f) * 0.1f, 0.03f * bob, 0);
                        turn = Mathf.Sin(((float)beat * Mathf.Pi * 0.25f) + m.Place) * 12f;
                        lean = new Vector3(6f * bob, 0, 0);
                        nodeAt.Y += Jump(beat, stage.Genre);
                    }

                    quick = 40f;
                    break;

                case BandRole.Keys:
                    (left, right) = Keys(beat, moment, t, h);
                    lean = new Vector3(playing ? 6f * bob : 0f, 0, 0);
                    quick = 30f;
                    break;

                case BandRole.Dj:
                    (left, right) = Dj(beat, moment, t, h);
                    if (playing)
                    {
                        personAt += new Vector3(0, 0.06f * bob, 0);
                        lean = new Vector3(10f * bob, 0, 0);
                    }

                    quick = 30f;
                    break;

                case BandRole.Fiddler:
                    (left, right) = Fiddle(beat, moment, t, h);
                    if (playing)
                    {
                        nodeAt += new Vector3(Mathf.Sin((float)beat * Mathf.Pi * 0.5f) * 0.08f, 0.03f * bob, 0);
                        turn = Mathf.Sin(((float)beat * Mathf.Pi * 0.25f) + m.Place) * 10f;
                    }

                    quick = 30f;
                    break;

                default:
                    (left, right) = Singer(beat, moment, stage.InMoment, t, h, out mouth);
                    if (playing)
                    {
                        personAt += new Vector3(0, 0.035f * bob, 0);
                        turn = Mathf.Sin(((float)beat * Mathf.Pi * 0.125f) + 1f) * 10f;
                        lean = new Vector3(5f * bob, 0, Mathf.Sin((float)beat * Mathf.Pi * 0.5f) * 4f);
                        // The mouth moves with the singing that is heard; on a stage that is not heard, it just keeps moving.
                        mouth = stage.Heard ? stage.Voice : 0.25f + (0.35f * Mathf.Abs(Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 1.7f)));
                    }

                    break;
            }

            m.Person.Pose = Pose.Performing;
        }

        m.Node.Position = nodeAt;
        m.Node.RotationDegrees = new Vector3(0, turn, 0);
        m.Person.Position = m.Person.Position.Lerp(personAt, m.Started ? Mathf.Min(1f, (float)delta * 10f) : 1f);

        // The hands move quickly but not instantly to where they should be, so a new move never snaps.
        var k = m.Started ? 1f - Mathf.Exp(-quick * (float)delta) : 1f;
        m.LeftHand = m.LeftHand.Lerp(left, k);
        m.RightHand = m.RightHand.Lerp(right, k);
        m.Started = true;
        m.Person.Perform(m.LeftHand - m.Person.Position, m.RightHand - m.Person.Position, lift, lean);
        m.Person.Animate(delta);
        if (m.Role == BandRole.Singer)
        {
            m.MouthOpen = Mathf.MoveToward(m.MouthOpen, mouth, (float)delta * 12f);
            m.Person.Mouth(m.MouthOpen);
        }

        if (m.Role == BandRole.Drummer)
        {
            Sticks(m, beat, moment);
            Shake(m, beat, playing);
        }
        else if (m.Role == BandRole.Fiddler && m.Right is { } bow)
        {
            var hand = m.Node.ToLocal(m.Person.HandAt(true));
            Place(bow, hand, hand + new Vector3(-0.62f, 0.06f, -0.12f));
        }
    }

    /// <summary>How high a rock or metal player jumps on the first beat of every eighth bar.</summary>
    private static float Jump(double beat, Genre genre)
    {
        if (genre is not (Genre.Rock or Genre.Metal))
        {
            return 0;
        }

        var bar = System.Math.Floor(beat / Performing.BeatsABar);
        var inBar = (float)(beat - (bar * Performing.BeatsABar));
        return ((long)bar % 8) == 7 && inBar > 3f ? Mathf.Sin((inBar - 3f) * Mathf.Pi) * 0.22f : 0f;
    }

    private static (Vector3 Left, Vector3 Right) Drummer(BandMember m, double beat, StageMoment moment, float t, out Vector3 personAt)
    {
        personAt = m.Home;
        if (moment == StageMoment.Applause)
        {
            // Stands up behind the kit and holds the sticks up high.
            personAt = m.Home + new Vector3(0, Performers.DrummerSits, -0.05f);
            var wave = Mathf.Sin(t * 6f) * 0.06f;
            return (new Vector3(-0.32f + wave, 1.9f, 0.1f), new Vector3(0.32f - wave, 1.9f, 0.1f));
        }

        if (moment != StageMoment.Song)
        {
            // Sticks resting on the snare and hi-hat.
            return (Hand(Performers.DrumTop(Drum.Snare), 0.1f), Hand(Performers.DrumTop(Drum.HiHat), 0.1f));
        }

        return (Hand(Tip(beat, false, out var liftL), liftL), Hand(Tip(beat, true, out var liftR), liftR));
    }

    /// <summary>Where a stick's tip is: on the drum it is coming down on, lifted above it by the swing.</summary>
    private static Vector3 Tip(double beat, bool right, out float lift)
    {
        var (up, from, to, across) = Performing.Stick(beat, right);
        lift = (float)up;
        return Performers.DrumTop(from).Lerp(Performers.DrumTop(to), (float)across);
    }

    /// <summary>Where the hand holding a stick is, for the stick to come down on a point: behind it and above, higher as the stick lifts.</summary>
    private static Vector3 Hand(Vector3 tip, float lift) =>
        new(tip.X * 0.82f, tip.Y + 0.1f + (0.2f * lift), tip.Z - 0.3f + (0.04f * lift));

    private static void Sticks(BandMember m, double beat, StageMoment moment)
    {
        if (m.Left is null || m.Right is null)
        {
            return;
        }

        var handL = m.Node.ToLocal(m.Person.HandAt(false));
        var handR = m.Node.ToLocal(m.Person.HandAt(true));
        Vector3 tipL, tipR;
        if (moment == StageMoment.Applause)
        {
            tipL = handL + new Vector3(-0.08f, 0.3f, 0.06f);
            tipR = handR + new Vector3(0.08f, 0.3f, 0.06f);
        }
        else if (moment != StageMoment.Song)
        {
            tipL = Performers.DrumTop(Drum.Snare);
            tipR = Performers.DrumTop(Drum.HiHat);
        }
        else
        {
            // The tip of the stick is on the drum at the hit and rises with the swing.
            tipL = Tip(beat, false, out var liftL) + new Vector3(0, 0.3f * liftL, -0.08f * liftL);
            tipR = Tip(beat, true, out var liftR) + new Vector3(0, 0.3f * liftR, -0.08f * liftR);
        }

        m.LeftTip = tipL;
        m.RightTip = tipR;
        Place(m.Left, handL, tipL);
        Place(m.Right, handR, tipR);
    }

    /// <summary>Lays a stick or bow (made along +Z, a metre long) from one point to another.</summary>
    private static void Place(Node3D stick, Vector3 from, Vector3 to)
    {
        var along = to - from;
        var length = along.Length();
        if (length < 0.01f)
        {
            return;
        }

        var basis = Basis.LookingAt(along / length, Mathf.Abs(along.Normalized().Y) > 0.98f ? Vector3.Forward : Vector3.Up, true);
        stick.Transform = new Transform3D(basis * Basis.FromScale(new Vector3(1, 1, length)), from);
    }

    /// <summary>The drums swell and the cymbals rock just after they are hit.</summary>
    private static void Shake(BandMember m, double beat, bool playing)
    {
        foreach (var drum in Drums)
        {
            if (!m.Kit.TryGetValue(drum.ToString(), out var piece))
            {
                continue;
            }

            var since = playing ? (float)Performing.SinceHit(beat, drum) : 99f;
            var kick = Mathf.Exp(-since * 9f);
            if (drum is Drum.HiHat or Drum.Crash)
            {
                var swing = (drum == Drum.Crash ? 9f : 4f) * Mathf.Exp(-since * 4f) * Mathf.Cos(since * 22f);
                piece.RotationDegrees = new Vector3(swing, 0, swing * 0.5f);
            }
            else
            {
                piece.Scale = new Vector3(1 + (0.08f * kick), 1 - (0.1f * kick), 1 + (0.08f * kick));
            }
        }

        if (m.Kit.TryGetValue("Kick", out var bass))
        {
            var since = playing ? (float)Performing.SinceKick(beat) : 99f;
            var kick = Mathf.Exp(-since * 9f);
            bass.Scale = Vector3.One * (1 + (0.06f * kick));
        }
    }

    private static (Vector3 Left, Vector3 Right) Guitar(BandMember m, double beat, StageMoment moment, float t, float h)
    {
        var bass = m.Role == BandRole.Bassist;
        var bar = (int)System.Math.Floor(beat / Performing.BeatsABar);
        // The fretting hand on the neck, moving between chords every bar.
        var chord = moment == StageMoment.Song ? ((bar * 7) % 4) - 1.5f : 0f;
        var u = (bass ? 0.42f : 0.34f) + (0.04f * chord);
        var neck = new Vector3(-0.1f - (0.82f * u), (0.9f * h) + 0.14f + (0.57f * u), 0.24f);
        if (moment == StageMoment.Song)
        {
            var strum = (float)Performing.Strum(beat) * (bass ? 0.04f : 0.09f);
            return (neck, new Vector3(0.14f, (0.9f * h) + strum, 0.34f));
        }

        if (moment == StageMoment.BetweenSongs && m.Place % 2 == 1)
        {
            // A wave to the crowd with the strumming hand.
            return (neck, new Vector3(0.36f + (Mathf.Sin(t * 7f) * 0.07f), 1.85f * h, 0.25f));
        }

        return (neck, new Vector3(0.14f, 0.9f * h, 0.3f));
    }

    private static (Vector3 Left, Vector3 Right) Keys(double beat, StageMoment moment, float t, float h)
    {
        var bar = (int)System.Math.Floor(beat / Performing.BeatsABar);
        var across = moment == StageMoment.Song ? Mathf.Sin(bar * 1.7f) * 0.06f : 0f;
        var eighth = (float)(beat * 2);
        var pressL = moment == StageMoment.Song ? Mathf.Exp(-Mathf.PosMod(eighth, 2f) * 4f) * 0.05f : 0f;
        var pressR = moment == StageMoment.Song ? Mathf.Exp(-Mathf.PosMod(eighth + 1f, 2f) * 4f) * 0.05f : 0f;
        var left = new Vector3(-0.22f + across, 1.04f - pressL, 0.44f);
        var right = new Vector3(0.24f + across, 1.04f - pressR, 0.44f);
        if (moment == StageMoment.BetweenSongs)
        {
            right = new Vector3(0.36f + (Mathf.Sin(t * 7f) * 0.07f), 1.85f * h, 0.25f);
        }

        return (left, right);
    }

    private static (Vector3 Left, Vector3 Right) Dj(double beat, StageMoment moment, float t, float h)
    {
        if (moment != StageMoment.Song)
        {
            var wave = Mathf.Sin(t * 7f) * 0.08f;
            return (new Vector3(-0.38f + wave, 1.95f * h, 0.25f), new Vector3(0.38f - wave, 1.95f * h, 0.25f));
        }

        var b = (float)beat;
        // Scratching the record with the right hand, back and forth on the beat.
        var right = new Vector3(0.45f + (Mathf.Sin(b * Mathf.Tau) * 0.05f), 1.1f, 0.42f + (Mathf.Cos(b * Mathf.Tau) * 0.04f));
        var bar = (int)System.Math.Floor(beat / Performing.BeatsABar);
        var punch = 0.5f + (0.5f * Mathf.Cos((b - Mathf.Floor(b)) * Mathf.Tau));
        // Every other two bars the left fist punches the air on the beat; otherwise it is on the mixer.
        var left = (bar / 2) % 2 == 1
            ? new Vector3(-0.3f, (1.55f + (0.35f * punch)) * h, 0.3f)
            : new Vector3(-0.1f + (Mathf.Sin(b * 0.7f) * 0.04f), 1.1f, 0.44f);
        return (left, right);
    }

    private static (Vector3 Left, Vector3 Right) Fiddle(double beat, StageMoment moment, float t, float h)
    {
        var left = new Vector3(-0.32f, 1.32f * h, 0.5f);
        if (moment != StageMoment.Song)
        {
            return (left, new Vector3(0.26f, 1.0f * h, 0.25f));
        }

        // Bowing: one way across the strings and back each beat.
        var saw = Mathf.Sin((float)beat * Mathf.Pi);
        return (left, new Vector3(0.12f + (0.16f * saw), 1.26f * h, 0.3f));
    }

    private static (Vector3 Left, Vector3 Right) Singer(double beat, StageMoment moment, double inMoment, float t, float h, out float mouth)
    {
        var mic = new Vector3(-0.04f, (1.24f * h) + 0.04f, 0.37f);
        var onMic = mic + new Vector3(0.03f, -0.06f, -0.04f);
        mouth = 0;
        if (moment == StageMoment.BetweenSongs)
        {
            // "Thank you!" into the mic, waving with the other hand.
            mouth = inMoment < 1.6 ? 0.45f * Mathf.Abs(Mathf.Sin(t * 11f)) : 0f;
            return (new Vector3(-0.38f + (Mathf.Sin(t * 7f) * 0.08f), 1.9f * h, 0.25f), onMic);
        }

        if (moment != StageMoment.Song)
        {
            return (new Vector3(-0.22f, 0.55f * h, 0.12f), onMic);
        }

        var b = (float)beat;
        var bob = 0.5f + (0.5f * Mathf.Cos((b - Mathf.Floor(b)) * Mathf.Tau));
        var phrase = (int)System.Math.Floor(beat / 16) % 4;
        switch (phrase)
        {
            case 1:
                // Pointing out over the crowd.
                return (new Vector3(-0.4f, (1.7f + (0.05f * bob)) * h, 0.5f), onMic);
            case 2:
                // Both hands on the mic, leaning in.
                return (mic + new Vector3(-0.04f, -0.1f, -0.04f), onMic);
            case 3:
            {
                // Clapping over their head on the beat, for the crowd to join in.
                var open = 1f - bob;
                return (new Vector3(-0.05f - (0.2f * open), 2.0f * h, 0.12f), new Vector3(0.05f + (0.2f * open), 2.0f * h, 0.12f));
            }

            default:
                // The free hand out to the side, moving with the beat.
                return (new Vector3(-0.5f, (1.0f + (0.12f * bob)) * h, 0.32f), onMic);
        }
    }
}
