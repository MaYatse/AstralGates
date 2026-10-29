using System.Numerics;

namespace HybridSdfEngine.Render;

/// <summary>Renders linear radiance for a world-space ray.</summary>
public interface IRayRenderer
{
    Vector3 RenderRay(Vector3 origin, Vector3 direction);
    RaySample RenderRaySample(Vector3 origin, Vector3 direction);
}
