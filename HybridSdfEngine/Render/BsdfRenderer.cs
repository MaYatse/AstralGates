using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;

namespace HybridSdfEngine.Render;

/// <summary>Direct light sampling and bounded BSDF path transport, with no per-ray heap allocation.</summary>
public class BsdfRenderer
{
    private float _shadowBias = 0.002f, _shadowMaxDistance = 1000f, _maxDistance = 1000f;
    private float _directLightScale = MathF.PI, _outsideIor = 1f;
    private int _shadowMaxSteps = 256, _maxSteps = 1000, _maxBounces = 6, _sampleCount = 1, _maxShadowIntersections = 32;
    private Vector3 _backgroundColor;

    public SceneTracer Tracer { get; }
    public bool ShadowsEnabled { get; set; } = true;
    public bool TransparentShadowsEnabled { get; set; } = true;
    /// <summary>Enables secondary paths for pure diffuse surfaces. Composite PBR and dielectric BSDFs always use their full sampling distribution.</summary>
    public bool TraceDiffuseBounces { get; set; }
    public uint Seed { get; set; } = 1;

    public float ShadowBias { get => _shadowBias; set { Positive(value); _shadowBias = value; } }
    public float ShadowMaxDistance { get => _shadowMaxDistance; set { Positive(value); _shadowMaxDistance = value; } }
    public int ShadowMaxSteps { get => _shadowMaxSteps; set { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); _shadowMaxSteps = value; } }
    public float MaxDistance { get => _maxDistance; set { Positive(value); _maxDistance = value; } }
    public int MaxSteps { get => _maxSteps; set { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value); _maxSteps = value; } }
    public int MaxBounces { get => _maxBounces; set { Range(value, 0, 32); _maxBounces = value; } }
    public int SampleCount { get => _sampleCount; set { Range(value, 1, 4096); _sampleCount = value; } }
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

    public BsdfRenderer(Scene scene) : this(new SceneTracer(scene)) { }
    public BsdfRenderer(SceneTracer tracer)
    {
        ArgumentNullException.ThrowIfNull(tracer);
        Tracer = tracer;
    }

    public Vector3 RenderRay(Vector3 origin, Vector3 direction)
    {
        float length = direction.Length();
        if (!float.IsFinite(length) || length <= 0f) throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        direction /= length;
        bool transparentShadows = TransparentShadowsEnabled && HasTransmissiveSurfaces();
        var random = new PathRandom(origin, direction, Seed);
        Vector3 sum = Vector3.Zero;
        for (int sample = 0; sample < SampleCount; sample++)
            sum += RenderPath(origin, direction, transparentShadows, ref random);
        return sum / SampleCount;
    }

    private Vector3 RenderPath(Vector3 origin, Vector3 direction, bool transparentShadows, ref PathRandom random)
    {
        Span<float> iors = stackalloc float[34];
        iors[0] = OutsideIor;
        var path = new PathState(origin, direction);
        Vector3 radiance = Vector3.Zero;
        for (int bounce = 0; bounce <= MaxBounces; bounce++)
        {
            var hit = Tracer.Trace(path.Origin, path.Direction, MaxDistance, MaxSteps);
            if (!hit.Hit)
            {
                radiance += path.Throughput * (hit.StepLimitReached ? Vector3.Zero : BackgroundColor);
                break;
            }
            MaterialSample surface = Surface(hit.ClosestObject!, hit.CollisionPoint, out Vector3 normal);
            IBsdf bsdf = surface.Bsdf;
            var context = BoundaryContext(surface, normal, path, iors);
            radiance += path.Throughput * (surface.Emission + DirectLighting(surface, context, hit.CollisionPoint, transparentShadows));
            if (bounce == MaxBounces || !TraceDiffuseBounces && (bsdf.Flags & (BsdfLobes.Glossy | BsdfLobes.Specular)) == 0) break;
            if (!Scatter(surface, context, hit.CollisionPoint, ref path, iors, ref random)) break;
        }
        return radiance;
    }

    private static BsdfContext BoundaryContext(in MaterialSample surface, Vector3 normal, in PathState path, ReadOnlySpan<float> iors)
    {
        bool entering = Vector3.Dot(normal, path.Direction) < 0f;
        float incident = !entering && path.Medium == 0 ? surface.IndexOfRefraction : iors[path.Medium];
        float target = entering ? surface.IndexOfRefraction : iors[Math.Max(0, path.Medium - 1)];
        return new(normal, -path.Direction, incident, target);
    }

    private bool Scatter(in MaterialSample surface, in BsdfContext context, Vector3 point,
        ref PathState path, Span<float> iors, ref PathRandom random)
    {
        BsdfSample scatter = surface.Bsdf.Sample(surface, context, random.Next3());
        if (!scatter.IsValid) return false;
        float cosine = MathF.Abs(Vector3.Dot(BsdfMath.FaceNormal(surface, context), scatter.Direction));
        path.Throughput *= scatter.Value * (cosine / scatter.Pdf);
        if (!BsdfMath.IsFinite(path.Throughput) || path.Throughput == Vector3.Zero) return false;
        if ((scatter.Flags & BsdfLobes.Transmission) != 0)
        {
            if (context.FrontFace) iors[++path.Medium] = context.TransmittedIor;
            else path.Medium = Math.Max(0, path.Medium - 1);
        }
        path.Direction = Vector3.Normalize(scatter.Direction);
        path.Origin = Offset(point, context.GeometricNormal, path.Direction);
        return true;
    }

    private struct PathState(Vector3 origin, Vector3 direction)
    {
        public Vector3 Origin = origin;
        public Vector3 Direction = direction;
        public Vector3 Throughput = Vector3.One;
        public int Medium;
    }

    private Vector3 DirectLighting(in MaterialSample surface, in BsdfContext context, Vector3 point, bool transparentShadows)
    {
        Vector3 sum = Vector3.Zero;
        foreach (ILight light in Tracer.Scene.LightSpan)
        {
            int count = light.SampleCount;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            Vector3 contribution = Vector3.Zero;
            for (int index = 0; index < count; index++)
                contribution += DirectSample(count == 1 ? light.Sample(point) : light.Sample(point, index),
                    surface, context, point, transparentShadows);
            sum += contribution / count;
        }
        return sum;
    }

    private Vector3 DirectSample(LightSample light, in MaterialSample surface, in BsdfContext context,
        Vector3 point, bool transparentShadows)
    {
        if (light.Radiance == Vector3.Zero) return Vector3.Zero;
        Vector3 f = surface.Bsdf.Evaluate(surface, context, light.DirectionToLight);
        float cosine = MathF.Abs(Vector3.Dot(BsdfMath.FaceNormal(surface, context), light.DirectionToLight));
        if (f == Vector3.Zero || cosine <= 0f) return Vector3.Zero;
        Vector3 visibility = ShadowsEnabled ? Visibility(light, point, context.GeometricNormal, transparentShadows) : Vector3.One;
        return f * light.Radiance * visibility * (cosine * DirectLightScale);
    }

    private Vector3 Visibility(LightSample light, Vector3 point, Vector3 normal, bool transparent)
    {
        var ray = new ShadowRay
        {
            Origin = Offset(point, normal, light.DirectionToLight),
            Direction = light.DirectionToLight,
            Remaining = ShadowMaxDistance,
            Finite = !float.IsPositiveInfinity(light.Distance)
        };
        if (ray.Finite) ray.Target = point + light.DirectionToLight * light.Distance;
        if (!AimAtLight(ref ray)) return Vector3.One;
        if (!transparent)
            return Tracer.OccludedNormalized(ray.Origin, ray.Direction, ray.Remaining, ShadowMaxSteps) ? Vector3.Zero : Vector3.One;
        return TraceTransmission(ref ray);
    }

    private bool AimAtLight(ref ShadowRay ray)
    {
        if (!ray.Finite) return ray.Remaining > 0f;
        Vector3 offset = ray.Target - ray.Origin;
        float distance = offset.Length();
        if (distance <= ShadowBias) return false;
        ray.Direction = offset / distance;
        ray.Remaining = distance - ShadowBias;
        return true;
    }

    private Vector3 TraceTransmission(ref ShadowRay ray)
    {
        Vector3 transmission = Vector3.One;
        for (int boundary = 0; boundary < MaxShadowIntersections; boundary++)
        {
            var hit = Tracer.Trace(ray.Origin, ray.Direction, ray.Remaining, ShadowMaxSteps);
            if (hit.StepLimitReached) return Vector3.Zero;
            if (!hit.Hit || hit.Dist >= ray.Remaining) return transmission;
            MaterialSample surface = Surface(hit.ClosestObject!, hit.CollisionPoint, out Vector3 boundaryNormal);
            var context = BsdfContext.Create(surface, boundaryNormal, -ray.Direction, OutsideIor);
            transmission *= surface.Bsdf.ShadowTransmittance(surface, context);
            if (transmission == Vector3.Zero) return Vector3.Zero;
            ray.Origin = Offset(hit.CollisionPoint, boundaryNormal, ray.Direction);
            ray.Remaining -= hit.Dist + ShadowBias;
            if (!AimAtLight(ref ray)) return transmission;
        }
        return Vector3.Zero; // Budget exhaustion must not leak light.
    }

    private struct ShadowRay
    {
        public Vector3 Origin, Direction, Target;
        public float Remaining;
        public bool Finite;
    }

    private bool HasTransmissiveSurfaces()
    {
        foreach (ISdfObject obj in Tracer.Scene.ObjectSpan)
            if (obj.Material?.MayTransmitShadows == true) return true;
        return false;
    }

    private static MaterialSample Surface(ISdfObject obj, Vector3 point, out Vector3 normal)
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

    private Vector3 Offset(Vector3 point, Vector3 normal, Vector3 direction) =>
        point + normal * (Vector3.Dot(normal, direction) >= 0f ? ShadowBias : -ShadowBias);

    private static void Positive(float value)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
    }

    private static void Range(int value, int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, minimum);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, maximum);
    }
}
