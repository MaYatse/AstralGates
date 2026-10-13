using System.Numerics;

namespace SDFEngine.SDF.Objects;

public class SdfSphere(Vector3 position, float radius) : ISdfObject
{
    private Vector3 _position = position;

    public float DistanceTo(Vector3 point) => Vector3.Distance(_position, point) - radius;

    public void Move(Vector3 axis) => _position += axis;

    public void Rotate(Vector3 axis, float angle) => throw new NotImplementedException();

    public void Set(Vector3 position) => _position = position;
}
