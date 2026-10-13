using System.Numerics;

namespace SDFEngine.SDF.Objects;

public class SdfBox(Vector3 position, Vector3 scale) : ISdfObject
{
    private Vector3 _position = position;
    private Quaternion _rotation = Quaternion.Identity;

    public float DistanceTo(Vector3 point)
    {
        Vector3 localPoint = Vector3.Transform(
                point - _position,
                Quaternion.Inverse(_rotation));


        Vector3 d = Vector3.Abs(localPoint) - scale;
        return Vector3.Max(d, new(0)).Length() + MathF.Min(MathF.Max(d.X, MathF.Max(d.Y, d.Z)), 0);
    }

    public void Move(Vector3 axis) => _position += axis;
    public void Set(Vector3 position) => _position = position;

    public void Rotate(Vector3 axis, float angle) => _rotation *= Quaternion.CreateFromAxisAngle(axis, angle);

}
