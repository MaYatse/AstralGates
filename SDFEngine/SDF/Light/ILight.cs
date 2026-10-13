using System.Numerics;

namespace SDFEngine.SDF.Light;

public readonly record struct LightSample(
    Vector3 DirectionToLight,
    Vector3 Radiance,
    float Distance);

public interface ILight
{
    LightSample Sample(Vector3 point);
}
