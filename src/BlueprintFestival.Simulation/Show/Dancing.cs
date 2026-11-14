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
        _ => DanceMove.Bounce,
    };

    /// <summary>
    /// How one person dances to some music: mostly the move that suits it, but one in five does
    /// something gentler that would suit it too, and one in ten just nods along.
    /// </summary>
    public static DanceMove MoveFor(Genre genre, int person)
    {
        var roll = Roll(person, 1) % 100;
        if (roll < 10)
        {
            return DanceMove.Tap;
        }

        if (roll < 30)
        {
            return genre switch
            {
                Genre.Metal => DanceMove.HeadBang,
                Genre.Rock => DanceMove.Bounce,
                Genre.Electronic => DanceMove.Bounce,
                Genre.HipHop => DanceMove.Bounce,
                Genre.Pop => DanceMove.Sway,
                _ => DanceMove.Tap,
            };
        }

        return MoveFor(genre);
    }

    /// <summary>
    /// How many beats apart someone clapping along claps (only the sway claps). The crowd's clap
    /// sound follows this too, so it matches the hands.
    /// </summary>
    public const int BeatsAClap = 1;

    /// <summary>
    /// How many beats one go of a move takes: a sway goes one way and back over two, a jump punches
    /// with one fist and then the other, and a hip hop nod leans one way and then the other.
    /// </summary>
    public static int BeatsAMove(DanceMove move) => move is DanceMove.Sway or DanceMove.Jump or DanceMove.Nod or DanceMove.Bounce ? 2 : 1;

    /// <summary>
    /// How far off the beat someone is, as a part of a beat: nobody is more than a twentieth of a
    /// beat early or late, which reads as a crowd in time but not in lockstep.
    /// </summary>
    public static double Lateness(int person) => ((Roll(person, 2) % 101) - 50) / 1000.0;

    /// <summary>How hard someone dances, from 0.65 (gently) to 1.15 (giving it everything).</summary>
    public static double Energy(int person) => 0.65 + ((Roll(person, 3) % 51) / 100.0);

    /// <summary>
    /// How far through their move someone is, from 0 to 1, where 0 is the moment the beat lands.
    /// </summary>
    /// <param name="beat">How many beats into the song it is (see <see cref="BeatInfo.BeatAt"/>).</param>
    public static double Through(double beat, DanceMove move, int person)
    {
        var length = BeatsAMove(move);
        var at = (beat - Lateness(person)) / length;
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
