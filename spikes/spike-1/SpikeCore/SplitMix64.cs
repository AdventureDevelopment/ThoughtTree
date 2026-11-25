namespace SpikeCore;

/// <summary>Seeded generator for test data only (ADR-034: no system randomness in the spike).</summary>
public sealed class SplitMix64
{
    private ulong _state;

    public SplitMix64(ulong seed) => _state = seed;

    public static ulong SeedFromLabel(string label)
    {
        // FNV-1a 64 over UTF-8: a stable seed from a readable label.
        ulong h = 14695981039346656037UL;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(label)) { h ^= b; h *= 1099511628211UL; }
        return h;
    }

    public ulong Next()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform integer in [0, n).</summary>
    public int Below(int n) => (int)(Next() % (ulong)n);

    public double NextDouble() => (Next() >> 11) * (1.0 / (1UL << 53));

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> list) => list[Below(list.Count)];
}
