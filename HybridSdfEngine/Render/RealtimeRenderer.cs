using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Render.Environment;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.Render;

/// <summary>Deterministic direct PBR, prefiltered IBL and a shared budget of ideal secondary rays.</summary>
public sealed class RealtimeRenderer : RayRendererBase
{
    private int _maxSecondaryRays = 2, _maxLights = 8, _maxLightSamples = 4;
    private float _reflectionRoughnessLimit = 0.05f;
    private IEnvironmentLight _environment = new ConstantEnvironmentLight(Vector3.Zero);
    private readonly Dictionary<ISdfObject, IEnvironmentLight> _objectEnvironments = new(ReferenceEqualityComparer.Instance);

    public IEnvironmentLight EnvironmentLight
    {
        get => _environment;
        set { ArgumentNullException.ThrowIfNull(value); _environment = value; }
    }
    public bool SceneReflectionsEnabled { get; set; } = true;
    public int MaxSecondaryRays { get => _maxSecondaryRays; set { Range(value, 0, 32); _maxSecondaryRays = value; } }
    public int MaxLights { get => _maxLights; set { Range(value, 1, 64); _maxLights = value; } }
    public int MaxLightSamples { get => _maxLightSamples; set { Range(value, 1, 64); _maxLightSamples = value; } }
    public float SceneReflectionRoughnessLimit
    {
        get => _reflectionRoughnessLimit;
        set
        {
            if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
            _reflectionRoughnessLimit = value;
        }
    }

    public RealtimeRenderer(Scene scene) : this(new SceneTracer(scene)) { }
    public RealtimeRenderer(SceneTracer tracer) : base(tracer)
    {
        MaxBounces = 3;
        MaxSteps = 128;
        MaxDistance = 100;
        ShadowMaxDistance = 60;
        ShadowMaxSteps = 64;
        MaxShadowIntersections = 4;
    }

    /// <summary>Bakes a static distant probe using direct lighting only. Call while the scene is not being updated.</summary>
    public void BakeEnvironment(Vector3 position, int resolution = 32, int sampleCount = 64)
    {
        EnvironmentLight = CaptureEnvironment(Tracer, position, resolution, sampleCount);
    }

    /// <summary>Bakes a probe for one receiver, excluding that receiver from capture geometry and shadows.</summary>
    public void BakeEnvironmentFor(ISdfObject receiver, Vector3 position, int resolution = 32, int sampleCount = 64)
    {
        RequireReceiver(receiver);
        var captureScene = new Scene(Tracer.Scene.Objects.Where(obj => !ReferenceEquals(obj, receiver)), Tracer.Scene.Lights);
        var environment = CaptureEnvironment(new SceneTracer(captureScene), position, resolution, sampleCount);
        _objectEnvironments[receiver] = environment;
    }

    /// <summary>Overrides the distant lighting of one scene object. Change only between frames.</summary>
    public void SetEnvironmentFor(ISdfObject receiver, IEnvironmentLight environment)
    {
        RequireReceiver(receiver);
        ArgumentNullException.ThrowIfNull(environment);
        _objectEnvironments[receiver] = environment;
    }

    /// <summary>Returns an object override, or the shared EnvironmentLight when none is configured.</summary>
    public IEnvironmentLight GetEnvironmentFor(ISdfObject receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return _objectEnvironments.TryGetValue(receiver, out var environment) ? environment : EnvironmentLight;
    }

    public bool ClearEnvironmentFor(ISdfObject receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        return _objectEnvironments.Remove(receiver);
    }

    private void RequireReceiver(ISdfObject receiver)
    {
        ArgumentNullException.ThrowIfNull(receiver);
        if (!Tracer.Scene.Objects.Any(obj => ReferenceEquals(obj, receiver)))
            throw new ArgumentException("The environment receiver must belong to this scene.", nameof(receiver));
    }

