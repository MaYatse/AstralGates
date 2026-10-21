using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>A ring torus around local Y: MajorRadius is the ring radius, MinorRadius the tube radius.</summary>
public sealed class SdfTorus : SdfPrimitive
{
    public float MajorRadius { get; }
    public float MinorRadius { get; }

    public SdfTorus(Vector3 position, float majorRadius, float minorRadius) : base(position)
    {
        ValidatePositive(majorRadius, nameof(majorRadius));
        ValidatePositive(minorRadius, nameof(minorRadius));
        if (minorRadius >= majorRadius)
            throw new ArgumentOutOfRangeException(nameof(minorRadius), "The tube radius must be smaller than the ring radius.");
        MajorRadius = majorRadius;
        MinorRadius = minorRadius;
    }

    protected override float LocalDistance(Vector3 point) =>
        new Vector2(new Vector2(point.X, point.Z).Length() - MajorRadius, point.Y).Length() - MinorRadius;

    protected override Vector3 LocalNormal(Vector3 point)
    {
        Vector3 radial = RadialDirection(point);
        Vector3 offset = point - radial * MajorRadius;
        return offset.LengthSquared() > 0f ? Vector3.Normalize(offset) : radial;
    }

    protected override Vector2 LocalUv(Vector3 point, Vector3 normal) =>
        new(MathF.Atan2(point.Z, point.X) / MathF.Tau + 0.5f,
            MathF.Atan2(-point.Y, new Vector2(point.X, point.Z).Length() - MajorRadius) / MathF.Tau + 0.5f);

    protected override Vector3 LocalTangent(Vector3 point, Vector3 normal) => AzimuthTangent(point);
}
