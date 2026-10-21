using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>A closed cone along local Y. Base is at -Height/2, apex at +Height/2 relative to Position.</summary>
public sealed class SdfCone : SdfPrimitive, IRayIntersectable
{
    private readonly Vector2 _side;
    private readonly float _sideLengthSquared;
    private readonly Vector2 _sideNormal;
    public float Radius { get; }
    public float Height { get; }

    public SdfCone(Vector3 position, float radius, float height) : base(position)
    {
        ValidatePositive(radius, nameof(radius));
        ValidatePositive(height, nameof(height));
        Radius = radius;
        Height = height;
        _side = new(-radius, height);
        _sideLengthSquared = _side.LengthSquared();
        _sideNormal = Vector2.Normalize(new(height, radius));
    }

    private (float Side, float Base) ProfileDistances(Vector2 point)
    {
        Vector2 fromBaseRim = point - new Vector2(Radius, 0);
        float t = Math.Clamp(Vector2.Dot(fromBaseRim, _side) / _sideLengthSquared, 0f, 1f);
        float sideDistance = (fromBaseRim - _side * t).LengthSquared();
        float baseDistance = (point - new Vector2(Math.Clamp(point.X, 0f, Radius), 0f)).LengthSquared();
        return (sideDistance, baseDistance);
    }

    protected override float LocalDistance(Vector3 point)
    {
        Vector2 profile = new(new Vector2(point.X, point.Z).Length(), point.Y + Height * 0.5f);
        var distances = ProfileDistances(profile);
        float distance = MathF.Sqrt(MathF.Min(distances.Side, distances.Base));
        bool inside = profile.Y >= 0f && profile.Y <= Height && profile.X <= Radius * (1f - profile.Y / Height);
        return inside ? -distance : distance;
    }

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance) =>
        CappedSurfaceIntersection.TryIntersect(ToLocalPoint(origin), ToLocalDirection(direction),
            Radius, 0f, Height, maxDistance, out distance);

    protected override Vector3 LocalNormal(Vector3 point)
    {
        float radial = new Vector2(point.X, point.Z).Length();
        if (radial <= Radius * 1e-6f && point.Y >= Height * (0.5f - 1e-6f)) return Vector3.UnitY;
        var distances = ProfileDistances(new(radial, point.Y + Height * 0.5f));
        return distances.Base <= distances.Side ? -Vector3.UnitY
            : RadialDirection(point) * _sideNormal.X + Vector3.UnitY * _sideNormal.Y;
    }

    protected override Vector2 LocalUv(Vector3 point, Vector3 normal)
    {
        if (normal.Y < 0f) return new(point.X / (2f * Radius) + 0.5f, point.Z / (2f * Radius) + 0.5f);
        float u = normal.X == 0f && normal.Z == 0f ? 0.5f : MathF.Atan2(point.Z, point.X) / MathF.Tau + 0.5f;
        return new(u, 0.5f - point.Y / Height);
    }

    protected override Vector3 LocalTangent(Vector3 point, Vector3 normal)
    {
        if (normal.Y < 0f) return Vector3.UnitX;
        return normal.X == 0f && normal.Z == 0f ? Vector3.UnitZ : AzimuthTangent(point);
    }
}
