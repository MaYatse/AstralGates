using System.Numerics;
using System.Runtime.CompilerServices;

namespace AstralGates.Benchmarks;

// Experimental candidates, not a change to the production renderer.
internal static class GammaLookup
{
    internal const int Size = 4096;
    internal const float Exponent = 1f / 2.2f;
    private const float FirstInterval = 1f / (Size - 1);
    private static readonly float[] Table = CreateTable();

    private static float[] CreateTable()
    {
        var table = new float[Size];
        for (int i = 0; i < table.Length; i++)
            table[i] = MathF.Pow(i / (float)(Size - 1), Exponent);
        return table;
    }

    public static void Warmup() => _ = Table[1];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Pow(float value) => MathF.Pow(value, Exponent);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Linear(float value)
    {
        // Preserve HDR values and Pow's behavior for negative/non-finite inputs.
        if (!(value >= 0f && value <= 1f))
            return Pow(value);

        float position = value * (Size - 1);
        int index = Math.Min((int)position, Size - 2);
        float fraction = position - index;
        return Table[index] + fraction * (Table[index + 1] - Table[index]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DarkSafe(float value)
    {
        // Keep the steepest part of the power curve exact, in the first table cell.
        if (value > 0f && value < FirstInterval)
            return Pow(value);
        return Linear(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Pow(Vector3 color) => new(Pow(color.X), Pow(color.Y), Pow(color.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Linear(Vector3 color) => new(Linear(color.X), Linear(color.Y), Linear(color.Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 DarkSafe(Vector3 color) => new(DarkSafe(color.X), DarkSafe(color.Y), DarkSafe(color.Z));
}
