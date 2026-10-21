using System.Numerics;

namespace HybridSdfEngine.Materials.Scattering;

/// <summary>Normalized one-sided Lambertian reflection, restricted to the geometric reflection hemisphere.</summary>
public sealed class LambertianBsdf : IBsdf
{
    public static LambertianBsdf Instance { get; } = new();
    public BsdfLobes Flags => BsdfLobes.Diffuse | BsdfLobes.Reflection;

    public Vector3 Evaluate(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection) =>
        context.FrontFace && BsdfMath.SameSide(context, incomingDirection)
        && Vector3.Dot(BsdfMath.FaceNormal(material, context), incomingDirection) > 0f ? material.Albedo / MathF.PI : Vector3.Zero;

    public float Pdf(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection) =>
        context.FrontFace && BsdfMath.SameSide(context, incomingDirection)
            ? MathF.Max(0f, Vector3.Dot(BsdfMath.FaceNormal(material, context), incomingDirection)) / MathF.PI : 0f;

    public BsdfSample Sample(in MaterialSample material, in BsdfContext context, Vector3 random)
    {
        if (!context.FrontFace) return default;
        Vector3 wi = BsdfMath.CosineHemisphere(new(random.Y, random.Z), BsdfMath.FaceNormal(material, context));
        return new(wi, Evaluate(material, context, wi), Pdf(material, context, wi), Flags);
    }
}
