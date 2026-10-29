using HybridSdfEngine.Materials;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.Textures;
using System.Numerics;

namespace AstralGates;

/// <summary>Content and animation for the sample scene.</summary>
public sealed class DemoScene
{
    private readonly SdfSphere _sphere = CreateSphere();
    private readonly SdfBox _box = CreateBox();
#if PRIKOL
    private readonly SunLight _sun = new(new(0, -1, 0.5f), Vector3.One, 1);
#endif
    public float Time { get; set; }
    public float Angle { get; set; }
    public Scene Scene { get; }

    public DemoScene()
    {
#if PRIKOL
        Scene = new Scene([
            _sphere, _box,
            new SdfSphere(new(1, 1, 0), 1),
            new SdfSphere(new(-1, 1, 0), 1),
            new SdfSphere(new(0, 4.5f, -.1f), .5f)
            {
                Material = new DiffuseMaterial(Vector3.Normalize(new(222, 118, 194)))
            },
            CreateFloor()
        ], [_sun]);
#else
        Scene = new Scene([_box, _sphere, CreateFloor()],
            [new PointLight(new(2, 5, -3), new(1), 10)]);
#endif
    }

    /// <summary>Rebuild the demo's static probes after changes to scene content or lighting.</summary>
    public void BakeEnvironment(RealtimeRenderer renderer, int resolution = 32, int sampleCount = 64)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        if (!ReferenceEquals(renderer.Tracer.Scene, Scene))
            throw new ArgumentException("Renderer must use this demo scene.", nameof(renderer));
        Vector3 position = new(0, 5, -3);
        foreach (ISdfObject receiver in Scene.Objects)
            renderer.BakeEnvironmentFor(receiver, position, resolution, sampleCount);
    }

    private static SdfBox CreateBox()
    {
        return new(new(0, 2, 0), new(0.5f, 2, .5f))
        {
            Material = new DielectricMaterial
            {
                TransmissionTint = new ConstantTexture<Vector3>(new(0.9f, 1f, 1f)),
                IndexOfRefraction = new ConstantTexture<float>(1.5f),
                Roughness = new ConstantTexture<float>(0f)
            }
        };
    }

    private static SdfPlane CreateFloor()
    {
        return new(Vector3.Zero, Vector3.UnitY)
        {
            Material = new DiffuseMaterial(new CheckerTexture<Vector3>(
        new(0.8f), new(0.1f, 0.15f, 0.2f), frequency: 2f, space: TextureSpace.UV))
        };
    }

    private static SdfSphere CreateSphere()
    {
        return new(new(-3, 2, 1), 1)
        {
            Material = new PbrMaterial(new Vector3(0.95f, 0.65f, 0.3f))
            {
                Metallic = new ConstantTexture<float>(1f),
                Roughness = new ConstantTexture<float>(0.15f)
            }
        };
    }

    static float Sin(float x) => MathF.Sin(x);


    public void Update(float deltaTime)
    {
        _box.Rotate(new(0, 1, 0), Angle * deltaTime);
        _sphere.Rotate(new(0, 1, 0), Angle * deltaTime);
        _sphere.Set(new Vector3(1, 3f + 2f * Sin(MathF.Tau * Time / 10f), 0));
        Time += deltaTime;
    }

}
