using System.Numerics;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Materials;

/// <summary>Surface parameters evaluated once per hit, before sampling lights.</summary>
public readonly record struct MaterialSample(Vector3 Albedo, Vector3 Emission, Vector3 ShadingNormal);

public interface IMaterial
{
    MaterialSample Sample(in TextureContext context);
}
