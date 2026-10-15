using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

public class SunLight : ILight
{
    private LightSample _sample;
    private Vector3 _color;
    private float _intensity;

    public Vector3 Color
    {
        get => _color;
        set
        {
            _color = value;
            _sample = _sample with { Radiance = _color * _intensity };
        }
    }

    public float Intensity
    {
        get => _intensity;
        set
        {
            _intensity = value;
            _sample = _sample with { Radiance = _color * _intensity };
        }
    }

    /// <summary>The normalized direction in which the light rays travel.</summary>
    public Vector3 RayDirection
    {
        get => -_sample.DirectionToLight;
        set
        {
            float length = value.Length();

            if (!float.IsFinite(length) || length <= 0f)
                throw new ArgumentException(
                    "The direction must be finite and non-zero.",
                    nameof(value));

            _sample = _sample with { DirectionToLight = -value / length };
        }
    }

    public SunLight(
        Vector3 rayDirection,
        Vector3 color,
        float intensity)
    {
        _color = color;
        _intensity = intensity;
        _sample = new LightSample(
            Vector3.Zero,
            color * intensity,
            float.PositiveInfinity);
        RayDirection = rayDirection;
    }

    public LightSample Sample(Vector3 point) => _sample;
}
