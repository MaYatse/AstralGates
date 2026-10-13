using System.Numerics;

namespace SDFEngine.SDF.Light;

public interface ILight
{
    public float LightMethod(Vector3 point, Vector3 normal);
}
