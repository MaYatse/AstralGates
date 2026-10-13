using System.Numerics;

namespace SDFEngine.SDF.Objects;

public class SdfBox(Vector3 position, Vector3 scale) : ISdfObject
{
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
        _rotation *= Quaternion.CreateFromAxisAngle(axis, angle);
        _inverseRotation = Quaternion.Inverse(_rotation);
    }

    public Vector3 GetNormal(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(
                point - _position,
                _inverseRotation);
        Vector3 d = Vector3.Abs(localPoint) - scale;
        Vector3 normal = new(
            d.X > d.Y && d.X > d.Z ? MathF.Sign(localPoint.X) : 0,
            d.Y > d.X && d.Y > d.Z ? MathF.Sign(localPoint.Y) : 0,
            d.Z > d.X && d.Z > d.Y ? MathF.Sign(localPoint.Z) : 0
        );
        return Vector3.Normalize(Vector3.Transform(normal, _rotation));
    }
}
