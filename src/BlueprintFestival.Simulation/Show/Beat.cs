using System.Globalization;
using System.Text;

namespace BlueprintFestival.Simulation.Show;

/// <summary>
/// The beat of one song: how fast it is, in beats a minute, and how far into the song the first
/// beat falls. Worked out once from the song itself (see <see cref="BeatFinder"/>) and saved in a
/// small text file beside it, named after the song with ".beat" on the end, so a song called
/// "01 Hay Fever.mp3" has "01 Hay Fever.mp3.beat". The file can be edited by hand to correct it,
/// or deleted to have it worked out again.
/// </summary>
public sealed record BeatInfo(double Bpm, double FirstBeat, double Strength = 1)
{
    /// <summary>
    /// Every beat in the song, in seconds, when they have been found one by one. Players speed up
    /// and slow down a little, so these keep the crowd in time better than a steady beat would.
    /// </summary>
    public IReadOnlyList<double> Beats { get; init; } = [];

    /// <summary>What goes on the end of a song's file name to name its beat file.</summary>
    public const string Ending = ".beat";

    /// <summary>The slowest and fastest beats a song can have.</summary>
    public const double Slowest = 50;

    public const double Fastest = 220;

    /// <summary>How long one beat lasts, in seconds.</summary>
    public double BeatLength => 60 / Bpm;

    /// <summary>The beat file for a song file.</summary>
    public static string FileFor(string song) => song + Ending;

    /// <summary>
    /// How many beats into the song a moment is, counting the first beat as 0: a whole number falls
    /// exactly on a beat, and the part after the point is how far through the beat it is.
    /// </summary>
    public double BeatAt(double seconds)
    {
        var beats = Beats;
        if (beats.Count < 2)
        {
            return (seconds - FirstBeat) / BeatLength;
        }

        if (seconds <= beats[0])
        {
            return (seconds - beats[0]) / BeatLength;
        }

        if (seconds >= beats[^1])
        {
            return beats.Count - 1 + ((seconds - beats[^1]) / BeatLength);
        }

        // The last beat at or before the moment, found by halving.
        var low = 0;
        var high = beats.Count - 1;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            if (beats[middle] <= seconds)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return low + ((seconds - beats[low]) / (beats[low + 1] - beats[low]));
    }

    /// <summary>
    /// A steady beat to dance to when a song's own is not known: one that suits the sort of music,
    /// counted from the start of the song.
    /// </summary>
    public static BeatInfo ForGenre(Genre genre) => new(
        genre switch
        {
            Genre.Pop => 118,
            Genre.Rock => 126,
            Genre.Country => 104,
            Genre.Electronic => 126,
            Genre.Metal => 138,
            Genre.HipHop => 92,
            _ => 120,
        },
        0,
        0);

    /// <summary>The text of the beat file.</summary>
    public string Write()
    {
        var text = new StringBuilder();
        text.AppendLine("# The beat of this song, worked out by Blueprint Festival so the crowd can dance to it.");
        text.AppendLine("# Change the numbers if the crowd is out of time, or delete this file to have it worked out again.");
        text.AppendLine(CultureInfo.InvariantCulture, $"bpm = {Bpm:0.000}");
        text.AppendLine(CultureInfo.InvariantCulture, $"first beat = {FirstBeat:0.000}");
        text.AppendLine(CultureInfo.InvariantCulture, $"strength = {Strength:0.00}");
        if (Beats.Count > 0)
        {
            text.AppendLine("# Every beat, in seconds. Delete this line to use the steady beat above instead.");
            text.Append("beats = ");
            text.AppendLine(string.Join(" ", Beats.Select(b => b.ToString("0.000", CultureInfo.InvariantCulture))));
        }

        return text.ToString();
    }

