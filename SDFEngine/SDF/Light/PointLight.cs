using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

public class PointLight(
    Vector3 position,
    Vector3 color,
    float intensity) : ILight
{
    public Vector3 Position { get; set; } = position;
    public Vector3 Color { get; set; } = color;
    public float Intensity { get; set; } = intensity;

    public LightSample Sample(Vector3 point)
    {
        Vector3 offset = Position - point;
        float distanceSquared = offset.LengthSquared();

        if (distanceSquared <= 1e-12f)
            return new(Vector3.Zero, Vector3.Zero, 0f);

        float distance = MathF.Sqrt(distanceSquared);

        return new(
            offset / distance,
            Color * (Intensity / distanceSquared),
            distance);
    }
}
