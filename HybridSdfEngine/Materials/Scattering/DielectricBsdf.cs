using System.Numerics;

namespace HybridSdfEngine.Materials.Scattering;

/// <summary>GGX dielectric reflection/transmission, with ideal specular transport at zero roughness.</summary>
public sealed class DielectricBsdf : IBsdf
{
    public static DielectricBsdf Instance { get; } = new();
    public BsdfLobes Flags => BsdfLobes.Reflection | BsdfLobes.Transmission | BsdfLobes.Glossy | BsdfLobes.Specular;

    private static bool IsDelta(in MaterialSample material, in BsdfContext context) =>
        material.Roughness <= 0f || context.IncidentIor - context.TransmittedIor == 0f;

    private static bool HalfVector(in BsdfContext context, Vector3 wi, Vector3 n, bool reflection, out Vector3 h)
    {
        float eta = context.TransmittedIor / context.IncidentIor;
        Vector3 sum = context.OutgoingDirection + wi * (reflection ? 1f : eta);
        h = Vector3.Zero;
        if (sum.LengthSquared() <= 1e-20f) return false;
        h = Vector3.Normalize(sum);
        if (Vector3.Dot(n, h) < 0f) h = -h;
        float oh = Vector3.Dot(context.OutgoingDirection, h), ih = Vector3.Dot(wi, h);
        return oh > 0f && (reflection ? ih > 0f : ih < 0f);
    }

    public Vector3 Evaluate(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection)
    {
        if (IsDelta(material, context)) return Vector3.Zero;
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        float no = Vector3.Dot(n, wo), ni = Vector3.Dot(n, incomingDirection);
        bool reflection = BsdfMath.SameSide(context, incomingDirection);
        if (no <= 0f || ni == 0f || reflection != (ni > 0f)
            || !HalfVector(context, incomingDirection, n, reflection, out Vector3 h)) return Vector3.Zero;
        float oh = Vector3.Dot(wo, h), ih = Vector3.Dot(incomingDirection, h);
        float fresnel = BsdfMath.Fresnel(oh, context.IncidentIor, context.TransmittedIor);
        float alpha = BsdfMath.Alpha(material.Roughness);
        float dg = BsdfMath.Distribution(Vector3.Dot(n, h), alpha)
            * BsdfMath.Masking(no, alpha) * BsdfMath.Masking(ni, alpha);
        if (reflection) return new Vector3(fresnel * dg / (4f * no * ni));
        float eta = context.TransmittedIor / context.IncidentIor;
        float denominator = oh + eta * ih;
        if (denominator == 0f) return Vector3.Zero;
        // Radiance transport includes (eta_i/eta_t)^2; it cancels the Jacobian's eta^2 here.
        return material.Albedo * ((1f - fresnel) * dg * MathF.Abs(oh * ih)
            / (no * MathF.Abs(ni) * denominator * denominator));
    }

    public float Pdf(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection)
    {
        if (IsDelta(material, context)) return 0f;
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        bool reflection = BsdfMath.SameSide(context, incomingDirection);
        float ni = Vector3.Dot(n, incomingDirection);
        if (Vector3.Dot(n, wo) <= 0f || ni == 0f || reflection != (ni > 0f)
            || !HalfVector(context, incomingDirection, n, reflection, out Vector3 h)) return 0f;
        float oh = Vector3.Dot(wo, h), ih = Vector3.Dot(incomingDirection, h);
        float nh = MathF.Max(0f, Vector3.Dot(n, h));
        float density = BsdfMath.Distribution(nh, BsdfMath.Alpha(material.Roughness)) * nh;
        float fresnel = BsdfMath.Fresnel(oh, context.IncidentIor, context.TransmittedIor);
        if (reflection) return fresnel * density / (4f * oh);
        float eta = context.TransmittedIor / context.IncidentIor;
        float denominator = oh + eta * ih;
        return denominator == 0f ? 0f : (1f - fresnel) * density * eta * eta * MathF.Abs(ih) / (denominator * denominator);
    }

    public BsdfSample Sample(in MaterialSample material, in BsdfContext context, Vector3 random)
    {
        Vector3 n = BsdfMath.FaceNormal(material, context), wo = context.OutgoingDirection;
        if (Vector3.Dot(n, wo) <= 0f) return default;
        bool delta = IsDelta(material, context);
        Vector3 h = delta ? n : BsdfMath.SampleHalf(new(random.Y, random.Z), n, BsdfMath.Alpha(material.Roughness));
        float oh = Vector3.Dot(wo, h);
        if (oh <= 0f) return default;
        float fresnel = BsdfMath.Fresnel(oh, context.IncidentIor, context.TransmittedIor);
        float eta = context.IncidentIor / context.TransmittedIor;
        bool reflection = random.X < fresnel;
        Vector3 wi;
        if (reflection) wi = Vector3.Reflect(-wo, h);
        else if (!BsdfMath.Refract(wo, h, eta, out wi)) return default;
        if (BsdfMath.SameSide(context, wi) != reflection) return default;
        BsdfLobes flags = (reflection ? BsdfLobes.Reflection : BsdfLobes.Transmission)
            | (delta ? BsdfLobes.Specular : BsdfLobes.Glossy);
        if (!delta) return new(wi, Evaluate(material, context, wi), Pdf(material, context, wi), flags);
        float probability = reflection ? fresnel : 1f - fresnel;
        Vector3 weight = reflection ? new(probability) : material.Albedo * (probability * eta * eta);
        return new(wi, weight / MathF.Abs(Vector3.Dot(n, wi)), probability, flags);
    }

    public Vector3 ShadowTransmittance(in MaterialSample material, in BsdfContext context)
    {
        float fresnel = BsdfMath.Fresnel(Vector3.Dot(context.GeometricNormal, context.OutgoingDirection),
            context.IncidentIor, context.TransmittedIor);
        return material.Albedo * (1f - fresnel);
    }
}
