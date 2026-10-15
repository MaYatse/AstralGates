using System.Numerics;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.SDF.Tracing;

/// <summary>Geometric queries over fixed scene membership and mutable object transforms.</summary>
public sealed class SceneTracer
{
    private const float epsilon = 0.001f;
    private readonly IRayIntersectable[] _analyticObjects;
    private readonly ISdfObject[] _marchObjects;

    public Scene Scene { get; }

    public SceneTracer(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Scene = scene;
        _analyticObjects = scene.Objects.OfType<IRayIntersectable>().ToArray();
        _marchObjects = scene.Objects.Where(obj => obj is not IRayIntersectable).ToArray();
    }

    public float Map(Vector3 point)
    {
        var objects = Scene.ObjectSpan;
        float minDistance = float.PositiveInfinity;

        for (int i = 0; i < objects.Length; i++)
        {
            float distance = objects[i].DistanceTo(point);
            minDistance = MathF.Min(minDistance, distance);
        }

        return minDistance;
    }

    public ISdfObject? GetClosestObject(Vector3 point)
    {
        var objects = Scene.ObjectSpan;
        ISdfObject? closestObject = null;
        float minDistance = float.PositiveInfinity;
        for (int i = 0; i < objects.Length; i++)
        {
            float distance = objects[i].DistanceTo(point);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestObject = objects[i];
            }
        }
        return closestObject;
    }

    /// <summary>
    /// Finds the nearest forward surface, using exact intersections where available.
    /// Direction is normalized here; distances and maxDistance are in world units.
    /// Independent scene objects are surfaces, not a CSG union of their interiors.
    /// </summary>
    public MarchResult Trace(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int maxSteps = 1000)
    {
        direction = ValidateRay(origin, direction, maxDistance, maxSteps);
        var analyticHit = IntersectAnalyticObjects(origin, direction, maxDistance);
        return _marchObjects.Length == 0
            ? analyticHit
            : MarchSdfObjects(origin, direction, analyticHit, maxSteps);
    }

    /// <summary>
    /// Tests for any surface before maxDistance, normalizing direction.
    /// The endpoint is excluded; callers must offset surface origins themselves.
    /// Returns true if the SDF step budget is exhausted before proving visibility.
    /// </summary>
    public bool Occluded(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int maxSteps = 256)
    {
        direction = ValidateRay(origin, direction, maxDistance, maxSteps);
        return OccludedNormalized(origin, direction, maxDistance, maxSteps);
    }

    internal bool OccludedNormalized(Vector3 origin, Vector3 direction, float maxDistance, int maxSteps)
    {
        if (maxDistance <= 0f) return false;
        foreach (var obj in _analyticObjects)
        {
            if (obj.TryIntersect(origin, direction, maxDistance, out float distance) && distance < maxDistance)
                return true;
        }
        if (_marchObjects.Length == 0) return false;

        float traveled = 0f;
        for (int step = 0; step < maxSteps && traveled < maxDistance; step++)
        {
            float distance = FindNearestSdfSurface(origin + direction * traveled).Distance;
            if (distance >= maxDistance - traveled) return false;
            if (distance < epsilon) return true;
            traveled += distance;
        }
        return traveled < maxDistance;
    }

    private static Vector3 ValidateRay(Vector3 origin, Vector3 direction, float maxDistance, int maxSteps)
    {
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z))
            throw new ArgumentException("Ray origin must be finite.", nameof(origin));
        float length = direction.Length();
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);
        return direction / length;
    }

    private MarchResult IntersectAnalyticObjects(Vector3 origin, Vector3 direction, float maxDistance)
    {
        float closestDistance = maxDistance;
        ISdfObject? closestObject = null;
        foreach (var obj in _analyticObjects)
        {
            if (obj.TryIntersect(origin, direction, closestDistance, out float distance)
                && (closestObject is null || distance < closestDistance))
            {
                closestDistance = distance;
                closestObject = obj;
            }
        }

        return new(closestDistance, origin + direction * closestDistance, closestObject);
    }

    private MarchResult MarchSdfObjects(Vector3 origin, Vector3 direction, MarchResult analyticHit, int maxSteps)
    {
        float traveled = 0f;
        for (int step = 0; step < maxSteps; step++)
        {
            Vector3 point = origin + direction * traveled;
            var (nearestDistance, nearestObject) = FindNearestSdfSurface(point);

            if (nearestDistance < epsilon)
                return new(traveled, point, nearestObject);
            if (nearestDistance > analyticHit.Dist - traveled || traveled >= analyticHit.Dist)
                return analyticHit;

            traveled += nearestDistance;
        }

        // Reaching the step budget does not prove an analytic hit behind the SDF is visible.
        return new(traveled, origin + direction * traveled, null) { StepLimitReached = true };
    }

    private (float Distance, ISdfObject? Object) FindNearestSdfSurface(Vector3 point)
    {
        float nearestDistance = float.PositiveInfinity;
        ISdfObject? nearestObject = null;
        foreach (var obj in _marchObjects)
        {
            // Absolute signed distance also lets rays leave a solid from inside.
            float distance = MathF.Abs(obj.DistanceTo(point));
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestObject = obj;
            }
        }
        return (nearestDistance, nearestObject);
    }

}
