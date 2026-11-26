using CsCheck;

namespace ThoughtTree.Domain.Properties.Support;

/// <summary>Runs a property over the same inputs in every run, and shrinks a failure (ADR-035).</summary>
internal static class Deterministic
{
    public const string MasterSeed = "0N0XIzNsQ0O2";

    public const int Iterations = 1000;

    private const int ShrinkIterations = 20_000;

    public static void SampleFixed<T>(this Gen<T> gen, Action<T> assert, int iterations = Iterations)
    {
        // CsCheck's seed: fixes only the first iteration, so each iteration gets a seed of its own.
        var master = PCG.Parse(MasterSeed);
        for (var i = 0; i < iterations; i++)
        {
            var seed = new PCG(master.Stream, master.Next64()).ToString();
            try
            {
                gen.Sample(assert, seed: seed, iter: 1, threads: 1);
            }
            catch (CsCheckException)
            {
                // The first iteration replays the failure; the rest are random and only make the counterexample smaller.
                gen.Sample(assert, seed: seed, iter: ShrinkIterations, threads: 1);
                throw;
            }
        }
    }
}
