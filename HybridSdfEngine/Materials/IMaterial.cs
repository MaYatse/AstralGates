using System.Numerics;
using HybridSdfEngine.Textures;
using HybridSdfEngine.Materials.Scattering;

namespace HybridSdfEngine.Materials;

/// <summary>Surface parameters evaluated once per hit, before sampling lights.</summary>
public readonly record struct MaterialSample(Vector3 Albedo, Vector3 Emission, Vector3 ShadingNormal)
{
    public IBsdf Bsdf { get; init; } = LambertianBsdf.Instance;
    public float Roughness { get; init; } = 0.5f;
    public float Metallic { get; init; }
    public float IndexOfRefraction { get; init; } = 1.5f;
}

public interface IMaterial
{
    /// <summary>Allows the renderer to select transmissive shadow traversal without sampling every material.</summary>
    bool MayTransmitShadows => false;
    MaterialSample Sample(in TextureContext context);
}