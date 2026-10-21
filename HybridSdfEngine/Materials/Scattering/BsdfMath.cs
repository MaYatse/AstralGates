using System.Numerics;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Materials.Scattering;

internal static class BsdfMath
{
    public static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    public static Vector3 FaceNormal(in MaterialSample material, in BsdfContext context)
    {
        Vector3 n = context.FrontFace ? material.ShadingNormal : -material.ShadingNormal;
        if (Vector3.Dot(n, context.OutgoingDirection) <= 0f)
            n = context.FrontFace ? context.GeometricNormal : -context.GeometricNormal;
        return n;
    }

    public static bool SameSide(in BsdfContext context, Vector3 wi) =>
        Vector3.Dot(context.GeometricNormal, wi) * Vector3.Dot(context.GeometricNormal, context.OutgoingDirection) > 0f;

    public static Vector3 ToWorld(Vector3 local, Vector3 normal)
    {
        Vector3 tangent = TextureContext.CreateTangent(normal);
        return Vector3.Normalize(tangent * local.X + Vector3.Cross(normal, tangent) * local.Y + normal * local.Z);
    }

    public static Vector3 CosineHemisphere(Vector2 u, Vector3 normal)
    {
        float radius = MathF.Sqrt(u.X), angle = MathF.Tau * u.Y;
        return ToWorld(new(radius * MathF.Cos(angle), radius * MathF.Sin(angle), MathF.Sqrt(1f - u.X)), normal);
    }

    public static float Alpha(float roughness) => MathF.Max(roughness * roughness, 0.001f);

    public static float Distribution(float cosHalf, float alpha)
    {
        if (cosHalf <= 0f) return 0f;
        double a2 = (double)alpha * alpha;
        double cosine = Math.Min(cosHalf, 1f);
        double d = cosine * cosine * (a2 - 1d) + 1d;
        return (float)(a2 / (Math.PI * d * d));
    }

    public static float Masking(float cosine, float alpha)
    {
        cosine = MathF.Abs(cosine);
        if (cosine <= 0f) return 0f;
        return 2f * cosine / (cosine + MathF.Sqrt(alpha * alpha + (1f - alpha * alpha) * cosine * cosine));
    }

    public static Vector3 SampleHalf(Vector2 u, Vector3 normal, float alpha)
    {
        float cosine = MathF.Sqrt((1f - u.X) / (1f + (alpha * alpha - 1f) * u.X));
        float sine = MathF.Sqrt(MathF.Max(0f, 1f - cosine * cosine));
        float angle = MathF.Tau * u.Y;
        return ToWorld(new(sine * MathF.Cos(angle), sine * MathF.Sin(angle), cosine), normal);
    }

    public static Vector3 Schlick(Vector3 f0, float cosine)
    {
        float x = 1f - Math.Clamp(cosine, 0f, 1f);
        return f0 + (Vector3.One - f0) * (x * x * x * x * x);
    }

    public static float Fresnel(float cosine, float etaIncident, float etaTransmitted)
    {
        if (etaIncident - etaTransmitted == 0f) return 0f;
        cosine = Math.Clamp(MathF.Abs(cosine), 0f, 1f);
        double eta = (double)etaIncident / etaTransmitted;
        double sinSquared = eta * eta * Math.Max(0d, 1d - (double)cosine * cosine);
        if (sinSquared >= 1d) return 1f;
        double cosTransmitted = Math.Sqrt(1d - sinSquared);
        double parallel = (etaTransmitted * cosine - etaIncident * cosTransmitted)
            / (etaTransmitted * cosine + etaIncident * cosTransmitted);
        double perpendicular = (etaIncident * cosine - etaTransmitted * cosTransmitted)
            / (etaIncident * cosine + etaTransmitted * cosTransmitted);
        return (float)((parallel * parallel + perpendicular * perpendicular) * 0.5d);
    }

    public static bool Refract(Vector3 wo, Vector3 normal, float eta, out Vector3 wi)
    {
        float cosine = Math.Clamp(Vector3.Dot(wo, normal), 0f, 1f);
        float sinSquared = eta * eta * MathF.Max(0f, 1f - cosine * cosine);
        wi = Vector3.Zero;
        if (sinSquared >= 1f) return false;
        wi = Vector3.Normalize(-eta * wo + (eta * cosine - MathF.Sqrt(1f - sinSquared)) * normal);
        return true;
    }
}
