using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.Render.Environment;

namespace HybridSdfEngine.Render;

/// <summary>Direct light sampling and bounded BSDF path transport, with no per-ray heap allocation.</summary>
public class BsdfRenderer : RayRendererBase
{
    private int _sampleCount = 1;
    /// <summary>Enables secondary paths for pure diffuse surfaces.</summary>
    public bool TraceDiffuseBounces { get; set; }
    public uint Seed { get; set; } = 1;
    public int SampleCount { get => _sampleCount; set { Range(value, 1, 4096); _sampleCount = value; } }

    public BsdfRenderer(Scene scene) : this(new SceneTracer(scene)) { }
    public BsdfRenderer(SceneTracer tracer) : base(tracer) { }

    public override Vector3 RenderRay(Vector3 origin, Vector3 direction) =>
        RenderRayCore(origin, direction, false, out _, out _);

    /// <summary>Returns the same linear color as RenderRay, plus primary-hit data without tracing another ray.</summary>
    public override RaySample RenderRaySample(Vector3 origin, Vector3 direction)
    {
        Vector3 color = RenderRayCore(origin, direction, true, out Vector3 direct, out SurfaceGuide guide);
        return new(color, direct, guide);
    }

    private Vector3 RenderRayCore(Vector3 origin, Vector3 direction, bool captureGuide,
        out Vector3 direct, out SurfaceGuide guide)
    {
        float length = direction.Length();
        if (!float.IsFinite(length) || length <= 0f) throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        direction /= length;
        bool transparentShadows = TransparentShadowsEnabled && HasTransmissiveSurfaces();
        var random = new PathRandom(origin, direction, Seed);
        Vector3 sum = Vector3.Zero;
        direct = Vector3.Zero;
        guide = default;
        for (int sample = 0; sample < SampleCount; sample++)
        {
            sum += RenderPath(origin, direction, transparentShadows, captureGuide, ref random, out Vector3 sampleDirect, out SurfaceGuide sampleGuide);
            if (!captureGuide) continue;
            direct += sampleDirect;
            if (sample == 0) guide = sampleGuide;
        }
        if (captureGuide) direct /= SampleCount;
        return sum / SampleCount;
    }

    private Vector3 RenderPath(Vector3 origin, Vector3 direction, bool transparentShadows, bool captureGuide,
        ref PathRandom random, out Vector3 direct, out SurfaceGuide guide)
    {
        Span<float> iors = stackalloc float[34];
        iors[0] = OutsideIor;
        var path = new PathState(origin, direction);
        Vector3 radiance = Vector3.Zero;
        direct = Vector3.Zero;
        guide = default;
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
            Vector3 localRadiance = surface.Emission + DirectLighting(surface, context, hit.CollisionPoint, transparentShadows);
            radiance += path.Throughput * localRadiance;
            if (bounce == 0 && captureGuide)
            {
                direct = localRadiance;
                bool secondary = MaxBounces > 0 && (TraceDiffuseBounces || (bsdf.Flags & (BsdfLobes.Glossy | BsdfLobes.Specular)) != 0);
                guide = new(hit.Dist, surface.ShadingNormal, surface.Albedo, hit.ClosestObject, secondary);
            }
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

}
