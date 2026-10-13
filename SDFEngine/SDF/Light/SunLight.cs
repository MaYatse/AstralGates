using System.Numerics;

namespace SDFEngine.SDF.Light;

public class SunLight : ILight
{
    private readonly LightSample _sample;

    public SunLight(
        Vector3 rayDirection,
        Vector3 color,
        float intensity)
    {
        float length = rayDirection.Length();

        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException(
                "The direction must be finite and non‑zero.",
                nameof(rayDirection));

        _sample = new LightSample(
            -rayDirection / length,
            color * intensity,
            float.PositiveInfinity);
    }

    public LightSample Sample(Vector3 point) => _sample;
}
