namespace BlueprintFestival.Simulation.Core;

/// <summary>
/// A small random number generator whose whole state is one number, so it can be written into a
/// save file and carry on exactly where it left off.
/// </summary>
public sealed class GameRandom
{
    public GameRandom(ulong seed)
    {
        State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
    }

    public static GameRandom FromSeed(int seed, int stream = 0) =>
        new(unchecked((((ulong)(uint)seed) << 32) | (uint)(stream * 2654435761L)) ^ 0xD1B54A32D192ED03UL);

    public ulong State { get; private set; }

    private ulong NextBits()
    {
        // xorshift64*
        var x = State;
        x ^= x >> 12;
        x ^= x << 25;
        x ^= x >> 27;
        State = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>A number from 0 up to, but not including, 1.</summary>
    public double NextDouble() => (NextBits() >> 11) * (1.0 / (1UL << 53));

    /// <summary>A whole number from 0 up to, but not including, the limit.</summary>
    public int Next(int limit) => limit <= 0 ? throw new ArgumentOutOfRangeException(nameof(limit)) : (int)(NextDouble() * limit);

    /// <summary>A number between two values.</summary>
    public double Between(double low, double high) => low + (NextDouble() * (high - low));
}
