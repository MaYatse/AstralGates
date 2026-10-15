using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

public interface ISdfObject
{
    Vector3 Color => new(1);
    float DistanceTo(Vector3 point);
    void Move(Vector3 axis);
    void Set(Vector3 position);
    void Rotate(Vector3 axis, float angle);
    Vector3 GetNormal(Vector3 point)
    {
        const float epsilon = 0.001f;
        var dx = new Vector3(epsilon, 0, 0);
        var dy = new Vector3(0, epsilon, 0);
        var dz = new Vector3(0, 0, epsilon);
        var gradient = new Vector3(
            DistanceTo(point + dx) - DistanceTo(point - dx),
            DistanceTo(point + dy) - DistanceTo(point - dy),
            DistanceTo(point + dz) - DistanceTo(point - dz)
        );
        return Vector3.Normalize(gradient);
    }
}
