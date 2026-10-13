using System.Numerics;

namespace SDFEngine.SDF.Objects;

public interface ISdfObject
{
    float DistanceTo(Vector3 point);
    void Move(Vector3 axis);
    void Set(Vector3 position);
    void Rotate(Vector3 axis, float angle);
}
