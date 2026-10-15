using System.Numerics;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;

namespace HybridSdfEngine.SDF.Utility;

/// <summary>Compatibility facade. New callers can use Scene, SceneTracer and DiffuseRenderer directly.</summary>
public class SdCompositor
{
    public Scene Scene => Tracer.Scene;
    public SceneTracer Tracer { get; }
    public DiffuseRenderer Renderer { get; }

    public SdCompositor(List<ISdfObject> sdfObjects, List<ILight>? lights = null)
        : this(new Scene(sdfObjects, lights)) { }

    public SdCompositor(Scene scene)
    {
        Tracer = new SceneTracer(scene);
        Renderer = new DiffuseRenderer(Tracer);
    }

    public bool ShadowsEnabled { get => Renderer.ShadowsEnabled; set => Renderer.ShadowsEnabled = value; }
    public float ShadowBias { get => Renderer.ShadowBias; set => Renderer.ShadowBias = value; }
    public float ShadowMaxDistance { get => Renderer.ShadowMaxDistance; set => Renderer.ShadowMaxDistance = value; }
    public int ShadowMaxSteps { get => Renderer.ShadowMaxSteps; set => Renderer.ShadowMaxSteps = value; }

    public float Map(Vector3 point) => Tracer.Map(point);
    public ISdfObject? GetClosestObject(Vector3 point) => Tracer.GetClosestObject(point);
    public MarchResult Trace(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int maxSteps = 1000) =>
        Tracer.Trace(origin, direction, maxDistance, maxSteps);
    public MarchResult RayMarch(Vector3 origin, Vector3 direction) => Tracer.Trace(origin, direction);
    public bool Occluded(Vector3 origin, Vector3 direction, float maxDistance = 1000f, int maxSteps = 256) =>
        Tracer.Occluded(origin, direction, maxDistance, maxSteps);
    public Vector3 DiffusedRender(Vector3 origin, Vector3 direction) => Renderer.RenderRay(origin, direction);
}
