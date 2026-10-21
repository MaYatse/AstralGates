using System.Numerics;

namespace HybridSdfEngine.Materials.Scattering;

/// <summary>Opaque metallic-roughness BSDF: GGX/Smith/Schlick reflection and an energy-reduced diffuse lobe.</summary>
public sealed class MetallicRoughnessBsdf : IBsdf
{
    public static MetallicRoughnessBsdf Instance { get; } = new();
    public BsdfLobes Flags => BsdfLobes.Reflection | BsdfLobes.Glossy | BsdfLobes.Diffuse;

    private static Vector3 F0(in MaterialSample material, in BsdfContext context)
    {
        float ratio = (material.IndexOfRefraction - context.IncidentIor) / (material.IndexOfRefraction + context.IncidentIor);
        return Vector3.Lerp(new(ratio * ratio), material.Albedo, material.Metallic);
    }

    public Vector3 Evaluate(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection)
    {
        if (!BsdfMath.SameSide(context, incomingDirection) || !context.FrontFace) return Vector3.Zero;
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        float no = Vector3.Dot(n, wo), ni = Vector3.Dot(n, incomingDirection);
        if (no <= 0f || ni <= 0f) return Vector3.Zero;
        Vector3 h = Vector3.Normalize(wo + incomingDirection);
        Vector3 fresnel = BsdfMath.Schlick(F0(material, context), Vector3.Dot(wo, h));
        Vector3 diffuse = material.Albedo * (1f - material.Metallic) * (Vector3.One - fresnel) / MathF.PI;
        if (material.Roughness <= 0f) return diffuse;
        float alpha = BsdfMath.Alpha(material.Roughness);
        float specular = BsdfMath.Distribution(Vector3.Dot(n, h), alpha)
            * BsdfMath.Masking(no, alpha) * BsdfMath.Masking(ni, alpha) / (4f * no * ni);
        return diffuse + fresnel * specular;
    }

    public float Pdf(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection)
    {
        if (!BsdfMath.SameSide(context, incomingDirection) || !context.FrontFace) return 0f;
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        float ni = Vector3.Dot(n, incomingDirection);
        if (ni <= 0f || Vector3.Dot(n, wo) <= 0f) return 0f;
        float probability = 0.5f + 0.5f * material.Metallic;
        float pdf = (1f - probability) * ni / MathF.PI;
        if (material.Roughness <= 0f) return pdf;
        Vector3 h = Vector3.Normalize(wo + incomingDirection);
        float wh = Vector3.Dot(wo, h), nh = MathF.Max(0f, Vector3.Dot(n, h));
        if (wh > 0f) pdf += probability * BsdfMath.Distribution(nh, BsdfMath.Alpha(material.Roughness)) * nh / (4f * wh);
        return pdf;
    }

    public BsdfSample Sample(in MaterialSample material, in BsdfContext context, Vector3 random)
    {
        if (!context.FrontFace) return default;
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        if (Vector3.Dot(n, wo) <= 0f) return default;
        float probability = 0.5f + 0.5f * material.Metallic;
        bool specular = random.X < probability;
        if (specular && material.Roughness <= 0f)
        {
            Vector3 reflected = Vector3.Reflect(-wo, n);
            if (!BsdfMath.SameSide(context, reflected)) return default;
            return new(reflected, BsdfMath.Schlick(F0(material, context), Vector3.Dot(wo, n))
                / MathF.Abs(Vector3.Dot(n, reflected)), probability, BsdfLobes.Reflection | BsdfLobes.Specular);
        }
        Vector3 wi;
        if (specular)
        {
            Vector3 h = BsdfMath.SampleHalf(new(random.Y, random.Z), n, BsdfMath.Alpha(material.Roughness));
            if (Vector3.Dot(wo, h) <= 0f) return default;
            wi = Vector3.Reflect(-wo, h);
        }
        else wi = BsdfMath.CosineHemisphere(new(random.Y, random.Z), n);
        return new(wi, Evaluate(material, context, wi), Pdf(material, context, wi),
            BsdfLobes.Reflection | (specular ? BsdfLobes.Glossy : BsdfLobes.Diffuse));
    }
}
