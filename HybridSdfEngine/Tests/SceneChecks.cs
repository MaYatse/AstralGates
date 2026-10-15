using System.Numerics;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.SDF.Utility;

internal static class SceneChecks
{
    public static void Run()
    {
        var sphere = new SdfSphere(new(0, 0, 3), 1f);
        var light = new PointLight(Vector3.Zero, Vector3.One, 4f);
        List<ISdfObject> objects = [sphere];
        List<ILight> lights = [light];
        var scene = new Scene(objects, lights);
        objects.Clear();
        lights.Clear();
        Check(scene.Objects.Count == 1 && scene.Lights.Count == 1, "Scene must snapshot input collections");
        Expect<NotSupportedException>(() => ((IList<ISdfObject>)scene.Objects).Clear());
        Expect<NotSupportedException>(() => ((IList<ILight>)scene.Lights).Clear());

        var tracer = new SceneTracer(scene);
        var renderer = new DiffuseRenderer(tracer);
        Near(renderer.RenderRay(Vector3.Zero, Vector3.UnitZ), Vector3.One, "Renderer uses scene lights");
        light.Intensity = 8f;
        Near(renderer.RenderRay(Vector3.Zero, Vector3.UnitZ), new(2), "Light changes remain visible");
        sphere.Move(new(0, 0, 2));
        Check(MathF.Abs(tracer.Trace(Vector3.Zero, Vector3.UnitZ).Dist - 4f) < 1e-5f,
            "Object changes remain visible without rebuilding scene membership");
        Near(renderer.RenderRay(Vector3.Zero, Vector3.UnitZ), new(0.5f), "Renderer and tracer share the same geometry");

        var facade = new SdCompositor(scene);
        Near(facade.DiffusedRender(Vector3.Zero, Vector3.UnitZ), new(0.5f), "Legacy compositor remains usable");
        facade.ShadowsEnabled = false;
        Check(!facade.Renderer.ShadowsEnabled, "Legacy shadow settings must reach the renderer");

        Expect<ArgumentNullException>(() => new Scene(null!));
        Expect<ArgumentException>(() => new Scene([null!]));
        Expect<ArgumentException>(() => new Scene([], [null!]));
        Expect<ArgumentNullException>(() => new SceneTracer(null!));
        Expect<ArgumentNullException>(() => new DiffuseRenderer((SceneTracer)null!));
        Console.WriteLine("PASS: scene membership snapshot, read-only collections, shared mutable objects/lights and compositor compatibility.");
    }

    private static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(float.IsFinite(actual.LengthSquared()) && Vector3.DistanceSquared(actual, expected) < 1e-8f, message);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Expect<TException>(Action action) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new Exception($"Expected {typeof(TException).Name}");
    }
}
