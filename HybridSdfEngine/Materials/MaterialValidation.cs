using System.Numerics;
using HybridSdfEngine.Materials.Scattering;

namespace HybridSdfEngine.Materials;

internal static class MaterialValidation
{
    public static void Color(Vector3 value, string name)
    {
        if (!BsdfMath.IsFinite(value)) throw new ArgumentOutOfRangeException(name, "Material colors must be finite.");
    }

    public static float Unit(float value)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value), "Material parameters must be finite.");
        return Math.Clamp(value, 0f, 1f);
    }

    public static void Ior(float value)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(nameof(value), "The index of refraction must be finite and positive.");
    }
}
