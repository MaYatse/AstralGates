using System.Numerics;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Tracing;

namespace HybridSdfEngine.Render;

/// <summary>Direct diffuse lighting and per-sample shadow visibility.</summary>
public sealed class DiffuseRenderer
{
    private float _shadowBias = 0.002f;
    private float _shadowMaxDistance = 1000f;
    private int _shadowMaxSteps = 256;

    public bool ShadowsEnabled { get; set; } = true;

    /// <summary>Surface offset and endpoint margin in world units. Tune for the scene scale.</summary>
    public float ShadowBias
    {
        get => _shadowBias;
        set
        {
            ValidatePositiveFinite(value);
            _shadowBias = value;
        }
    }

    /// <summary>Shadow range for infinite-distance lights. Finite lights use their actual distance.</summary>
    public float ShadowMaxDistance
    {
        get => _shadowMaxDistance;
        set
        {
            ValidatePositiveFinite(value);
            _shadowMaxDistance = value;
        }
    }

    /// <summary>SDF step budget per shadow ray. An exhausted budget conservatively blocks light.</summary>
    public int ShadowMaxSteps
    {
        get => _shadowMaxSteps;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _shadowMaxSteps = value;
        }
    }

    public SceneTracer Tracer { get; }

    public DiffuseRenderer(Scene scene) : this(new SceneTracer(scene)) { }

    public DiffuseRenderer(SceneTracer tracer)
    {
        ArgumentNullException.ThrowIfNull(tracer);
        Tracer = tracer;
    }

    private static void ValidatePositiveFinite(float value)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(nameof(value), "The value must be finite and positive.");
    }

    public Vector3 RenderRay(Vector3 origin, Vector3 direction)
    {
        var hit = Tracer.Trace(origin, direction);
        
        if (hit.ClosestObject is null) return new(0);

        if (Tracer.Scene.LightSpan.IsEmpty) return new(0);

        var normal = hit.ClosestObject.GetNormal(hit.CollisionPoint);

        Vector3 lighting = Vector3.Zero;

        Vector3 albedo = hit.ClosestObject.Color;

        foreach (var light in Tracer.Scene.LightSpan)
            lighting += SampleDiffuse(light, hit.CollisionPoint, normal);

        return albedo * lighting;
    }

    private Vector3 SampleDiffuse(ILight light, Vector3 point, Vector3 normal)
    {
        int count = light.SampleCount;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        if (count == 1) return Diffuse(light.Sample(point), point, normal);

        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < count; i++)
            sum += Diffuse(light.Sample(point, i), point, normal);
        return sum / count;
    }

    private Vector3 Diffuse(LightSample sample, Vector3 point, Vector3 normal)
    {
        float diffuse = MathF.Max(Vector3.Dot(normal, sample.DirectionToLight), 0f);
        if (diffuse <= 0f || sample.Radiance == Vector3.Zero) return Vector3.Zero;
        if (ShadowsEnabled && IsShadowed(point, normal, sample)) return Vector3.Zero;
        return sample.Radiance * diffuse;
    }

    private bool IsShadowed(Vector3 point, Vector3 normal, LightSample sample)
    {
        Vector3 origin = point + normal * ShadowBias;
        Vector3 direction = sample.DirectionToLight;
        float maxDistance = ShadowMaxDistance;
        if (!float.IsPositiveInfinity(sample.Distance))
        {
            // Re-aim at the same light sample after offsetting the ray origin.
            Vector3 offset = direction * sample.Distance - (origin - point);
            float distance = offset.Length();
            if (distance <= ShadowBias) return false;
            direction = offset / distance;
            maxDistance = distance - ShadowBias;
        }
        return Tracer.OccludedNormalized(origin, direction, maxDistance, ShadowMaxSteps);
    }
}
