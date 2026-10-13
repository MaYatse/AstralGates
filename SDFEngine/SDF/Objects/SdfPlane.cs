using System.Numerics;

namespace SDFEngine.SDF.Objects;

public class SdfPlane(Vector3 position, Vector3 normal) : ISdfObject
{
    private Vector3 _position = position;

    //public float GetMinDist(Vector3 point) => Vector3.Distance(point, position) * MathF.Sign(Vector3.Dot(normal, point - position));
    public float DistanceTo(Vector3 point) => Vector3.Dot(point - _position, normal);

    public void Move(Vector3 axis) => _position += axis;
    public void Set(Vector3 position) => _position = position;

    public void Rotate(Vector3 axis, float angle)
    {
        throw new NotImplementedException();
    }

    public Vector3 GetNormal(Vector3 point) => normal;
}
