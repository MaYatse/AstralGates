using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

public class SdfSphere(Vector3 position, float radius, Vector3? color = null) : IRayIntersectable
{
    public Vector3 Color => color ?? new Vector3(1);
    private Vector3 _position = position;

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

    public void Rotate(Vector3 axis, float angle) => throw new NotImplementedException();

    public void Set(Vector3 position) => _position = position;

    public Vector3 GetNormal(Vector3 point) => Vector3.Normalize(point - _position);
}
