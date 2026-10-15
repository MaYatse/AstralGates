using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

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

    LightSample Sample(Vector3 point);
}
