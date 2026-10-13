using System.Numerics;

namespace SDFEngine.SDF.Light;

public class PointLight(Vector3 position) : ILight
{
    public Vector3 Direction(Vector3 point) => Vector3.Normalize(position - point);
    public float LightMethod(Vector3 point, Vector3 normal) => MathF.Max(Vector3.Dot(normal, Direction(point)), 0);
}
