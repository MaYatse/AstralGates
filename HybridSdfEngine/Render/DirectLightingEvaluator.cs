using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Render.Environment;
using HybridSdfEngine.SDF.Light;

namespace HybridSdfEngine.Render;

/// <summary>Shared direct lighting and shadow visibility, using the owning renderer's settings.</summary>
internal sealed class DirectLightingEvaluator(RayRendererBase renderer)
{
    private readonly RayRendererBase _renderer = renderer;

    public Vector3 Evaluate(in MaterialSample surface, in BsdfContext context, Vector3 point, bool transparentShadows,
        int maxLightSamples = int.MaxValue, int maxLights = int.MaxValue)
    {
        Vector3 sum = Vector3.Zero;
        int processed = 0;
        foreach (ILight light in _renderer.Tracer.Scene.LightSpan)
        {
            if (processed++ >= maxLights) break;
            int available = light.SampleCount;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(available);
            int count = Math.Min(available, maxLightSamples);
            Vector3 contribution = Vector3.Zero;
            for (int index = 0; index < count; index++)
                contribution += DirectSample(LightSampleAt(light, point, index, count, available),
                    surface, context, point, transparentShadows);
            sum += contribution / count;
        }
        return sum;
    }

    private static LightSample LightSampleAt(ILight light, Vector3 point, int index, int count, int available)
    {
        if (available == 1) return light.Sample(point);
        if (count == available) return light.Sample(point, index);
        if (light is AreaLight area)
        {
            Vector2 position = EnvironmentSampling.Hammersley(index, count);
            position.X += 0.5f / count;
            position.Y = (position.Y + 0.5f / count) % 1;
            return area.SampleAt(point, position);
        }
        return light.Sample(point, (int)((long)index * available / count));
    }

    private Vector3 DirectSample(LightSample light, in MaterialSample surface, in BsdfContext context,
        Vector3 point, bool transparentShadows)
    {
        if (light.Radiance == Vector3.Zero) return Vector3.Zero;
        Vector3 f = surface.Bsdf.Evaluate(surface, context, light.DirectionToLight);
        float cosine = MathF.Abs(Vector3.Dot(BsdfMath.FaceNormal(surface, context), light.DirectionToLight));
        if (f == Vector3.Zero || cosine <= 0f) return Vector3.Zero;
        Vector3 visibility = _renderer.ShadowsEnabled ? Visibility(light, point, context.GeometricNormal, transparentShadows) : Vector3.One;
        return f * light.Radiance * visibility * (cosine * _renderer.DirectLightScale);
    }

    private Vector3 Visibility(LightSample light, Vector3 point, Vector3 normal, bool transparent)
    {
        var ray = new ShadowRay
        {
            Origin = _renderer.Offset(point, normal, light.DirectionToLight),
            Direction = light.DirectionToLight,
            Remaining = _renderer.ShadowMaxDistance,
            Finite = !float.IsPositiveInfinity(light.Distance)
        };
        if (ray.Finite) ray.Target = point + light.DirectionToLight * light.Distance;
        if (!AimAtLight(ref ray)) return Vector3.One;
        if (!transparent)
            return _renderer.Tracer.OccludedNormalized(ray.Origin, ray.Direction, ray.Remaining, _renderer.ShadowMaxSteps) ? Vector3.Zero : Vector3.One;
        return TraceTransmission(ref ray);
    }

    private bool AimAtLight(ref ShadowRay ray)
    {
        if (!ray.Finite) return ray.Remaining > 0f;
        Vector3 offset = ray.Target - ray.Origin;
        float distance = offset.Length();
        if (distance <= _renderer.ShadowBias) return false;
        ray.Direction = offset / distance;
        ray.Remaining = distance - _renderer.ShadowBias;
        return true;
    }

    private Vector3 TraceTransmission(ref ShadowRay ray)
    {
        Vector3 transmission = Vector3.One;
        for (int boundary = 0; boundary < _renderer.MaxShadowIntersections; boundary++)
        {
            var hit = _renderer.Tracer.Trace(ray.Origin, ray.Direction, ray.Remaining, _renderer.ShadowMaxSteps);
            if (hit.StepLimitReached) return Vector3.Zero;
            if (!hit.Hit || hit.Dist >= ray.Remaining) return transmission;
            MaterialSample surface = RayRendererBase.Surface(hit.ClosestObject!, hit.CollisionPoint, out Vector3 boundaryNormal);
            var context = BsdfContext.Create(surface, boundaryNormal, -ray.Direction, _renderer.OutsideIor);
            transmission *= surface.Bsdf.ShadowTransmittance(surface, context);
            if (transmission == Vector3.Zero) return Vector3.Zero;
            ray.Origin = _renderer.Offset(hit.CollisionPoint, boundaryNormal, ray.Direction);
            ray.Remaining -= hit.Dist + _renderer.ShadowBias;
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

}
