namespace BlueprintFestival.Simulation.Show;

/// <summary>How someone in the crowd dances.</summary>
public enum DanceMove
{
    /// <summary>Bobbing up and down on every beat, hands swaying overhead side to side (pop).</summary>
    Bounce,

    /// <summary>Head and shoulders down hard on every beat, a fist pumping (rock).</summary>
    HeadBang,

    /// <summary>A deeper head-bang with both hands up making horns (metal).</summary>
    Horns,

    /// <summary>Jumping on every beat with a fist punching the air (electronic).</summary>
    Jump,

    /// <summary>Swaying side to side over two beats, clapping on the beat (country).</summary>
    Sway,

    /// <summary>A nod and a dip on every beat, one hand out in front pumping (hip hop).</summary>
    Nod,

    /// <summary>Just nodding along, for those who are not big dancers.</summary>
    Tap,

    /// <summary>Both arms up, waving together from side to side over two hits.</summary>
    Wave,

    /// <summary>Pointing up at the stage on every hit, one hand and then the other.</summary>
    Point,

    /// <summary>A side step, one way and then the other, turning with it and arms swinging low.</summary>
    Step,

    /// <summary>Clapping with both hands above the head on every hit.</summary>
    ClapHigh,

    /// <summary>Singing along in a chorus: both arms up, swaying slowly from side to side, head back.</summary>
    SingAlong,

    /// <summary>A shuffle: shoulders hunched, stepping on the spot, one elbow and then the other swinging up (indie).</summary>
    Shuffle,

    /// <summary>The pogo: straight up and down on the spot, arms stiff by the sides (punk).</summary>
    Pogo,

    /// <summary>A jig: hopping from one foot to the other, hands on the hips (folk).</summary>
    Jig,

    /// <summary>A skank: knees bending slowly, arms bent and rolling forward one after the other (reggae).</summary>
    Skank,

    /// <summary>A shimmy: shoulders shaking side to side, hands low and clicking (soul).</summary>
    Shimmy,

    /// <summary>The disco point: one hand up high to the side and then down across the body (disco).</summary>
    Disco,
}

/// <summary>Which beats someone dances on.</summary>
public enum DanceTiming
{
    /// <summary>On the hit, with most of the crowd (every other beat).</summary>
    OnTheHit,

    /// <summary>On the other beat from most of the crowd: still in time, but the opposite way round.</summary>
    Offbeat,

    /// <summary>Twice as fast, on every beat.</summary>
    DoubleTime,
}

/// <summary>
/// Who in the crowd dances, how, and how far ahead of or behind the beat. Everybody is a little
/// different, so a crowd moves together without looking like clockwork; and the same person always
/// dances the same way.
/// </summary>
public static class Dancing
{
    /// <summary>The move that suits each sort of music.</summary>
    public static DanceMove MoveFor(Genre genre) => genre switch
    {
        Genre.Pop => DanceMove.Bounce,
        Genre.Rock => DanceMove.HeadBang,
        Genre.Metal => DanceMove.Horns,
        Genre.Electronic => DanceMove.Jump,
        Genre.Country => DanceMove.Sway,
        Genre.HipHop => DanceMove.Nod,
        Genre.Indie => DanceMove.Shuffle,
        Genre.Punk => DanceMove.Pogo,
        Genre.Folk => DanceMove.Jig,
        Genre.Reggae => DanceMove.Skank,
        Genre.Soul => DanceMove.Shimmy,
        Genre.Disco => DanceMove.Disco,
        _ => DanceMove.Bounce,
    };

