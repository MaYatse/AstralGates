using System.Globalization;
using System.Numerics;

namespace AstralGates.Benchmarks;

internal static class GammaAccuracy
{
    public static void Run()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        GammaLookup.Warmup();
        Console.WriteLine("# Gamma LUT accuracy\n");
        Console.WriteLine($"{GammaLookup.Size} float entries; exponent = {GammaLookup.Exponent:R}. Reference: MathF.Pow.");
        Console.WriteLine("Byte error uses truncation of clamp(value * 255, 0, 255).\n");
        Console.WriteLine("| Input | Method | Max absolute error | Input at max | RMS error | Max byte difference | Non-finite inputs |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|");
        foreach (var (name, values) in new (string, IEnumerable<float>)[]
        {
            ("Uniform [0,1]", Uniform()),
            ("Dark [1e-10,0.001]", Dark()),
            ("Scene 320x180", Channels(GammaBenchmarks.CreateInput("Scene"))),
            ("Gradient 320x180", Channels(GammaBenchmarks.CreateInput("Gradient")))
        })
        {
            Report(name, "LutLinear", values, GammaLookup.Linear);
            Report(name, "LutDarkSafe", values, GammaLookup.DarkSafe);
        }

        foreach (float value in new[] { -1f, 0f, 1f, 2f, 10f, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
        {
            float expected = GammaLookup.Pow(value);
            if (!expected.Equals(GammaLookup.Linear(value)) || !expected.Equals(GammaLookup.DarkSafe(value)))
                throw new InvalidOperationException($"Fallback mismatch for {value}.");
        }
        Console.WriteLine("\nBoundary/HDR/non-finite fallback checks passed.");
    }

    private static IEnumerable<float> Uniform()
    {
        for (int i = 0; i <= 1_000_000; i++) yield return i / 1_000_000f;
    }

    private static IEnumerable<float> Dark()
    {
        for (int i = 0; i <= 200_000; i++) yield return MathF.Pow(10f, -10f + 7f * i / 200_000f);
    }

    private static IEnumerable<float> Channels(Vector3[] colors)
    {
        foreach (var color in colors) { yield return color.X; yield return color.Y; yield return color.Z; }
    }

    private static void Report(string input, string method, IEnumerable<float> values, Func<float, float> candidate)
    {
        double squaredError = 0;
        float maxError = 0, worstInput = 0;
        int count = 0, maxByteError = 0, nonFinite = 0;
        foreach (float value in values)
        {
            float reference = GammaLookup.Pow(value), actual = candidate(value);
            if (!float.IsFinite(reference) || !float.IsFinite(actual))
            {
                nonFinite++;
                if (!reference.Equals(actual)) throw new InvalidOperationException("Non-finite mismatch.");
                continue;
            }
            float error = MathF.Abs(reference - actual);
            if (error > maxError) { maxError = error; worstInput = value; }
            squaredError += (double)error * error;
            maxByteError = Math.Max(maxByteError, Math.Abs(ToByte(reference) - ToByte(actual)));
            count++;
        }
        Console.WriteLine($"| {input} | {method} | {maxError:G6} | {worstInput:G6} | {Math.Sqrt(squaredError / count):G6} | {maxByteError} | {nonFinite} |");
    }

    private static int ToByte(float value) => (int)Math.Clamp(value * 255f, 0f, 255f);
}
