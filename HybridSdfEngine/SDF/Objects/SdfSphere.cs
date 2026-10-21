using System.Numerics;

using HybridSdfEngine.Materials;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.SDF.Objects;

public class SdfSphere(Vector3 position, float radius) : IRayIntersectable
{
    public IMaterial? Material { get; set; }
    private Vector3 _position = position;
    private Quaternion _rotation = Quaternion.Identity;
    private Quaternion _inverseRotation = Quaternion.Identity;

    public float DistanceTo(Vector3 point) => Vector3.Distance(_position, point) - radius;

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        var offset = origin - _position;
        // Double intermediates avoid cancellation for small spheres far from the camera.
        double a = (double)direction.X * direction.X + (double)direction.Y * direction.Y + (double)direction.Z * direction.Z;
        double halfB = (double)offset.X * direction.X + (double)offset.Y * direction.Y + (double)offset.Z * direction.Z;
        double c = (double)offset.X * offset.X + (double)offset.Y * offset.Y + (double)offset.Z * offset.Z - (double)radius * radius;
        double discriminant = halfB * halfB - a * c;
        distance = 0f;
        if (discriminant < 0d || a == 0d) return false;

        double root = Math.Sqrt(discriminant);
        double t = (-halfB - root) / a;
        if (t < 0d) t = (-halfB + root) / a;
        if (t < 0d || t > maxDistance) return false;
        distance = (float)t;
        return true;
    }

    public void Move(Vector3 axis) => _position += axis;

    public void Rotate(Vector3 axis, float angle)
    {
        float length = axis.Length();
        if (!float.IsFinite(length) || length <= 0f || !float.IsFinite(angle))
            throw new ArgumentException("Rotation requires a finite nonzero axis and a finite angle.");
        _rotation = Quaternion.Normalize(_rotation * Quaternion.CreateFromAxisAngle(axis / length, angle));
        _inverseRotation = Quaternion.Inverse(_rotation);
    }

    public void Set(Vector3 position) => _position = position;

    public Vector3 GetNormal(Vector3 point) => Vector3.Normalize(point - _position);

    public TextureContext GetTextureContext(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(point - _position, _inverseRotation);
        Vector3 localNormal = Vector3.Normalize(localPoint);
        Vector2 uv = new(MathF.Atan2(localNormal.Z, localNormal.X) / MathF.Tau + 0.5f,
            MathF.Acos(Math.Clamp(localNormal.Y, -1f, 1f)) / MathF.PI);
        Vector3 localTangent = new(-localNormal.Z, 0, localNormal.X);
        localTangent = localTangent.LengthSquared() > 1e-12f ? Vector3.Normalize(localTangent) : Vector3.UnitZ;
        Vector3 normal = GetNormal(point);
        Vector3 tangent = Vector3.Normalize(Vector3.Transform(localTangent, _rotation));
        return new(point, localPoint, normal, localNormal, uv, tangent, Vector3.Cross(normal, tangent));
    }
}
