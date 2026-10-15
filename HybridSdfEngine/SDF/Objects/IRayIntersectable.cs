using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>A surface with an exact ray intersection, in addition to its SDF.</summary>
public interface IRayIntersectable : ISdfObject
{
    /// <summary>
    /// Finds the nearest surface in [0, maxDistance]. Direction must be normalized.
    /// A ray starting inside a solid finds its exit surface.
    /// </summary>
    bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance);
}
