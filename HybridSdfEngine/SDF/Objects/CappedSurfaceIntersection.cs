using System.Numerics;

namespace HybridSdfEngine.SDF.Objects;

/// <summary>Exact intersection of a finite cylinder or cone, centered on the local Y axis.</summary>
internal static class CappedSurfaceIntersection
{
    public static bool TryIntersect(Vector3 origin, Vector3 direction, float bottomRadius, float topRadius,
        float height, float maxDistance, out float distance)
    {
        double halfHeight = height * 0.5d;
        double slope = ((double)topRadius - bottomRadius) / height;
        double radiusAtOrigin = bottomRadius + slope * (origin.Y + halfHeight);
        double radiusDelta = slope * direction.Y;
        double a = (double)direction.X * direction.X + (double)direction.Z * direction.Z - radiusDelta * radiusDelta;
        double halfB = (double)origin.X * direction.X + (double)origin.Z * direction.Z - radiusAtOrigin * radiusDelta;
        double c = (double)origin.X * origin.X + (double)origin.Z * origin.Z - radiusAtOrigin * radiusAtOrigin;
        double closest = double.PositiveInfinity;

        if (c == 0d && Math.Abs(origin.Y) <= halfHeight) closest = 0d;
        if (Roots(a, halfB, c, out double first, out double second))
        {
            CheckSide(first, origin, direction, halfHeight, ref closest);
            CheckSide(second, origin, direction, halfHeight, ref closest);
        }
        CheckCap(-halfHeight, bottomRadius, origin, direction, ref closest);
        CheckCap(halfHeight, topRadius, origin, direction, ref closest);
        distance = (float)closest;
        return float.IsFinite(distance) && distance >= 0f && distance <= maxDistance;
    }

    private static void CheckSide(double t, Vector3 origin, Vector3 direction, double halfHeight, ref double closest)
    {
        if (t < 0d || t >= closest) return;
        double y = origin.Y + t * direction.Y;
        if (Math.Abs(y) <= halfHeight) closest = t;
    }

    private static void CheckCap(double y, double radius, Vector3 origin, Vector3 direction, ref double closest)
    {
        double t;
        double offset = y - origin.Y;
        if (direction.Y == 0f)
        {
            if (offset != 0d) return;
            t = 0d;
        }
        else t = offset / direction.Y;
        if (t < 0d || t >= closest) return;
        double x = origin.X + t * direction.X;
        double z = origin.Z + t * direction.Z;
        if (x * x + z * z <= radius * radius) closest = t;
    }

    private static bool Roots(double a, double halfB, double c, out double first, out double second)
    {
        first = second = double.NaN;
        if (a == 0d)
        {
            if (halfB == 0d) return false;
            first = second = -c / (2d * halfB);
            return true;
        }
        double discriminant = halfB * halfB - a * c;
        if (discriminant < 0d) return false;
        // This form preserves the smaller root when the two terms almost cancel.
        double q = -halfB - Math.CopySign(Math.Sqrt(discriminant), halfB);
        if (q == 0d) first = second = 0d;
        else
        {
            first = q / a;
            second = c / q;
        }
        return true;
    }
}
