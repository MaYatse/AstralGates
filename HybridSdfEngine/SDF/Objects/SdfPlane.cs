using System.Numerics;

using HybridSdfEngine.Materials;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.SDF.Objects;

public class SdfPlane : IRayIntersectable
{
    public IMaterial? Material { get; set; }
    private Vector3 _position;
    private Vector3 _normal, _tangent;
    private readonly Vector3 _localNormal;
    private Quaternion _rotation = Quaternion.Identity;
    private Quaternion _inverseRotation = Quaternion.Identity;

    public SdfPlane(Vector3 position, Vector3 normal)
    {
        _position = position;
        _normal = NormalizeNormal(normal);
        _localNormal = _normal;
        _tangent = TextureContext.CreateTangent(_normal);
    }

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
        _tangent = Vector3.Normalize(Vector3.Transform(_tangent, rotation));
        _rotation = Quaternion.Normalize(rotation * _rotation);
        _inverseRotation = Quaternion.Inverse(_rotation);
    }

    public Vector3 GetNormal(Vector3 point) => _normal;

    public TextureContext GetTextureContext(Vector3 point)
    {
        Vector3 offset = point - _position;
        Vector3 bitangent = Vector3.Cross(_normal, _tangent);
        return new(point, Vector3.Transform(offset, _inverseRotation), _normal, _localNormal,
            new(Vector3.Dot(offset, _tangent), Vector3.Dot(offset, bitangent)), _tangent, bitangent);
    }

    private static Vector3 NormalizeNormal(Vector3 normal)
    {
        float length = normal.Length();
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException("Plane normal must be finite and nonzero.", nameof(normal));
        return normal / length;
    }
}