    /// <summary>Reads a beat file. Null if it has no sensible beat in it.</summary>
    public static BeatInfo? Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        double? bpm = null;
        double first = 0;
        double strength = 1;
        var beats = new List<double>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var equals = line.IndexOfAny(new[] { '=', ':' });
            if (equals > 0 && line[..equals].Trim().Equals("beats", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var word in line[(equals + 1)..].Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out var at) && double.IsFinite(at)
                        && (beats.Count == 0 || at > beats[^1]))
                    {
                        beats.Add(at);
                    }
                }

                continue;
            }

            if (equals < 0
                || !double.TryParse(line[(equals + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value))
            {
                continue;
            }

            switch (line[..equals].Trim().ToLowerInvariant())
            {
                case "bpm":
                case "tempo":
                    bpm = value;
                    break;
                case "first beat":
                case "first":
                    first = value;
                    break;
                case "strength":
                    strength = Math.Clamp(value, 0, 1);
                    break;
            }
        }

        return bpm is { } b && b >= Slowest && b <= Fastest ? new BeatInfo(b, Math.Max(0, first), strength) { Beats = beats } : null;
    }
}

/// <summary>
/// Finds the beat of a song from its sound. It listens for the moments the sound gets suddenly
/// louder (drums, plucks, the start of notes), finds the gap those moments most often repeat at,
/// and then lines a steady beat up with them across the whole song, so it stays in time to the end.
/// </summary>
public static class BeatFinder
{
    private const int Window = 1024;
    private const double FramesASecond = 200;

