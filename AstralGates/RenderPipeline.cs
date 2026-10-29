using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using System.Numerics;

namespace AstralGates;

/// <summary>Connects a camera and scene to the available ray renderers.</summary>
public class RenderPipeline
{
    public DemoScene? Demo { get; }
    public Scene Scene { get; }
    public BsdfRenderer Renderer { get; }
    public RealtimeRenderer RealtimeRenderer { get; }
    public RenderMode Mode { get; set; } = RenderMode.Bsdf;
    public IRayRenderer ActiveRenderer => Mode == RenderMode.Realtime ? RealtimeRenderer : Renderer;
    public Camera Camera { get; }
    public float Time { get => RequireDemo().Time; set => RequireDemo().Time = value; }
    public float Angle { get => RequireDemo().Angle; set => RequireDemo().Angle = value; }

    public RenderPipeline() : this(new DemoScene()) { }
    public RenderPipeline(DemoScene demo) : this(demo?.Scene ?? throw new ArgumentNullException(nameof(demo)),
        new Camera { Position = new(0, 1, -10) })
    {
        Demo = demo;
    }

    public RenderPipeline(Scene scene, Camera camera)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Camera = camera ?? throw new ArgumentNullException(nameof(camera));
        Renderer = new BsdfRenderer(Scene);
        RealtimeRenderer = new RealtimeRenderer(Renderer.Tracer);
    }

    /// <summary>Rebuild static probes after scene content or lighting changes, between frames.</summary>
    public void BakeRealtimeEnvironment(int resolution = 32, int sampleCount = 64)
        => RequireDemo().BakeEnvironment(RealtimeRenderer, resolution, sampleCount);

    public void UpdateScene(float deltaTime) => RequireDemo().Update(deltaTime);

    private DemoScene RequireDemo() => Demo ?? throw new InvalidOperationException("This pipeline has no demo scene.");

    public Vector3 ShDr(Vector2 fragCord, Vector2 resolution)
        => ActiveRenderer.RenderRay(Camera.Position, GetRayDirection(fragCord, resolution));

    public RaySample ShDrSample(Vector2 fragCord, Vector2 resolution)
        => ActiveRenderer.RenderRaySample(Camera.Position, GetRayDirection(fragCord, resolution));

    private Vector3 GetRayDirection(Vector2 fragCord, Vector2 resolution)
    {
        var uv = (fragCord + new Vector2(0.5f) - resolution * 0.5f) / resolution.Y;
        uv.Y = -uv.Y;

        return Camera.GetRayDirection(uv);
    }
}
