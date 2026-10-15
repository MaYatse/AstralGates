using System.Numerics;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.SDF.Tracing;

public readonly record struct MarchResult(float Dist, Vector3 CollisionPoint, ISdfObject? ClosestObject)
{
    public bool Hit => ClosestObject is not null;
    public bool StepLimitReached { get; init; }
}
