using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

public class SdfPlane(Vector3 position, Vector3 normal, Vector3? color = null) : IRayIntersectable
{
    public Vector3 Color => color ?? new Vector3(1);
    private Vector3 _position = position;
    private Vector3 _normal = NormalizeNormal(normal);

    public float DistanceTo(Vector3 point) => Vector3.Dot(point - _position, _normal);

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        float offset = Vector3.Dot(_position - origin, _normal);
        float denominator = Vector3.Dot(direction, _normal);
        distance = 0f;
        // A ray on the surface already intersects it, including a coplanar ray.
        if (offset == 0f) return true;
        if (denominator == 0f) return false;

        distance = offset / denominator;
        return distance >= 0f && distance <= maxDistance;
    }

    public void Move(Vector3 axis) => _position += axis;
    public void Set(Vector3 position) => _position = position;

    public void Rotate(Vector3 axis, float angle)
    {
        float length = axis.Length();
        if (!float.IsFinite(length) || length <= 0f || !float.IsFinite(angle))
            throw new ArgumentException("Rotation requires a finite nonzero axis and a finite angle.");
        var rotation = Quaternion.CreateFromAxisAngle(axis / length, angle);
        _normal = Vector3.Normalize(Vector3.Transform(_normal, rotation));
    }

    public Vector3 GetNormal(Vector3 point) => _normal;

    private static Vector3 NormalizeNormal(Vector3 normal)
    {
        float length = normal.Length();
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException("Plane normal must be finite and nonzero.", nameof(normal));
        return normal / length;
    }
}
