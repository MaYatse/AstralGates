using System.Numerics;

using SDFEngine.SDF.Light;
using SDFEngine.SDF.Objects;

namespace SDFEngine.SDF.Utility;

public class SdCompositor(List<ISdfObject> sdfObjects, List<ILight>? lights = null)
{
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

    public Vector3 GetNormal(Vector3 point)
    {
        const float epsilon = 0.001f;

        var dx = new Vector3(epsilon, 0, 0);
        var dy = new Vector3(0, epsilon, 0);
        var dz = new Vector3(0, 0, epsilon);

        var gradient = new Vector3(
            Map(point + dx) - Map(point - dx),
            Map(point + dy) - Map(point - dy),
            Map(point + dz) - Map(point - dz)
        );

        return Vector3.Normalize(gradient);
    }
    public MarchResult RayMarch(Vector3 origin, Vector3 direction)
    {
        float dist = 0;
        
        for (int i = 0; i < 1000; i++)
        {
            Vector3 current = origin + direction * dist;
            float dS = Map(current);
            dist += dS;
            if (dS < 0.001 || dist > 1000.0) return new(dist, current, GetClosestObject(current));
        }

        return new(dist, Vector3.Zero, null);
    }

    public float DiffusedRender(Vector3 origin, Vector3 direction)
    {
        (float dist, Vector3 collisionPoint, _) = RayMarch(origin, direction);
        float result = 0;

        if (dist >= 100) return result;

        result = 1;

        if (lights is null) return result;

        var normal = GetNormal(collisionPoint);

        float lightIntensity = 0;

        foreach (var light in lights)
        {
            lightIntensity += light.LightMethod(collisionPoint, normal);
        }

        result = lightIntensity / lights.Count;

        return result;
    }
}

public record MarchResult(float Dist, Vector3 CollisionPoint, ISdfObject? ClosestObject);