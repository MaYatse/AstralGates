using System.Numerics;

namespace HybridSdfEngine.Materials.Scattering;

[Flags]
public enum BsdfLobes
{
    None = 0,
    Diffuse = 1,
    Glossy = 2,
    Specular = 4,
    Reflection = 8,
    Transmission = 16
}

/// <summary>World-space unit directions point away from the surface. IORs describe the current path boundary.</summary>
public readonly record struct BsdfContext(Vector3 GeometricNormal, Vector3 OutgoingDirection,
    float IncidentIor, float TransmittedIor)
{
    public bool FrontFace => Vector3.Dot(GeometricNormal, OutgoingDirection) >= 0f;

    public static BsdfContext Create(in MaterialSample material, Vector3 geometricNormal,
        Vector3 outgoingDirection, float outsideIor = 1f) => Vector3.Dot(geometricNormal, outgoingDirection) >= 0f
            ? new(geometricNormal, outgoingDirection, outsideIor, material.IndexOfRefraction)
            : new(geometricNormal, outgoingDirection, material.IndexOfRefraction, outsideIor);
}

/// <summary>Value is f, Pdf is a solid-angle density, or a discrete probability for Specular samples.</summary>
public readonly record struct BsdfSample(Vector3 Direction, Vector3 Value, float Pdf, BsdfLobes Flags)
{
    public bool IsValid => Pdf > 0f && float.IsFinite(Pdf) && BsdfMath.IsFinite(Value)
        && BsdfMath.IsFinite(Direction) && Direction.LengthSquared() > 0f;
}

public interface IBsdf
{
    BsdfLobes Flags { get; }
    Vector3 Evaluate(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection);
    float Pdf(in MaterialSample material, in BsdfContext context, Vector3 incomingDirection);
    /// <summary>Random components must be in [0,1). Rejected directions return a zero-PDF sample.</summary>
    BsdfSample Sample(in MaterialSample material, in BsdfContext context, Vector3 random);
    /// <summary>Approximate straight shadow-ray attenuation at a boundary. Opaque by default.</summary>
    Vector3 ShadowTransmittance(in MaterialSample material, in BsdfContext context) => Vector3.Zero;
}
