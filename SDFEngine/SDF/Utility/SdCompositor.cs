using System.Numerics;

using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.SDF.Utility;

public class SdCompositor
{
    const float epsilon = 0.001f;
    private readonly ISdfObject[] _objects;
    private readonly IRayIntersectable[] _analyticObjects;
    private readonly ISdfObject[] _marchObjects;
    private readonly ILight[] _lights;

    // Membership is captured once; object transforms and materials remain mutable.
    public SdCompositor(List<ISdfObject> sdfObjects, List<ILight>? lights = null)
    {
        _objects = sdfObjects.ToArray();
        _analyticObjects = _objects.OfType<IRayIntersectable>().ToArray();
        _marchObjects = _objects.Where(o => o is not IRayIntersectable).ToArray();
        _lights = lights?.ToArray() ?? [];
    }

    public float Map(Vector3 point)
    {
        float minDistance = float.PositiveInfinity;

        for (int i = 0; i < _objects.Length; i++)
        {
            float distance = _objects[i].DistanceTo(point);
            minDistance = MathF.Min(minDistance, distance);
        }

        return minDistance;
    }

    public ISdfObject? GetClosestObject(Vector3 point)
    {
        ISdfObject? closestObject = null;
        float minDistance = float.PositiveInfinity;
        for (int i = 0; i < _objects.Length; i++)
        {
            float distance = _objects[i].DistanceTo(point);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestObject = _objects[i];
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
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z))
            throw new ArgumentException("Ray origin must be finite.", nameof(origin));
        float length = direction.Length();
        if (!float.IsFinite(length) || length <= 0f)
            throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        if (!float.IsFinite(maxDistance) || maxDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSteps);
        direction /= length;

        var analyticHit = IntersectAnalyticObjects(origin, direction, maxDistance);
        return _marchObjects.Length == 0
            ? analyticHit
            : MarchSdfObjects(origin, direction, analyticHit, maxSteps);
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

    // Compatibility for callers using the old name.
    public MarchResult RayMarch(Vector3 origin, Vector3 direction) => Trace(origin, direction);

    public Vector3 DiffusedRender(Vector3 origin, Vector3 direction)
    {
        var hit = Trace(origin, direction);
        
        if (hit.ClosestObject is null) return new(0);

        if (_lights.Length == 0) return new(0);

        var normal = hit.ClosestObject.GetNormal(hit.CollisionPoint);

        Vector3 lighting = Vector3.Zero;

        Vector3 albedo = hit.ClosestObject.Color;

        foreach (var light in _lights)
        {
            var sample = light.Sample(hit.CollisionPoint);

            float diffuse = MathF.Max(
                Vector3.Dot(normal, sample.DirectionToLight), 0f);

            lighting += sample.Radiance * diffuse;
        }

        return albedo * lighting;
    }
}

public readonly record struct MarchResult(float Dist, Vector3 CollisionPoint, ISdfObject? ClosestObject)
{
    public bool Hit => ClosestObject is not null;
    public bool StepLimitReached { get; init; }
}
