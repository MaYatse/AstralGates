using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.Render.Environment;

namespace HybridSdfEngine.Render;

/// <summary>Shared ray-tracing configuration and surface lighting for ray renderers.</summary>
public abstract class RayRendererBase : IRayRenderer
{
    private float _shadowBias = 0.002f, _shadowMaxDistance = 1000f, _maxDistance = 1000f;
    private float _directLightScale = MathF.PI, _outsideIor = 1f;
    private int _shadowMaxSteps = 256, _maxSteps = 1000, _maxBounces = 6, _maxShadowIntersections = 32;
    private Vector3 _backgroundColor;

    private readonly DirectLightingEvaluator _lighting;

    public SceneTracer Tracer { get; }
    public bool ShadowsEnabled { get; set; } = true;
    public bool TransparentShadowsEnabled { get; set; } = true;

    public float ShadowBias { get => _shadowBias; set { Positive(value); _shadowBias = value; } }
    public float ShadowMaxDistance { get => _shadowMaxDistance; set { Positive(value); _shadowMaxDistance = value; } }
    public int ShadowMaxSteps { get => _shadowMaxSteps; set { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); _shadowMaxSteps = value; } }
    public float MaxDistance { get => _maxDistance; set { Positive(value); _maxDistance = value; } }
    public int MaxSteps { get => _maxSteps; set { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); _maxSteps = value; } }
    public int MaxBounces { get => _maxBounces; set { Range(value, 0, 32); _maxBounces = value; } }
    public int MaxShadowIntersections { get => _maxShadowIntersections; set { Range(value, 1, 256); _maxShadowIntersections = value; } }
    /// <summary>PI preserves the existing light intensity convention; use 1 for radiometrically scaled incident radiance.</summary>
    public float DirectLightScale { get => _directLightScale; set { Positive(value); _directLightScale = value; } }
    public float OutsideIor { get => _outsideIor; set { Positive(value); _outsideIor = value; } }
    public Vector3 BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (!BsdfMath.IsFinite(value) || value.X < 0 || value.Y < 0 || value.Z < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _backgroundColor = value;
        }
    }

    protected RayRendererBase(SceneTracer tracer)
    {
        ArgumentNullException.ThrowIfNull(tracer);
        Tracer = tracer;
        _lighting = new DirectLightingEvaluator(this);
    }

    public abstract Vector3 RenderRay(Vector3 origin, Vector3 direction);
    public abstract RaySample RenderRaySample(Vector3 origin, Vector3 direction);

    protected Vector3 DirectLighting(in MaterialSample surface, in BsdfContext context, Vector3 point, bool transparentShadows,
        int maxLightSamples = int.MaxValue, int maxLights = int.MaxValue) =>
        _lighting.Evaluate(surface, context, point, transparentShadows, maxLightSamples, maxLights);

    protected bool HasTransmissiveSurfaces()
    {
        foreach (ISdfObject obj in Tracer.Scene.ObjectSpan)
            if (obj.Material?.MayTransmitShadows == true) return true;
        return false;
    }

    protected internal static MaterialSample Surface(ISdfObject obj, Vector3 point, out Vector3 normal)
    {
        if (obj.Material is { } material)
        {
            var context = obj.GetTextureContext(point);
            normal = context.GeometricNormal;
            MaterialSample sample = material.Sample(context);
            return sample with { Bsdf = sample.Bsdf ?? LambertianBsdf.Instance };
        }
        normal = obj.GetNormal(point);
        return new(Vector3.One, Vector3.Zero, normal);
    }

    protected internal Vector3 Offset(Vector3 point, Vector3 normal, Vector3 direction) =>
        point + normal * (Vector3.Dot(normal, direction) >= 0f ? ShadowBias : -ShadowBias);

    protected static void Positive(float value)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
    }

    protected static void Range(int value, int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, minimum);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, maximum);
    }
}
