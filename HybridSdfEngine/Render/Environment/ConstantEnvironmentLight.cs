using System.Numerics;
using HybridSdfEngine.Materials.Scattering;

namespace HybridSdfEngine.Render.Environment;

public sealed class ConstantEnvironmentLight : IEnvironmentLight
{
    public Vector3 Radiance { get; }

    public ConstantEnvironmentLight(Vector3 radiance)
    {
        if (!BsdfMath.IsFinite(radiance) || radiance.X < 0 || radiance.Y < 0 || radiance.Z < 0)
            throw new ArgumentOutOfRangeException(nameof(radiance));
        Radiance = radiance;
    }

    public Vector3 SampleSpecular(Vector3 direction, float roughness) => Radiance;
    public Vector3 SampleDiffuse(Vector3 normal) => Radiance;
}