    /// <summary>
    /// How one person dances to some music: about two in three do the move that suits it; one in eight
    /// does something gentler that would suit it too; about one in seven dances a different style of
    /// their own (see <see cref="OtherStyles"/>), so each stage's crowd does not all look the same; and
    /// the rest just nod along.
    /// </summary>
    public static DanceMove MoveFor(Genre genre, int person)
    {
        var roll = Roll(person, 1) % 100;
        if (roll < 8)
        {
            return DanceMove.Tap;
        }

        if (roll < 20)
        {
            return genre switch
            {
                Genre.Metal => DanceMove.HeadBang,
                Genre.Rock => DanceMove.Bounce,
                Genre.Electronic => DanceMove.Bounce,
                Genre.HipHop => DanceMove.Bounce,
                Genre.Pop => DanceMove.Sway,
                Genre.Country => DanceMove.Step,
                Genre.Indie => DanceMove.Bounce,
                Genre.Punk => DanceMove.HeadBang,
                Genre.Folk => DanceMove.Sway,
                Genre.Reggae => DanceMove.Step,
                Genre.Soul => DanceMove.Sway,
                Genre.Disco => DanceMove.Bounce,
                _ => DanceMove.Tap,
            };
        }

        if (roll < 35)
        {
            var styles = OtherStyles(genre);
            return styles[Roll(person, 4) % styles.Count];
        }

        return MoveFor(genre);
    }

    /// <summary>
    /// The different styles someone might dance to some music instead of the crowd's move: each still
    /// suits the music (nobody head-bangs to country or jumps to hip hop).
    /// </summary>
    public static IReadOnlyList<DanceMove> OtherStyles(Genre genre) => genre switch
    {
        Genre.Pop => PopStyles,
        Genre.Rock => RockStyles,
        Genre.Metal => MetalStyles,
        Genre.Electronic => ElectronicStyles,
        Genre.Country => CountryStyles,
        Genre.HipHop => HipHopStyles,
        Genre.Indie => IndieStyles,
        Genre.Punk => PunkStyles,
        Genre.Folk => FolkStyles,
        Genre.Reggae => ReggaeStyles,
        Genre.Soul => SoulStyles,
        Genre.Disco => DiscoStyles,
        _ => PopStyles,
    };

    private static readonly DanceMove[] PopStyles = { DanceMove.Wave, DanceMove.ClapHigh, DanceMove.Step, DanceMove.Point };
    private static readonly DanceMove[] RockStyles = { DanceMove.Jump, DanceMove.Point, DanceMove.Wave, DanceMove.ClapHigh };
    private static readonly DanceMove[] MetalStyles = { DanceMove.Jump, DanceMove.Point, DanceMove.ClapHigh };
    private static readonly DanceMove[] ElectronicStyles = { DanceMove.Wave, DanceMove.Point, DanceMove.Step, DanceMove.ClapHigh };
    private static readonly DanceMove[] CountryStyles = { DanceMove.Step, DanceMove.ClapHigh, DanceMove.Wave };
    private static readonly DanceMove[] HipHopStyles = { DanceMove.Point, DanceMove.Wave, DanceMove.Step, DanceMove.Bounce };
    private static readonly DanceMove[] IndieStyles = { DanceMove.Jump, DanceMove.Point, DanceMove.ClapHigh, DanceMove.Step };
    private static readonly DanceMove[] PunkStyles = { DanceMove.Jump, DanceMove.Point, DanceMove.Horns };
    private static readonly DanceMove[] FolkStyles = { DanceMove.Step, DanceMove.ClapHigh, DanceMove.Wave };
    private static readonly DanceMove[] ReggaeStyles = { DanceMove.Wave, DanceMove.Sway, DanceMove.Point };
    private static readonly DanceMove[] SoulStyles = { DanceMove.Step, DanceMove.ClapHigh, DanceMove.Wave, DanceMove.Point };
    private static readonly DanceMove[] DiscoStyles = { DanceMove.Point, DanceMove.Step, DanceMove.ClapHigh, DanceMove.Wave };

    /// <summary>
    /// Which beats someone dances on: most on the hit with everyone else, about one in eight on the
    /// other beat (still in time, just the opposite way round), and about one in twelve twice as fast,
    /// on every beat. Those swaying and clapping along always clap on the hit, so the clapping heard
    /// matches their hands; and nobody jumps twice as fast.
    /// </summary>
    public static DanceTiming TimingOf(DanceMove move, int person)
    {
        if (move is DanceMove.Sway or DanceMove.SingAlong)
        {
            return DanceTiming.OnTheHit;
        }

        var roll = Roll(person, 5) % 100;
        if (roll < 12)
        {
            return DanceTiming.Offbeat;
        }

        if (roll < 20 && move is not (DanceMove.Jump or DanceMove.Wave or DanceMove.Step or DanceMove.Pogo or DanceMove.Jig or DanceMove.Skank))
        {
            return DanceTiming.DoubleTime;
        }

        return DanceTiming.OnTheHit;
    }

