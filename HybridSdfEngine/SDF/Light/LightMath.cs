using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

internal static class LightMath
{
    public static void RequireFinite(Vector3 value, string parameterName)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new ArgumentException("The vector must be finite.", parameterName);
    }

    public static Vector3 Normalize(Vector3 value, string parameterName)
    {
        RequireFinite(value, parameterName);
        float scale = MathF.Max(MathF.Abs(value.X), MathF.Max(MathF.Abs(value.Y), MathF.Abs(value.Z)));
        if (scale <= 0f)
            throw new ArgumentException("The direction must be nonzero.", parameterName);
        return Vector3.Normalize(value / scale);
    }
}
