using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>A zero-thickness triangle with local vertices. DistanceTo is unsigned; winding defines its normal.</summary>
public sealed class SdfTriangle : SdfPrimitive, IRayIntersectable
{
    private readonly Vector3 _ab, _ac, _normal, _tangent;
    private readonly double _aa, _abac, _cc, _denominator;
    public Vector3 A { get; }
    public Vector3 B { get; }
    public Vector3 C { get; }

    public SdfTriangle(Vector3 position, Vector3 a, Vector3 b, Vector3 c) : base(position)
    {
        ValidateVector(a, nameof(a));
        ValidateVector(b, nameof(b));
        ValidateVector(c, nameof(c));
        A = a;
        B = b;
        C = c;
        _ab = b - a;
        _ac = c - a;
        Vector3 cross = Vector3.Cross(_ab, _ac);
        float area = cross.Length();
        if (!float.IsFinite(area) || area <= 0f)
            throw new ArgumentException("Triangle vertices must form a finite, non-degenerate triangle.");
        _normal = cross / area;
        _tangent = Vector3.Normalize(_ab);
        _aa = Dot(_ab, _ab);
        _abac = Dot(_ab, _ac);
        _cc = Dot(_ac, _ac);
        _denominator = _aa * _cc - _abac * _abac;
        if (_denominator <= 0d) throw new ArgumentException("Triangle vertices are too close to collinear.");
    }

    protected override float LocalDistance(Vector3 point)
    {
        if (ContainsProjection(point)) return MathF.Abs(Vector3.Dot(point - A, _normal));
        return MathF.Sqrt(MathF.Min(SegmentDistanceSquared(point, A, B),
            MathF.Min(SegmentDistanceSquared(point, B, C), SegmentDistanceSquared(point, C, A))));
    }

    protected override Vector3 LocalNormal(Vector3 point) => _normal;
    protected override Vector3 LocalTangent(Vector3 point, Vector3 normal) => _tangent;

    protected override Vector2 LocalUv(Vector3 point, Vector3 normal)
    {
        Vector3 offset = point - A;
        double alongB = Dot(offset, _ab), alongC = Dot(offset, _ac);
        return new((float)((_cc * alongB - _abac * alongC) / _denominator),
            (float)((_aa * alongC - _abac * alongB) / _denominator));
    }

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        origin = ToLocalPoint(origin);
        direction = ToLocalDirection(direction);
        double offset = Dot(A - origin, _normal);
        double denominator = Dot(direction, _normal);
        distance = 0f;
        if (denominator == 0d)
            return offset == 0d && IntersectCoplanar(origin, direction, maxDistance, out distance);
        double t = offset / denominator;
        distance = (float)t;
        if (t < 0d || distance > maxDistance || !float.IsFinite(distance)) return false;
        return ContainsProjection(origin + direction * distance);
    }

    private bool ContainsProjection(Vector3 point) =>
        EdgeValue(A, B, point) >= 0d && EdgeValue(B, C, point) >= 0d && EdgeValue(C, A, point) >= 0d;

    private double EdgeValue(Vector3 a, Vector3 b, Vector3 point) => Dot(Vector3.Cross(b - a, point - a), _normal);

    private bool IntersectCoplanar(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        double enter = 0d, exit = maxDistance;
        bool hit = ClipEdge(A, B, origin, direction, ref enter, ref exit)
            && ClipEdge(B, C, origin, direction, ref enter, ref exit)
            && ClipEdge(C, A, origin, direction, ref enter, ref exit);
        distance = (float)enter;
        return hit;
    }

    private bool ClipEdge(Vector3 a, Vector3 b, Vector3 origin, Vector3 direction, ref double enter, ref double exit)
    {
        double offset = EdgeValue(a, b, origin);
        double slope = Dot(Vector3.Cross(b - a, direction), _normal);
        if (slope == 0d) return offset >= 0d;
        double t = -offset / slope;
        if (slope > 0d) enter = Math.Max(enter, t);
        else exit = Math.Min(exit, t);
        return enter <= exit;
    }

    private static float SegmentDistanceSquared(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 edge = b - a;
        float t = Math.Clamp(Vector3.Dot(point - a, edge) / edge.LengthSquared(), 0f, 1f);
        return Vector3.DistanceSquared(point, a + edge * t);
    }

    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
}
