using System.Numerics;

using HybridSdfEngine.Materials;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.SDF.Objects;

public class SdfBox(Vector3 position, Vector3 scale, Vector3? color = null) : IRayIntersectable
{
    public Vector3 Color => color ?? new Vector3(1);
    public IMaterial? Material { get; set; }
    private Vector3 _position = position;
    private Quaternion _rotation = Quaternion.Identity;
    private Quaternion _inverseRotation = Quaternion.Identity;

    public float DistanceTo(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(
                point - _position,
                _inverseRotation);


        Vector3 d = Vector3.Abs(localPoint) - scale;
        return Vector3.Max(d, new(0)).Length() + MathF.Min(MathF.Max(d.X, MathF.Max(d.Y, d.Z)), 0);
    }

    public void Move(Vector3 axis) => _position += axis;
    public void Set(Vector3 position) => _position = position;

    public void Rotate(Vector3 axis, float angle)
    {
        float length = axis.Length();
        if (!float.IsFinite(length) || length <= 0f || !float.IsFinite(angle))
            throw new ArgumentException("Rotation requires a finite nonzero axis and a finite angle.");
        _rotation = Quaternion.Normalize(_rotation * Quaternion.CreateFromAxisAngle(axis / length, angle));
        _inverseRotation = Quaternion.Inverse(_rotation);
    }

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        var localOrigin = Vector3.Transform(origin - _position, _inverseRotation);
        var localDirection = Vector3.Transform(direction, _inverseRotation);
        float enter = float.NegativeInfinity, exit = float.PositiveInfinity;
        distance = 0f;
        var bounds = Vector3.Abs(localOrigin) - scale;
        if (MathF.Max(bounds.X, MathF.Max(bounds.Y, bounds.Z)) == 0f) return true;
        if (!IntersectSlab(localOrigin.X, localDirection.X, scale.X, ref enter, ref exit)
            || !IntersectSlab(localOrigin.Y, localDirection.Y, scale.Y, ref enter, ref exit)
            || !IntersectSlab(localOrigin.Z, localDirection.Z, scale.Z, ref enter, ref exit))
            return false;

        // Entry is behind the origin for a ray inside the box: choose its exit.
        distance = enter >= 0f ? enter : exit;
        return distance >= 0f && distance <= maxDistance;
    }

    private static bool IntersectSlab(float origin, float direction, float halfSize, ref float enter, ref float exit)
    {
        // Handle parallel rays explicitly, avoiding 0 * Infinity on a face.
        if (direction == 0f) return origin >= -halfSize && origin <= halfSize;
        float a = (-halfSize - origin) / direction;
        float b = (halfSize - origin) / direction;
        enter = MathF.Max(enter, MathF.Min(a, b));
        exit = MathF.Min(exit, MathF.Max(a, b));
        return enter <= exit;
    }

    public Vector3 GetNormal(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(
                point - _position,
                _inverseRotation);
        return Vector3.Normalize(Vector3.Transform(GetLocalNormal(localPoint), _rotation));
    }

    private Vector3 GetLocalNormal(Vector3 localPoint)
    {
        Vector3 d = Vector3.Abs(localPoint) - scale;
        // On edges/corners several faces tie; choose one deterministically.
        Vector3 normal;
        if (d.X >= d.Y && d.X >= d.Z)
            normal = new(MathF.Sign(localPoint.X), 0, 0);
        else if (d.Y >= d.Z)
            normal = new(0, MathF.Sign(localPoint.Y), 0);
        else
            normal = new(0, 0, MathF.Sign(localPoint.Z));
        return normal;
    }

    public TextureContext GetTextureContext(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(point - _position, _inverseRotation);
        Vector3 localNormal = GetLocalNormal(localPoint);
        Vector3 localTangent;
        if (localNormal.X != 0f) localTangent = new(0, 0, -localNormal.X);
        else if (localNormal.Y != 0f) localTangent = Vector3.UnitX;
        else localTangent = new(localNormal.Z, 0, 0);
        Vector3 localBitangent = Vector3.Cross(localNormal, localTangent);
        float width = 2f * Vector3.Dot(Vector3.Abs(localTangent), scale);
        float height = 2f * Vector3.Dot(Vector3.Abs(localBitangent), scale);
        Vector2 uv = new(Vector3.Dot(localPoint, localTangent) / width + 0.5f,
            Vector3.Dot(localPoint, localBitangent) / height + 0.5f);
        Vector3 normal = Vector3.Normalize(Vector3.Transform(localNormal, _rotation));
        Vector3 tangent = Vector3.Normalize(Vector3.Transform(localTangent, _rotation));
        return new(point, localPoint, normal, localNormal, uv, tangent, Vector3.Cross(normal, tangent));
    }
}
