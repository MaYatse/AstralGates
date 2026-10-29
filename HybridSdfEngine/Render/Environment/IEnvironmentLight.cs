using System.Numerics;

namespace HybridSdfEngine.Render.Environment;

/// <summary>Linear distant illumination. Diffuse returns irradiance divided by PI for a unit Lambertian surface.</summary>
public interface IEnvironmentLight
{
    Vector3 SampleSpecular(Vector3 direction, float roughness);
    Vector3 SampleDiffuse(Vector3 normal);
}
