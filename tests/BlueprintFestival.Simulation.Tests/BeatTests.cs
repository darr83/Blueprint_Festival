using BlueprintFestival.Simulation.Show;

namespace BlueprintFestival.Simulation.Tests;

public class BeatTests
{
    private const int Rate = 22050;

    /// <summary>
    /// A made-up song: a kick drum on every beat, a quieter hi-hat between beats, and a little hiss
    /// throughout, at the times given.
    /// </summary>
    private static float[] Song(IReadOnlyList<double> beats, double seconds, int seed = 1)
    {
        var random = new Random(seed);
        var samples = new float[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)((random.NextDouble() - 0.5) * 0.02);
        }

        void Hit(double at, double loud, double pitch, double decay, bool noisy)
        {
            var start = (int)(at * Rate);
            for (var i = 0; i < Rate / 5 && start + i < samples.Length; i++)
            {
                if (start + i < 0)
                {
                    continue;
                }

                var t = (double)i / Rate;
                var tone = noisy ? random.NextDouble() - 0.5 : Math.Sin(2 * Math.PI * pitch * t * (1 + (2 * Math.Exp(-t * 40))));
                samples[start + i] += (float)(loud * tone * Math.Exp(-t * decay));
            }
        }

        for (var b = 0; b < beats.Count; b++)
        {
            Hit(beats[b], 0.8, 55, 18, noisy: false);
            if (b + 1 < beats.Count)
            {
                Hit((beats[b] + beats[b + 1]) / 2, 0.25, 0, 60, noisy: true);
            }
        }

        return samples;
    }

    private static List<double> Steady(double bpm, double first, double seconds)
    {
        var beats = new List<double>();
        for (var t = first; t < seconds; t += 60 / bpm)
        {
            beats.Add(t);
        }

        return beats;
    }

    /// <summary>How far, in seconds, the found beat is from the real one at its worst, over the whole song.</summary>
    private static double WorstMiss(BeatInfo found, IReadOnlyList<double> real)
    {
        var worst = 0.0;
        foreach (var at in real.Skip(2).SkipLast(2))
        {
            var beat = found.BeatAt(at);
            var off = Math.Abs(beat - Math.Round(beat)) * found.BeatLength;
            worst = Math.Max(worst, off);
        }

        return worst;
    }

    [Theory]
    [InlineData(120, 0.30)]
    [InlineData(128, 0.05)]
    [InlineData(92, 0.61)]
    [InlineData(104, 0.0)]
    public void FindsTheTempoAndFirstBeatOfASteadySong(double bpm, double first)
    {
        var real = Steady(bpm, first, 60);
        var found = BeatFinder.Find(Song(real, 60), Rate);

        Assert.NotNull(found);
        Assert.InRange(found!.Bpm, bpm * 0.995, bpm * 1.005);
        Assert.True(WorstMiss(found, real) < 0.03, $"Out of time by {WorstMiss(found, real):0.000}s");
        Assert.InRange(found.Strength, 0.3, 1);
    }

    [Fact]
    public void StaysInTimeWhenThePlayersSpeedUp()
    {
        // Starts at 110 beats a minute and ends at 118, as a live band might.
        var real = new List<double>();
        for (var t = 0.4; t < 90; t += 60 / (110 + (8 * t / 90)))
        {
            real.Add(t);
        }

        var found = BeatFinder.Find(Song(real, 90, seed: 3), Rate);

        Assert.NotNull(found);
        Assert.True(found!.Beats.Count > real.Count * 0.95, "Every beat should be found.");
        Assert.True(WorstMiss(found, real) < 0.04, $"Out of time by {WorstMiss(found, real):0.000}s");
    }

    [Fact]
    public void FindsNoBeatInSilenceOrInATooShortSong()
    {
        Assert.Null(BeatFinder.Find(new float[Rate * 20], Rate));
        Assert.Null(BeatFinder.Find(Song(Steady(120, 0, 3), 3), Rate));
    }

    [Fact]
    public void ABeatFileReadsBackTheSame()
    {
        var beat = new BeatInfo(123.456, 0.25, 0.8) { Beats = [0.25, 0.74, 1.23, 1.71] };
        var read = BeatInfo.Read(beat.Write());

        Assert.NotNull(read);
        Assert.Equal(123.456, read!.Bpm, 3);
        Assert.Equal(0.25, read.FirstBeat, 3);
        Assert.Equal(0.8, read.Strength, 2);
        Assert.Equal(beat.Beats, read.Beats);
    }

    [Fact]
    public void ABeatFileEditedByHandIsRead()
    {
        var read = BeatInfo.Read("# my own\r\nbpm = 96\r\nfirst beat = 1.5\r\n");

        Assert.NotNull(read);
        Assert.Equal(96, read!.Bpm);
        Assert.Equal(1.5, read.FirstBeat);
        Assert.Empty(read.Beats);
        Assert.Equal(0, read.BeatAt(1.5), 6);
        Assert.Equal(2, read.BeatAt(1.5 + 1.25), 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bpm = fast")]
    [InlineData("bpm = 12")]
    [InlineData("bpm = 900")]
    [InlineData("first beat = 0.2")]
    public void ABeatFileWithNoSensibleTempoIsIgnored(string text) => Assert.Null(BeatInfo.Read(text));

    [Fact]
    public void BeatsFoundOneByOneAreCountedBetween()
    {
        var beat = new BeatInfo(120, 1, 1) { Beats = [1.0, 1.5, 2.1, 2.6] };

        Assert.Equal(0, beat.BeatAt(1.0), 6);
        Assert.Equal(1.5, beat.BeatAt(1.8), 6);
        Assert.Equal(3, beat.BeatAt(2.6), 6);

        // Before the first and after the last, the steady beat carries on.
        Assert.Equal(-1, beat.BeatAt(0.5), 6);
        Assert.Equal(4, beat.BeatAt(3.1), 6);
    }

    [Fact]
    public void EverySortOfMusicHasASteadyBeatToFallBackOn()
    {
        foreach (var genre in Enum.GetValues<Genre>())
        {
            var beat = BeatInfo.ForGenre(genre);
            Assert.InRange(beat.Bpm, 80, 150);
        }

        Assert.True(BeatInfo.ForGenre(Genre.Metal).Bpm > BeatInfo.ForGenre(Genre.Country).Bpm);
        Assert.True(BeatInfo.ForGenre(Genre.HipHop).Bpm < BeatInfo.ForGenre(Genre.Electronic).Bpm);
    }

    [Fact]
    public void TheBeatFileSitsBesideTheSong() =>
        Assert.Equal("01 Hay Fever.mp3.beat", BeatInfo.FileFor("01 Hay Fever.mp3"));

    [Fact]
    public void ABeatFileIsNotTakenForASong() => Assert.Null(SongFile.Read("01 Hay Fever.mp3.beat"));
}