    /// <summary>
    /// The beat of a song, from its sound as one channel of samples between -1 and 1.
    /// Null if the song is too short or has no beat that can be heard.
    /// </summary>
    public static BeatInfo? Find(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (sampleRate < 4000)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate), "The sound needs at least 4,000 samples a second.");
        }

        var onsets = Onsets(samples, sampleRate, out var frameRate);
        if (onsets.Length < frameRate * 8)
        {
            return null;
        }

        var bpm = RoughTempo(onsets, frameRate);
        if (bpm is not { } rough)
        {
            return null;
        }

        // Line the beat up, trying tempos close to the rough one in ever finer steps.
        var best = (Bpm: rough, Phase: 0.0, Score: double.MinValue);
        foreach (var (spread, step) in new[] { (0.04, 0.004), (0.005, 0.0005), (0.0006, 0.00005) })
        {
            var centre = best.Bpm;
            for (var t = centre * (1 - spread); t <= centre * (1 + spread); t += centre * step)
            {
                var (phase, score) = BestPhase(onsets, frameRate * 60 / t);
                if (score > best.Score)
                {
                    best = (t, phase, score);
                }
            }
        }

        // How much stronger the sound is on the beat than off it, from 0 (no beat at all) to 1.
        var period = frameRate * 60 / best.Bpm;
        var offBeat = CombScore(onsets, period, best.Phase + (period / 2));
        var mean = 0.0;
        foreach (var o in onsets)
        {
            mean += o;
        }

        mean /= onsets.Length;
        var onBeat = best.Score;
        var strength = onBeat <= 0 ? 0 : Math.Clamp((onBeat - Math.Max(mean, offBeat)) / onBeat * 1.5, 0, 1);
        if (onBeat <= mean * 1.05)
        {
            return null;
        }

        var first = best.Phase / frameRate;
        var beats = Track(onsets, frameRate, period).Select(f => Math.Round(f / frameRate, 3)).ToList();
        return new BeatInfo(Math.Round(best.Bpm, 3), Math.Round(first, 3), Math.Round(strength, 2)) { Beats = beats };
    }

    /// <summary>
    /// How suddenly the sound gets louder, two hundred times a second: the rise in each band of
    /// pitches since a moment before, added up, with the low bands (the kick drum and bass) counted
    /// most.
    /// </summary>
    internal static float[] Onsets(ReadOnlySpan<float> samples, int sampleRate, out double frameRate)
    {
        var hop = Math.Max(1, (int)Math.Round(sampleRate / FramesASecond));
        frameRate = (double)sampleRate / hop;
        var frames = samples.Length < Window ? 0 : 1 + ((samples.Length - Window) / hop);
        var flux = new float[frames];
        if (frames == 0)
        {
            return flux;
        }

        var hann = new float[Window];
        for (var i = 0; i < Window; i++)
        {
            hann[i] = (float)(0.5 - (0.5 * Math.Cos(2 * Math.PI * i / (Window - 1))));
        }

        // Pitch bands a third of an octave wide, from 40 Hz up to 8 kHz (or as high as the sound goes).
        var top = Math.Min(8000, sampleRate / 2.0);
        var edges = new List<int>();
        for (var f = 40.0; f < top; f *= 1.26)
        {
            var bin = Math.Max(1, (int)Math.Round(f * Window / sampleRate));
            if (edges.Count == 0 || bin > edges[^1])
            {
                edges.Add(bin);
            }
        }

        var bands = edges.Count - 1;
        var weight = new float[bands];
        for (var b = 0; b < bands; b++)
        {
            var hz = edges[b] * (double)sampleRate / Window;
            weight[b] = hz < 200 ? 1.6f : hz < 2000 ? 1f : 0.7f;
        }

        var re = new double[Window];
        var im = new double[Window];
        var last = new float[bands];
        var now = new float[bands];
        for (var n = 0; n < frames; n++)
        {
            var start = n * hop;
            for (var i = 0; i < Window; i++)
            {
                re[i] = samples[start + i] * hann[i];
                im[i] = 0;
            }

            Fft(re, im);
            var sum = 0f;
            for (var b = 0; b < bands; b++)
            {
                var energy = 0.0;
                for (var k = edges[b]; k < edges[b + 1]; k++)
                {
                    energy += (re[k] * re[k]) + (im[k] * im[k]);
                }

                now[b] = (float)Math.Log(1 + (1000 * energy / (edges[b + 1] - edges[b])));
                if (n > 0 && now[b] > last[b])
                {
                    sum += (now[b] - last[b]) * weight[b];
                }
            }

            (last, now) = (now, last);
            flux[n] = sum;
        }

        // Take away the general level over the last half second or so, so only the sudden rises are left.
        var span = (int)(frameRate * 0.25);
        var onsets = new float[frames];
        var running = 0.0;
        for (var n = 0; n < frames; n++)
        {
            running += flux[n];
            if (n > 2 * span)
            {
                running -= flux[n - (2 * span) - 1];
            }

            var count = Math.Min(n + 1, (2 * span) + 1);
            var centre = n - span;
            if (centre >= 0)
            {
                onsets[centre] = (float)Math.Max(0, flux[centre] - (running / count));
            }
        }

        // The window starts at the frame's sample but the sound it hears is centred half a window later.
        var shift = (int)Math.Round(Window / 2.0 / hop);
        var shifted = new float[frames + shift];
        Array.Copy(onsets, 0, shifted, shift, frames);
        return shifted;
    }

    /// <summary>
    /// The gap the rises most often repeat at, as beats a minute, favouring tempos people dance to
    /// (around 120) when a song could be heard as half or twice as fast.
    /// </summary>
    internal static double? RoughTempo(float[] onsets, double frameRate)
    {
        var shortest = (int)Math.Floor(frameRate * 60 / BeatInfo.Fastest);
        var longest = (int)Math.Ceiling(frameRate * 60 / BeatInfo.Slowest);
        if (onsets.Length < longest * 4)
        {
            return null;
        }

        var echo = new double[(longest * 2) + 2];
        for (var lag = shortest; lag < echo.Length; lag++)
        {
            var sum = 0.0;
            for (var n = lag; n < onsets.Length; n++)
            {
                sum += onsets[n] * onsets[n - lag];
            }

            echo[lag] = sum / (onsets.Length - lag);
        }

        var bestLag = -1;
        var best = 0.0;
        for (var lag = shortest; lag <= longest; lag++)
        {
            var bpm = frameRate * 60 / lag;
            var octaves = Math.Log2(bpm / 120);
            var liking = Math.Exp(-0.5 * octaves * octaves / (0.9 * 0.9));
            var score = (echo[lag] + (0.5 * echo[lag * 2])) * liking;
            if (score > best)
            {
                best = score;
                bestLag = lag;
            }
        }

        if (bestLag < 0)
        {
            return null;
        }

        // Find the peak between whole frames.
        var a = echo[bestLag - 1];
        var b = echo[bestLag];
        var c = echo[bestLag + 1];
        var bend = a - (2 * b) + c;
        var exact = bestLag + (bend < 0 ? Math.Clamp(0.5 * (a - c) / bend, -0.5, 0.5) : 0);
        return frameRate * 60 / exact;
    }

    /// <summary>
    /// Follows the beat through the song one beat at a time, so it keeps up when the players drift a
    /// little faster or slower: each beat lands on a rise in the sound if it can, but no more than a
    /// little way from where the last beat says it should be. Returns the frame of every beat.
    /// </summary>
    internal static List<double> Track(float[] onsets, double frameRate, double period)
    {
        var n = onsets.Length;
        var spread = 0.0;
        var mean = 0.0;
        foreach (var o in onsets)
        {
            mean += o;
        }

        mean /= n;
        foreach (var o in onsets)
        {
            spread += (o - mean) * (o - mean);
        }

        spread = Math.Sqrt(spread / n);
        if (spread <= 0)
        {
            return [];
        }

        // How hard it is to stray from a steady beat; the higher, the steadier.
        const double steadiness = 100;
        var score = new double[n];
        var from = new int[n];
        var shortest = (int)Math.Round(period / 2);
        var longest = (int)Math.Round(period * 2);
        for (var t = 0; t < n; t++)
        {
            var here = onsets[t] / spread;
            var best = double.MinValue;
            var bestFrom = -1;
            for (var gap = shortest; gap <= longest && gap <= t; gap++)
            {
                var stray = Math.Log(gap / period);
                var s = score[t - gap] - (steadiness * stray * stray);
                if (s > best)
                {
                    best = s;
                    bestFrom = t - gap;
                }
            }

            score[t] = here + (bestFrom >= 0 ? best : 0);
            from[t] = bestFrom;
        }

        // End on the best-scoring beat in the last beat of the song, and walk back to the start.
        var end = n - 1;
        for (var t = Math.Max(0, n - (int)Math.Ceiling(period)); t < n; t++)
        {
            if (score[t] > score[end])
            {
                end = t;
            }
        }

        var beats = new List<double>();
        for (var t = end; t >= 0; t = from[t])
        {
            beats.Add(PeakAt(onsets, t));
        }

        beats.Reverse();
        return beats;
    }

    /// <summary>A beat's frame, nudged between whole frames towards the stronger side.</summary>
    private static double PeakAt(float[] onsets, int t)
    {
        if (t <= 0 || t >= onsets.Length - 1)
        {
            return t;
        }

        double a = onsets[t - 1], b = onsets[t], c = onsets[t + 1];
        var bend = a - (2 * b) + c;
        return bend < 0 ? t + Math.Clamp(0.5 * (a - c) / bend, -0.5, 0.5) : t;
    }

    /// <summary>Where a steady beat a given number of frames apart lines up best with the rises, and how well.</summary>
    private static (double Phase, double Score) BestPhase(float[] onsets, double period)
    {
        var best = (Phase: 0.0, Score: double.MinValue);
        for (var phase = 0.0; phase < period; phase += 0.5)
        {
            var score = CombScore(onsets, period, phase);
            if (score > best.Score)
            {
                best = (phase, score);
            }
        }

        return best;
    }

    /// <summary>The average rise on a steady beat that starts at a frame and repeats every so many frames.</summary>
    private static double CombScore(float[] onsets, double period, double phase)
    {
        var sum = 0.0;
        var count = 0;
        for (var at = phase % period; at < onsets.Length - 1; at += period)
        {
            var i = (int)at;
            var part = at - i;

            // A little leeway either side of the beat, as players are never exactly on it.
            var here = (onsets[i] * (1 - part)) + (onsets[i + 1] * part);
            var near = Math.Max(i > 0 ? onsets[i - 1] : 0, i + 2 < onsets.Length ? onsets[i + 2] : 0);
            sum += here + (0.5 * near);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    /// <summary>A fast Fourier transform, in place. The length must be a power of two.</summary>
    private static void Fft(double[] re, double[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var len = 2; len <= n; len <<= 1)
        {
            var angle = -2 * Math.PI / len;
            var wr = Math.Cos(angle);
            var wi = Math.Sin(angle);
            for (var i = 0; i < n; i += len)
            {
                var cr = 1.0;
                var ci = 0.0;
                for (var k = 0; k < len / 2; k++)
                {
                    var a = i + k;
                    var b = a + (len / 2);
                    var tr = (re[b] * cr) - (im[b] * ci);
                    var ti = (re[b] * ci) + (im[b] * cr);
                    re[b] = re[a] - tr;
                    im[b] = im[a] - ti;
                    re[a] += tr;
                    im[a] += ti;
                    var next = (cr * wr) - (ci * wi);
                    ci = (cr * wi) + (ci * wr);
                    cr = next;
                }
            }
        }
    }
}
