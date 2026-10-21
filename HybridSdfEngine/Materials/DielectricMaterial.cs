using System.Numerics;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Materials;

/// <summary>Solid dielectric with Fresnel reflection, tinted transmission, IOR and optional roughness.</summary>
public sealed class DielectricMaterial : IMaterial
{
    private readonly DiffuseMaterial _surface;
    private ITexture<float> _roughness = new ConstantTexture<float>(0f);
    private ITexture<float> _ior = new ConstantTexture<float>(1.5f);

    public bool MayTransmitShadows => true;
    public ITexture<Vector3> TransmissionTint { get => _surface.Albedo; set => _surface.Albedo = value; }
    public ITexture<Vector3> Emission { get => _surface.Emission; set => _surface.Emission = value; }
    public ITexture<Vector3>? NormalMap { get => _surface.NormalMap; set => _surface.NormalMap = value; }
    public ITexture<float> Roughness { get => _roughness; set { ArgumentNullException.ThrowIfNull(value); _roughness = value; } }
    public ITexture<float> IndexOfRefraction { get => _ior; set { ArgumentNullException.ThrowIfNull(value); _ior = value; } }

    public DielectricMaterial() : this(Vector3.One) { }
    public DielectricMaterial(Vector3 tint) : this(new ConstantTexture<Vector3>(tint)) { }
    public DielectricMaterial(ITexture<Vector3> tint) => _surface = new(tint);

    public MaterialSample Sample(in TextureContext context)
    {
        float ior = IndexOfRefraction.Sample(context);
        MaterialValidation.Ior(ior);
        return _surface.Sample(context) with
        {
            Bsdf = DielectricBsdf.Instance,
            Roughness = MaterialValidation.Unit(Roughness.Sample(context)),
            IndexOfRefraction = ior
        };
    }
}
