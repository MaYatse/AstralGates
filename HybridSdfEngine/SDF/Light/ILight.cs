using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

/// <summary>
/// One weighted lighting estimate: unit direction toward the emitter and its distance
/// (positive infinity for directional lights). A dark sample may have zero direction/distance.
/// </summary>
public readonly record struct LightSample(
    Vector3 DirectionToLight,
    Vector3 Radiance,
    float Distance);

public interface ILight
{
    /// <summary>The light color in linear RGB, before applying intensity.</summary>
    Vector3 Color { get; set; }

    /// <summary>The multiplier applied to the light color.</summary>
    float Intensity { get; set; }

    /// <summary>Positive number of samples whose lighting contributions are averaged.</summary>
    int SampleCount => 1;

    /// <summary>Returns the first sample. For an area light, use all SampleCount samples.</summary>
    LightSample Sample(Vector3 point);

    /// <summary>Returns one lighting estimate, before averaging across samples.</summary>
    LightSample Sample(Vector3 point, int sampleIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sampleIndex, SampleCount);
        return Sample(point);
    }
}