    private PrefilteredEnvironmentMap CaptureEnvironment(SceneTracer tracer, Vector3 position, int resolution, int sampleCount)
    {
        if (!BsdfMath.IsFinite(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var capture = new BsdfRenderer(tracer)
        {
            MaxBounces = 0, MaxDistance = MaxDistance, MaxSteps = MaxSteps,
            ShadowsEnabled = ShadowsEnabled, TransparentShadowsEnabled = TransparentShadowsEnabled,
            ShadowMaxDistance = ShadowMaxDistance, ShadowMaxSteps = ShadowMaxSteps,
            MaxShadowIntersections = MaxShadowIntersections, ShadowBias = ShadowBias,
            BackgroundColor = BackgroundColor, DirectLightScale = DirectLightScale, OutsideIor = OutsideIor
        };
        var environment = new PrefilteredEnvironmentMap(direction => capture.RenderRay(position, direction), resolution, sampleCount);
        EnvironmentBrdf.Prepare();
        return environment;
    }

    public override Vector3 RenderRay(Vector3 origin, Vector3 direction) => RenderCore(origin, direction, false, out _, out _);

    public override RaySample RenderRaySample(Vector3 origin, Vector3 direction)
    {
        Vector3 color = RenderCore(origin, direction, true, out Vector3 direct, out SurfaceGuide guide);
        return new(color, direct, guide);
    }

    private Vector3 RenderCore(Vector3 origin, Vector3 direction, bool captureGuide, out Vector3 direct, out SurfaceGuide guide)
    {
        float length = direction.Length();
        if (!float.IsFinite(length) || length <= 0) throw new ArgumentException("Ray direction must be finite and nonzero.", nameof(direction));
        Span<float> media = stackalloc float[34];
        media[0] = OutsideIor;
        int budget = MaxSecondaryRays;
        var ray = new Ray(origin, direction / length, 0, 0, null);
        bool transparentShadows = TransparentShadowsEnabled && HasTransmissiveSurfaces();
        return Trace(ray, media, ref budget, transparentShadows, captureGuide, out direct, out guide);
    }

    private Vector3 Trace(in Ray ray, ReadOnlySpan<float> media, ref int budget, bool transparentShadows,
        bool captureGuide, out Vector3 direct, out SurfaceGuide guide)
    {
        direct = Vector3.Zero;
        guide = default;
        MarchResult hit = Tracer.Trace(ray.Origin, ray.Direction, MaxDistance, MaxSteps);
        if (!hit.Hit)
        {
            if (hit.StepLimitReached || ray.Medium > 0) return Vector3.Zero;
            return ray.MissEnvironment?.SampleSpecular(ray.Direction, 0) ?? BackgroundColor;
        }
        MaterialSample surface = Surface(hit.ClosestObject!, hit.CollisionPoint, out Vector3 geometricNormal);
        var context = Context(surface, geometricNormal, ray, media);
        Vector3 local = surface.Emission + DirectLighting(surface, context, hit.CollisionPoint, transparentShadows, MaxLightSamples, MaxLights);
        if (captureGuide)
        {
            direct = local;
            // Realtime produces no stochastic secondary transport for Spatial to denoise.
            guide = new(hit.Dist, surface.ShadingNormal, surface.Albedo, hit.ClosestObject, false);
        }
        var boundary = new Boundary(hit.CollisionPoint, surface, context, GetEnvironmentFor(hit.ClosestObject!));
        return local + ((surface.Bsdf.Flags & BsdfLobes.Transmission) != 0
            ? Glass(boundary, ray, media, ref budget, transparentShadows)
            : Opaque(boundary, ray, media, ref budget, transparentShadows));
    }

    private Vector3 Opaque(in Boundary boundary, in Ray ray, ReadOnlySpan<float> media, ref int budget, bool transparentShadows)
    {
        if (!boundary.Context.FrontFace) return Vector3.Zero;
        MaterialSample surface = boundary.Surface;
        Vector3 normal = BsdfMath.FaceNormal(surface, boundary.Context);
        BsdfLobes flags = surface.Bsdf.Flags;
        if ((flags & (BsdfLobes.Glossy | BsdfLobes.Specular)) == 0)
            return surface.Albedo * boundary.Environment.SampleDiffuse(normal);

        float nv = MathF.Max(0, Vector3.Dot(normal, boundary.Context.OutgoingDirection));
        float ratio = (surface.IndexOfRefraction - boundary.Context.IncidentIor) / (surface.IndexOfRefraction + boundary.Context.IncidentIor);
        Vector3 f0 = Vector3.Lerp(new(ratio * ratio), surface.Albedo, surface.Metallic);
        Vector3 fresnel = BsdfMath.Schlick(f0, nv);
        Vector3 diffuse = surface.Metallic < 1
            ? surface.Albedo * (1 - surface.Metallic) * (Vector3.One - fresnel) * boundary.Environment.SampleDiffuse(normal)
            : Vector3.Zero;
        Vector3 reflected = ReflectionDirection(ray.Direction, normal, boundary.Context.GeometricNormal);
        Vector3 specular;
        if (SceneReflectionsEnabled && surface.Roughness <= SceneReflectionRoughnessLimit && CanTrace(ray, budget))
        {
            var child = NextRay(boundary, ray, reflected, ray.Medium, true);
            specular = TraceChild(child, media, ref budget, transparentShadows) * fresnel;
        }
        else
            specular = boundary.Environment.SampleSpecular(reflected, surface.Roughness) * EnvironmentBrdf.Evaluate(f0, nv, surface.Roughness);
        return diffuse + specular;
    }

    private Vector3 Glass(in Boundary boundary, in Ray ray, ReadOnlySpan<float> media, ref int budget, bool transparentShadows)
    {
        Vector3 normal = BsdfMath.FaceNormal(boundary.Surface, boundary.Context);
        float eta = boundary.Context.IncidentIor / boundary.Context.TransmittedIor;
        bool refracted = BsdfMath.Refract(boundary.Context.OutgoingDirection, normal, eta, out Vector3 transmission);
        if (refracted && BsdfMath.SameSide(boundary.Context, transmission))
        {
            normal = boundary.Context.FrontFace ? boundary.Context.GeometricNormal : -boundary.Context.GeometricNormal;
            refracted = BsdfMath.Refract(boundary.Context.OutgoingDirection, normal, eta, out transmission);
        }
        float fresnel = refracted
            ? BsdfMath.Fresnel(Vector3.Dot(normal, boundary.Context.OutgoingDirection), boundary.Context.IncidentIor, boundary.Context.TransmittedIor)
            : 1;
        Vector3 reflection = ReflectionDirection(ray.Direction, normal, boundary.Context.GeometricNormal);
        Vector3 reflectedColor, transmittedColor;
        // Both branches have Fresnel weights. The stronger branch gets the shared ray budget first.
        if (fresnel >= 0.5f)
        {
            reflectedColor = GlassReflection(boundary, ray, reflection, fresnel, media, ref budget, transparentShadows);
            transmittedColor = GlassTransmission(boundary, ray, transmission, fresnel, media, ref budget, transparentShadows);
        }
        else
        {
            transmittedColor = GlassTransmission(boundary, ray, transmission, fresnel, media, ref budget, transparentShadows);
            reflectedColor = GlassReflection(boundary, ray, reflection, fresnel, media, ref budget, transparentShadows);
        }
        return reflectedColor + transmittedColor;
    }

    private Vector3 GlassReflection(in Boundary boundary, in Ray ray, Vector3 direction, float fresnel,
        ReadOnlySpan<float> media, ref int budget, bool transparentShadows)
    {
        if (fresnel <= 0) return Vector3.Zero;
        if (!SceneReflectionsEnabled || boundary.Surface.Roughness > SceneReflectionRoughnessLimit || !CanTrace(ray, budget))
            return boundary.Environment.SampleSpecular(direction, boundary.Surface.Roughness) * fresnel;
        return TraceChild(NextRay(boundary, ray, direction, ray.Medium, true), media, ref budget, transparentShadows) * fresnel;
    }

    private Vector3 GlassTransmission(in Boundary boundary, in Ray ray, Vector3 direction, float fresnel,
        ReadOnlySpan<float> media, ref int budget, bool transparentShadows)
    {
        if (fresnel >= 1 || !CanTrace(ray, budget)) return Vector3.Zero;
        Span<float> transmittedMedia = stackalloc float[34];
        media.CopyTo(transmittedMedia);
        int medium = boundary.Context.FrontFace ? ray.Medium + 1 : Math.Max(0, ray.Medium - 1);
        if (boundary.Context.FrontFace) transmittedMedia[medium] = boundary.Context.TransmittedIor;
        float eta = boundary.Context.IncidentIor / boundary.Context.TransmittedIor;
        Vector3 weight = boundary.Surface.Albedo * ((1 - fresnel) * eta * eta);
        return weight * TraceChild(NextRay(boundary, ray, direction, medium, false), transmittedMedia, ref budget, transparentShadows);
    }

    private bool CanTrace(in Ray ray, int budget) => ray.Depth < MaxBounces && budget > 0;

    private Vector3 TraceChild(in Ray ray, ReadOnlySpan<float> media, ref int budget, bool transparentShadows)
    {
        budget--;
        return Trace(ray, media, ref budget, transparentShadows, false, out _, out _);
    }

    private Ray NextRay(in Boundary boundary, in Ray parent, Vector3 direction, int medium, bool environmentOnMiss) =>
        new(Offset(boundary.Point, boundary.Context.GeometricNormal, direction), Vector3.Normalize(direction), parent.Depth + 1, medium,
            environmentOnMiss ? boundary.Environment : null);

    private static Vector3 ReflectionDirection(Vector3 incoming, Vector3 shadingNormal, Vector3 geometricNormal)
    {
        Vector3 reflected = Vector3.Reflect(incoming, shadingNormal);
        return Vector3.Dot(reflected, geometricNormal) * Vector3.Dot(-incoming, geometricNormal) > 0
            ? reflected : Vector3.Reflect(incoming, geometricNormal);
    }

    private static BsdfContext Context(in MaterialSample surface, Vector3 normal, in Ray ray, ReadOnlySpan<float> media)
    {
        bool entering = Vector3.Dot(normal, ray.Direction) < 0;
        float incident = !entering && ray.Medium == 0 ? surface.IndexOfRefraction : media[ray.Medium];
        float target = entering ? surface.IndexOfRefraction : media[Math.Max(0, ray.Medium - 1)];
        return new(normal, -ray.Direction, incident, target);
    }

    private readonly record struct Ray(Vector3 Origin, Vector3 Direction, int Depth, int Medium, IEnvironmentLight? MissEnvironment);
    private readonly record struct Boundary(Vector3 Point, MaterialSample Surface, BsdfContext Context, IEnvironmentLight Environment);
}
