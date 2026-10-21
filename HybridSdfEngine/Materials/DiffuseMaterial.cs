using System.Numerics;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Materials;

/// <summary>Diffuse albedo, emission and optional tangent-space normal mapping.</summary>
public sealed class DiffuseMaterial : IMaterial
{
    private ITexture<Vector3> _albedo;
    private ITexture<Vector3> _emission = new ConstantTexture<Vector3>(Vector3.Zero);

    public ITexture<Vector3> Albedo
    {
        get => _albedo;
        set { ArgumentNullException.ThrowIfNull(value); _albedo = value; }
    }

    public ITexture<Vector3> Emission
    {
        get => _emission;
        set { ArgumentNullException.ThrowIfNull(value); _emission = value; }
    }

    /// <summary>RGB in [0,1], decoded as a tangent-space normal. Use a linear/data texture.</summary>
    public ITexture<Vector3>? NormalMap { get; set; }

    public DiffuseMaterial() : this(Vector3.One) { }
    public DiffuseMaterial(Vector3 albedo) : this(new ConstantTexture<Vector3>(albedo)) { }
    public DiffuseMaterial(ITexture<Vector3> albedo)
    {
        ArgumentNullException.ThrowIfNull(albedo);
        _albedo = albedo;
    }

    public MaterialSample Sample(in TextureContext context)
    {
        Vector3 albedo = Albedo.Sample(context);
        Vector3 emission = Emission.Sample(context);
        MaterialValidation.Color(albedo, nameof(Albedo));
        MaterialValidation.Color(emission, nameof(Emission));
        return new(Vector3.Clamp(albedo, Vector3.Zero, Vector3.One),
            Vector3.Max(emission, Vector3.Zero), GetShadingNormal(context));
    }

    private Vector3 GetShadingNormal(in TextureContext context)
    {
        if (NormalMap is null) return context.GeometricNormal;
        Vector3 tangentNormal = NormalMap.Sample(context) * 2f - Vector3.One;
        Vector3 normal = context.Tangent * tangentNormal.X + context.Bitangent * tangentNormal.Y
            + context.GeometricNormal * tangentNormal.Z;
        float lengthSquared = normal.LengthSquared();
        if (!float.IsFinite(lengthSquared) || lengthSquared <= 1e-12f ||
            Vector3.Dot(normal, context.GeometricNormal) <= 0f)
            return context.GeometricNormal;
        return normal / MathF.Sqrt(lengthSquared);
    }
}
