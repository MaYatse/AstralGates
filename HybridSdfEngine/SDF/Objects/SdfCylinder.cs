using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>A closed cylinder centered at Position, along local Y. Height is the full height.</summary>
public sealed class SdfCylinder : SdfPrimitive, IRayIntersectable
{
    public float Radius { get; }
    public float Height { get; }

    public SdfCylinder(Vector3 position, float radius, float height) : base(position)
    {
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(height, nameof(height));
        Radius = radius;
        Height = height;
    }

    protected override float LocalDistance(Vector3 point)
    {
        Vector2 d = new(new Vector2(point.X, point.Z).Length() - Radius, MathF.Abs(point.Y) - Height * 0.5f);
        return Vector2.Max(d, Vector2.Zero).Length() + MathF.Min(MathF.Max(d.X, d.Y), 0f);
    }

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance) =>
        CappedSurfaceIntersection.TryIntersect(ToLocalPoint(origin), ToLocalDirection(direction),
            Radius, Radius, Height, maxDistance, out distance);

    protected override Vector3 LocalNormal(Vector3 point)
    {
        float side = new Vector2(point.X, point.Z).Length() - Radius;
        float cap = MathF.Abs(point.Y) - Height * 0.5f;
        Vector3 capNormal = point.Y < 0f ? -Vector3.UnitY : Vector3.UnitY;
        Vector3 radial = RadialDirection(point);
        if (side > 0f && cap > 0f) return Vector3.Normalize(radial * side + capNormal * cap);
        return cap >= side ? capNormal : radial;
    }

    protected override Vector2 LocalUv(Vector3 point, Vector3 normal) => IsCap(normal)
        ? new(point.X / (2f * Radius) + 0.5f, -normal.Y * point.Z / (2f * Radius) + 0.5f)
        : new(MathF.Atan2(point.Z, point.X) / MathF.Tau + 0.5f, 0.5f - point.Y / Height);

    protected override Vector3 LocalTangent(Vector3 point, Vector3 normal) =>
        IsCap(normal) ? Vector3.UnitX : AzimuthTangent(point);

    private static bool IsCap(Vector3 normal) => normal.X == 0f && normal.Z == 0f;
}
