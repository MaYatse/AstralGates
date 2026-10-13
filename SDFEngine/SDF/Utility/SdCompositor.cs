using System.Numerics;

using SDFEngine.SDF.Light;
using SDFEngine.SDF.Objects;

namespace SDFEngine.SDF.Utility;

public class SdCompositor(List<ISdfObject> sdfObjects, List<ILight>? lights = null)
{
    const float maxDistance = 1000f;
    const float epsilon = 0.001f;
    public float Map(Vector3 point)
    {
        float minDistance = float.PositiveInfinity;

        for (int i = 0; i < sdfObjects.Count; i++)
        {
            float distance = sdfObjects[i].DistanceTo(point);
            minDistance = MathF.Min(minDistance, distance);
        }

        return minDistance;
    }

    public ISdfObject? GetClosestObject(Vector3 point)
    {
        ISdfObject? closestObject = null;
        float minDistance = float.PositiveInfinity;
        for (int i = 0; i < sdfObjects.Count; i++)
        {
            float distance = sdfObjects[i].DistanceTo(point);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestObject = sdfObjects[i];
            }
        }
        return closestObject;
    }

    public MarchResult RayMarch(Vector3 origin, Vector3 direction)
    {
        float fullDistance = 0;
        
        for (int i = 0; i < maxDistance; i++)
        {
            Vector3 current = origin + direction * fullDistance;

            if (fullDistance >= maxDistance)
                return new(fullDistance, current, null);

            float mapDistance = Map(current);

            if (mapDistance < epsilon) 
                return new(fullDistance, current, GetClosestObject(current));

            fullDistance += mapDistance;
        }

        return new(fullDistance, Vector3.Zero, null);
    }

    public Vector3 DiffusedRender(Vector3 origin, Vector3 direction)
    {
        var hit = RayMarch(origin, direction);
        
        if (hit.ClosestObject is null) return new(0);

        if (lights is null) return new(0);

        var normal = hit.ClosestObject.GetNormal(hit.CollisionPoint);

        Vector3 lighting = Vector3.Zero;

        if (lights is not null)
        {
            foreach (var light in lights)
            {
                var sample = light.Sample(hit.CollisionPoint);

                float diffuse = MathF.Max(
                    Vector3.Dot(normal, sample.DirectionToLight), 0f);

                lighting += sample.Radiance * diffuse;
            }
        }

        return lighting;
    }
}

public readonly record struct MarchResult(float Dist, Vector3 CollisionPoint, ISdfObject? ClosestObject);