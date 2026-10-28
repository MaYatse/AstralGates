using System.Numerics;

namespace HybridSdfEngine.Render;

/// <summary>A single 5x5 cross-bilateral pass for linear indirect radiance. No history or additional rays.</summary>
public sealed class SpatialDenoiser
{
    private static readonly int[] Kernel = [1, 4, 6, 4, 1];

    /// <summary>Input and output must be separate buffers. Guides refer to the current frame's primary surfaces.</summary>
    public void Filter(Vector3[] source, SurfaceGuide[] guides, Vector3[] destination, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(guides);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int count = checked(width * height);
        if (source.Length < count || guides.Length < count || destination.Length < count)
            throw new ArgumentException("Denoising buffers must contain the complete frame.");
        if (ReferenceEquals(source, destination))
            throw new ArgumentException("Spatial filtering requires separate input and output buffers.", nameof(destination));
        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
                destination[y * width + x] = FilterPixel(source, guides, width, height, x, y);
        });
    }

    private static Vector3 FilterPixel(Vector3[] source, SurfaceGuide[] guides, int width, int height, int x, int y)
    {
        int index = y * width + x;
        SurfaceGuide center = guides[index];
        if (!center.Filterable || center.Object is null || !float.IsFinite(center.Depth)) return source[index];
        Vector3 sum = Vector3.Zero;
        float weights = 0f;
        for (int row = Math.Max(0, y - 2); row <= Math.Min(height - 1, y + 2); row++)
        for (int column = Math.Max(0, x - 2); column <= Math.Min(width - 1, x + 2); column++)
        {
            int neighbor = row * width + column;
            float weight = GuideWeight(center, guides[neighbor]) * Kernel[row - y + 2] * Kernel[column - x + 2];
            if (weight <= 0f) continue;
            sum += source[neighbor] * weight;
            weights += weight;
        }
        return weights > 0f ? sum / weights : source[index];
    }

    private static float GuideWeight(in SurfaceGuide center, in SurfaceGuide neighbor)
    {
        if (!neighbor.Filterable || !ReferenceEquals(center.Object, neighbor.Object)) return 0f;
        if (!Compatible(center, neighbor)) return 0f;
        float tolerance = 0.001f + 0.02f * MathF.Max(center.Depth, neighbor.Depth);
        float depth = MathF.Abs(center.Depth - neighbor.Depth) / tolerance;
        if (!float.IsFinite(depth) || depth >= 1f) return 0f;
        float normal = Math.Clamp(Vector3.Dot(center.Normal, neighbor.Normal), 0f, 1f);
        normal *= normal;
        normal *= normal;
        normal *= normal;
        normal *= normal; // cos(angle)^16, without an exponential in the inner loop.
        float albedo = MathF.Max(0f, 1f - Vector3.DistanceSquared(center.Albedo, neighbor.Albedo) / 0.01f);
        return (1f - depth) * normal * albedo;
    }

    internal static bool Compatible(in SurfaceGuide first, in SurfaceGuide second) =>
        ReferenceEquals(first.Object, second.Object)
        && Vector3.Dot(first.Normal, second.Normal) >= 0.9f
        && Vector3.DistanceSquared(first.Albedo, second.Albedo) < 0.01f;
}
