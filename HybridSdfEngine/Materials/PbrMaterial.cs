using System.Numerics;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Materials;

/// <summary>Opaque metallic-roughness material. All color, roughness and metallic parameters support maps.</summary>
public sealed class PbrMaterial : IMaterial
{
    private readonly DiffuseMaterial _surface;
    private ITexture<float> _roughness = new ConstantTexture<float>(0.5f);
    private ITexture<float> _metallic = new ConstantTexture<float>(0f);
    private float _ior = 1.5f;

    public ITexture<Vector3> Albedo { get => _surface.Albedo; set => _surface.Albedo = value; }
    public ITexture<Vector3> Emission { get => _surface.Emission; set => _surface.Emission = value; }
    public ITexture<Vector3>? NormalMap { get => _surface.NormalMap; set => _surface.NormalMap = value; }
    public ITexture<float> Roughness { get => _roughness; set { ArgumentNullException.ThrowIfNull(value); _roughness = value; } }
    public ITexture<float> Metallic { get => _metallic; set { ArgumentNullException.ThrowIfNull(value); _metallic = value; } }
    public float IndexOfRefraction { get => _ior; set { MaterialValidation.Ior(value); _ior = value; } }

    public PbrMaterial() : this(Vector3.One) { }
    public PbrMaterial(Vector3 albedo) : this(new ConstantTexture<Vector3>(albedo)) { }
    public PbrMaterial(ITexture<Vector3> albedo) => _surface = new(albedo);

    public MaterialSample Sample(in TextureContext context) => _surface.Sample(context) with
    {
        Bsdf = MetallicRoughnessBsdf.Instance,
        Roughness = MaterialValidation.Unit(Roughness.Sample(context)),
        Metallic = MaterialValidation.Unit(Metallic.Sample(context)),
        IndexOfRefraction = IndexOfRefraction
    };
}