    /// <summary>
    /// How many beats apart someone clapping along claps (only the sway claps): once a hit, like
    /// every other move. The crowd's clap sound follows this too, so it matches the hands.
    /// </summary>
    public const int BeatsAClap = BeatsAHit;

    /// <summary>
    /// How many hits one go of a move takes (a hit is every other beat, see <see cref="BeatsAHit"/>):
    /// a sway goes one way and back over two, a jump punches with one fist and then the other, and
    /// a hip hop nod leans one way and then the other.
    /// </summary>
    public static int BeatsAMove(DanceMove move) =>
        move is DanceMove.Sway or DanceMove.Jump or DanceMove.Nod or DanceMove.Bounce or DanceMove.Wave or DanceMove.Point or DanceMove.Step or DanceMove.SingAlong
            or DanceMove.Shuffle or DanceMove.Jig or DanceMove.Shimmy or DanceMove.Disco ? 2 : 1;

    /// <summary>
    /// How far off the beat someone is, as a part of a beat: nobody is more than a twentieth of a
    /// beat early or late, which reads as a crowd in time but not in lockstep.
    /// </summary>
    public static double Lateness(int person) => ((Roll(person, 2) % 101) - 50) / 1000.0;

    /// <summary>How hard someone dances, from 0.65 (gently) to 1.15 (giving it everything).</summary>
    public static double Energy(int person) => 0.65 + ((Roll(person, 3) % 51) / 100.0);

    /// <summary>
    /// The crowd moves on every other beat of the music: each nod, clap or bounce then has two beats
    /// to wind up and land, which looks smooth and unhurried but is still in time.
    /// </summary>
    public const int BeatsAHit = 2;

    /// <summary>
    /// How far through their move someone is, from 0 to 1, where 0 is the moment it lands: on every
    /// other beat of the music, or for some on the beats in between or on every beat (see
    /// <see cref="TimingOf"/>).
    /// </summary>
    /// <param name="beat">How many beats into the song it is (see <see cref="BeatInfo.BeatAt"/>).</param>
    public static double Through(double beat, DanceMove move, int person)
    {
        var length = BeatsAMove(move);
        var timing = TimingOf(move, person);
        var hit = timing == DanceTiming.DoubleTime ? 1 : BeatsAHit;
        var offset = timing == DanceTiming.Offbeat ? 1 : 0;
        var at = (beat - Lateness(person) - offset) / (length * hit);
        return at - Math.Floor(at);
    }

    /// <summary>
    /// The beat the crowd at a stage moves to, as how many beats in. The song that is heard keeps
    /// its own beat, from its beat file. Otherwise (no songs, the beat not worked out yet, or a
    /// stage that is not the one being heard) a steady beat that suits the music is counted from
    /// the start of the set.
    /// </summary>
    /// <param name="beat">The song's beat, if it is known and the song is being heard.</param>
    /// <param name="songSeconds">How far into the song it is, in seconds.</param>
    /// <param name="setSeconds">How long the act has been playing, in seconds, for the steady beat.</param>
    public static double BeatNow(BeatInfo? beat, double songSeconds, Genre genre, double setSeconds) =>
        beat is not null ? beat.BeatAt(songSeconds) : BeatInfo.ForGenre(genre).BeatAt(setSeconds);

    /// <summary>A number made from a person and a question about them, the same every time.</summary>
    private static int Roll(int person, int question)
    {
        unchecked
        {
            var x = (uint)((person * 73856093) ^ (question * 19349663));
            x ^= x >> 13;
            x *= 0x5bd1e995;
            x ^= x >> 15;
            return (int)(x & 0x7fffffff);
        }
    }
}
