using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

public class SpotLight : ILight
{
    public Vector3 Color { get; set; } = Vector3.One;
    public float Intensity { get; set; } = 1f;

    public LightSample Sample(Vector3 point)
    {
        throw new NotImplementedException();
    }
}
