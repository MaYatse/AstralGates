using System.Numerics;

namespace SDFEngine.SDF.Light;

public class PointLight(
    Vector3 position,
    Vector3 color,
    float intensity) : ILight
{
    public LightSample Sample(Vector3 point)
    {
        Vector3 offset = position - point;
        float distanceSquared = offset.LengthSquared();

        if (distanceSquared <= 1e-12f)
            return new(Vector3.Zero, Vector3.Zero, 0f);

        float distance = MathF.Sqrt(distanceSquared);

        return new(
            offset / distance,
            color * (intensity / distanceSquared),
            distance);
    }
}
