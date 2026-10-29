using System.Numerics;
using HybridSdfEngine.Materials.Scattering;

namespace HybridSdfEngine.Render.Environment;

/// <summary>Small, deterministic split-sum GGX BRDF integration table shared by all realtime renderers.</summary>
internal static class EnvironmentBrdf
{
    private const int Size = 32, Samples = 128;
    private static readonly Vector3[] Table = Build();

    public static void Prepare() => _ = Table.Length;

    public static Vector3 Evaluate(Vector3 f0, float normalView, float roughness)
    {
        normalView = Math.Clamp(normalView, 0, 1);
        if (roughness <= 0) return BsdfMath.Schlick(f0, normalView);
        Vector3 coefficients = EnvironmentSampling.Bilinear(Table, Size, 0, normalView, roughness);
        return Vector3.Clamp(f0 * coefficients.X + new Vector3(coefficients.Y), Vector3.Zero, Vector3.One);
    }

    private static Vector3[] Build()
    {
        var table = new Vector3[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float nv = (x + 0.5f) / Size, roughness = (y + 0.5f) / Size;
            Vector3 view = new(MathF.Sqrt(1 - nv * nv), 0, nv);
            float a = 0, b = 0, alpha = BsdfMath.Alpha(roughness);
            for (int i = 0; i < Samples; i++)
            {
                Vector3 half = EnvironmentSampling.GgxHalf(EnvironmentSampling.Hammersley(i, Samples), roughness);
                Vector3 light = Vector3.Reflect(-view, half);
                if (light.Z <= 0) continue;
                float vh = MathF.Max(0, Vector3.Dot(view, half));
                float visibility = BsdfMath.Masking(nv, alpha) * BsdfMath.Masking(light.Z, alpha) * vh / (half.Z * nv);
                float fresnel = 1 - vh;
                float f2 = fresnel * fresnel;
                fresnel *= f2 * f2;
                a += (1 - fresnel) * visibility;
                b += fresnel * visibility;
            }
            table[y * Size + x] = new(a / Samples, b / Samples, 0);
        }
        return table;
    }
}
