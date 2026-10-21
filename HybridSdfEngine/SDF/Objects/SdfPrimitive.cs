using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>Shared rigid transform and material support for local-space primitives.</summary>
public abstract class SdfPrimitive : ISdfObject
{
    private Vector3 _position;
    private Quaternion _rotation = Quaternion.Identity;
    private Quaternion _inverseRotation = Quaternion.Identity;

    public Vector3 Position => _position;
    public IMaterial? Material { get; set; }

    protected SdfPrimitive(Vector3 position)
    {
        ValidateVector(position, nameof(position));
        _position = position;
    }

    public float DistanceTo(Vector3 point) => LocalDistance(ToLocalPoint(point));
    public Vector3 GetNormal(Vector3 point) => ToWorldDirection(LocalNormal(ToLocalPoint(point)));

    public void Move(Vector3 axis) => Set(_position + axis);

    public void Set(Vector3 position)
    {
        ValidateVector(position, nameof(position));
        _position = position;
    }

    /// <summary>Rotates about the primitive's position around a world-space axis.</summary>
    public void Rotate(Vector3 axis, float angle)
    {
        float length = axis.Length();
        if (!float.IsFinite(length) || length <= 0f || !float.IsFinite(angle))
            throw new ArgumentException("Rotation requires a finite nonzero axis and a finite angle.");
        _rotation = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(axis / length, angle) * _rotation);
        _inverseRotation = Quaternion.Conjugate(_rotation);
    }

    public TextureContext GetTextureContext(Vector3 point)
    {
        Vector3 local = ToLocalPoint(point);
        Vector3 localNormal = LocalNormal(local);
        Vector3 normal = ToWorldDirection(localNormal);
        Vector3 tangent = ToWorldDirection(LocalTangent(local, localNormal));
        return new(point, local, normal, localNormal, LocalUv(local, localNormal),
            tangent, Vector3.Cross(normal, tangent));
    }

    protected Vector3 ToLocalPoint(Vector3 point) => Vector3.Transform(point - _position, _inverseRotation);
    protected Vector3 ToLocalDirection(Vector3 direction) => Vector3.Transform(direction, _inverseRotation);
    private Vector3 ToWorldDirection(Vector3 direction) => Vector3.Normalize(Vector3.Transform(direction, _rotation));

    protected abstract float LocalDistance(Vector3 point);
    protected abstract Vector3 LocalNormal(Vector3 point);
    protected abstract Vector2 LocalUv(Vector3 point, Vector3 normal);
    protected abstract Vector3 LocalTangent(Vector3 point, Vector3 normal);

    protected static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(name, "The dimension must be finite and positive.");
    }

    protected static void ValidateVector(Vector3 value, string name)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new ArgumentException("Coordinates must be finite.", name);
    }

    protected static Vector3 RadialDirection(Vector3 point)
    {
        float radius = new Vector2(point.X, point.Z).Length();
        return radius > 0f ? new(point.X / radius, 0f, point.Z / radius) : Vector3.UnitX;
    }

    protected static Vector3 AzimuthTangent(Vector3 point)
    {
        Vector3 radial = RadialDirection(point);
        return new(-radial.Z, 0f, radial.X);
    }
}
