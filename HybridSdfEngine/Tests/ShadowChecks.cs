using System.Numerics;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.Render;

internal static class ShadowChecks
{
    public static void Run()
    {
        CheckOcclusion();
        CheckFiniteLights();
        CheckSurfaceBias();
        CheckSun();
        CheckArea();
        CheckSkippedSamples();
        Console.WriteLine("PASS: analytic/SDF shadows, ray bounds, self-shadow bias, multiple lights, area penumbra, early exit and conservative step limits.");
    }

    private static void CheckOcclusion()
    {
        var sphere = new SdfSphere(new(0, 1, 0), 0.25f);
        var scene = new SceneTracer(new Scene([sphere]));
        Check(scene.Occluded(Vector3.Zero, new(0, 5, 0), 2f), "Normalize occlusion direction");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0.5f), "Occluder outside ray segment");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0.75f), "Exclude exact endpoint");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0f), "Empty ray segment");
        Check(!scene.Occluded(Vector3.Zero, -Vector3.UnitY, 2f), "Occluder behind origin");

        scene = new SceneTracer(new Scene([sphere, new ShadowProbe(throwOnShadow: true)]));
        Check(scene.Occluded(Vector3.Zero, Vector3.UnitY, 2f), "Stop at first analytic blocker");
        scene = new SceneTracer(new Scene([new DistanceOnly(sphere.DistanceTo)]));
        Check(scene.Occluded(Vector3.Zero, Vector3.UnitY, 2f), "SDF-only blocker");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0.5f), "SDF outside segment");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0.75f), "SDF exact endpoint");
        scene = new SceneTracer(new Scene([new DistanceOnly(_ => 0.01f)]));
        Check(scene.Occluded(Vector3.Zero, Vector3.UnitY, 2f, maxSteps: 1), "Budget exhaustion must not leak light");
        Check(!scene.Occluded(Vector3.Zero, Vector3.UnitY, 0.005f, maxSteps: 1), "Proven clearance before budget exhaustion");

        Invalid(() => scene.Occluded(Vector3.Zero, Vector3.Zero));
        Invalid(() => scene.Occluded(Vector3.Zero, Vector3.UnitY, float.PositiveInfinity));
        Invalid(() => scene.Occluded(Vector3.Zero, Vector3.UnitY, maxSteps: 0));
        var renderer = new DiffuseRenderer(scene);
        Invalid(() => renderer.ShadowBias = 0f);
        Invalid(() => renderer.ShadowBias = float.NaN);
        Invalid(() => renderer.ShadowMaxDistance = float.PositiveInfinity);
        Invalid(() => renderer.ShadowMaxSteps = 0);
    }

    private static void CheckFiniteLights()
    {
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var sphere = new SdfSphere(new(0, 1, 0), 0.25f);
        ILight[] lights = [
            new PointLight(new(0, 2, 0), Vector3.One, 4f),
            new SpotLight(new(0, 2, 0), -Vector3.UnitY, Vector3.One, 4f)
        ];
        foreach (ILight light in lights)
        {
            Near(Render(new DiffuseRenderer(new Scene([plane], [light]))), Vector3.One, "Unoccluded floor must not shadow itself");
            var blocked = new DiffuseRenderer(new Scene([plane, sphere], [light]));
            Near(Render(blocked), Vector3.Zero, "Blocked finite light");
            blocked.ShadowsEnabled = false;
            Near(Render(blocked), Vector3.One, "Disable shadows without changing lighting");
            blocked.ShadowsEnabled = true;
            blocked.ShadowMaxDistance = 0.1f;
            Near(Render(blocked), Vector3.Zero, "Directional range must not truncate finite-light shadows");
            Near(Render(new DiffuseRenderer(new Scene([plane, new SdfSphere(new(0, 3, 0), 0.25f)], [light]))), Vector3.One,
                "Object behind light must not cast a shadow");
            Near(Render(new DiffuseRenderer(new Scene([plane, new SdfPlane(new(0, 2, 0), -Vector3.UnitY)], [light]))), Vector3.One,
                "Geometry at light endpoint must not block it");
            Near(Render(new DiffuseRenderer(new Scene([plane, new DistanceOnly(sphere.DistanceTo)], [light]))), Vector3.Zero,
                "SDF-only object casts a shadow");
        }

        var red = new PointLight(new(0, 2, 0), Vector3.UnitX, 4f);
        var blue = new PointLight(new(2, 2, 0), Vector3.UnitZ, 8f);
        Near(Render(new DiffuseRenderer(new Scene([plane, sphere], [red, blue]))), new(0, 0, MathF.Sqrt(0.5f)),
            "Blocking one source must preserve another source's contribution");
    }

    private static void CheckSun()
    {
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var sun = new SunLight(-Vector3.UnitY, Vector3.One, 1f);
        Near(Render(new DiffuseRenderer(new Scene([plane], [sun]))), Vector3.One, "Unoccluded sun");
        var scene = new DiffuseRenderer(new Scene([plane, new SdfSphere(new(0, 4, 0), 0.25f)], [sun]));
        scene.ShadowMaxDistance = 2f;
        Near(Render(scene), Vector3.One, "Sun occluder beyond configured range");
        scene.ShadowMaxDistance = 10f;
        Near(Render(scene), Vector3.Zero, "Sun occluder within configured range");
    }

    private static void CheckSurfaceBias()
    {
        var sphere = new SdfSphere(Vector3.Zero, 1f);
        ISdfObject[] receivers = [sphere, new SdfBox(Vector3.Zero, Vector3.One), new DistanceOnly(sphere.DistanceTo)];
        var point = new PointLight(new(0, 3, 0), Vector3.One, 4f);
        foreach (ISdfObject receiver in receivers)
        {
            var scene = new DiffuseRenderer(new Scene([receiver], [point]));
            Near(scene.RenderRay(new(0, 3, 0), -Vector3.UnitY), Vector3.One,
                "Exposed analytic and SDF surfaces must not shadow themselves");
        }
    }

    private static void CheckArea()
    {
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var area = new AreaLight(new(0, 2, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f),
            new(2, 2), Vector3.One, 1f, 2);
        Vector3 clear = Render(new DiffuseRenderer(new Scene([plane], [area])));
        Check(clear.X > 0f, "Panel must illuminate the floor");
        // Cover only the negative-X half of the symmetric sample grid.
        var halfBlocker = new SdfBox(new(-0.5f, 1, 0), new(0.45f, 0.05f, 2));
        Near(Render(new DiffuseRenderer(new Scene([plane, halfBlocker], [area]))), clear / 2f,
            "Average all panel samples, including blocked samples");
        Near(Render(new DiffuseRenderer(new Scene([plane, new SdfSphere(new(0, 1, 0), 0.45f)], [area]))), Vector3.Zero,
            "Fully blocked panel");
        Near(Render(new DiffuseRenderer(new Scene([plane, new SdfBox(new(0, 3, 0), new(2, 0.1f, 2))], [area]))), clear,
            "Geometry behind panel must not block samples");
    }

    private static void CheckSkippedSamples()
    {
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var probe = new ShadowProbe(throwOnShadow: true);
        Near(Render(new DiffuseRenderer(new Scene([plane, probe], [new PointLight(new(0, 2, 0), Vector3.Zero, 1)]))), Vector3.Zero,
            "Zero-radiance samples must skip shadow tracing");
        Near(Render(new DiffuseRenderer(new Scene([plane, probe], [new SunLight(Vector3.UnitY, Vector3.One, 1)]))), Vector3.Zero,
            "Back-facing samples must skip shadow tracing");
    }

    private static Vector3 Render(DiffuseRenderer scene) =>
        scene.RenderRay(new(0, 0.25f, 0), -Vector3.UnitY);

    private static void Near(Vector3 actual, Vector3 expected, string message)
    {
        Check(float.IsFinite(actual.LengthSquared()) && Vector3.DistanceSquared(actual, expected) < 1e-8f,
            $"{message}: expected {expected}, got {actual}");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid shadow argument was accepted");
    }

    private sealed class DistanceOnly(Func<Vector3, float> distance) : ISdfObject
    {
        public float DistanceTo(Vector3 point) => distance(point);
        public void Move(Vector3 axis) => throw new NotSupportedException();
        public void Set(Vector3 position) => throw new NotSupportedException();
        public void Rotate(Vector3 axis, float angle) => throw new NotSupportedException();
    }

    private sealed class ShadowProbe(bool throwOnShadow) : IRayIntersectable
    {
        public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
        {
            if (throwOnShadow && direction.Y > 0f) throw new Exception("Unexpected shadow query");
            distance = 0f;
            return false;
        }
        public float DistanceTo(Vector3 point) => throw new Exception("Analytic blockers must not use DistanceTo");
        public void Move(Vector3 axis) => throw new NotSupportedException();
        public void Set(Vector3 position) => throw new NotSupportedException();
        public void Rotate(Vector3 axis, float angle) => throw new NotSupportedException();
    }
}
