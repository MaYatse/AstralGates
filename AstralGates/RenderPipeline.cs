//#define PRIKOL
using HybridSdfEngine.Materials;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.Textures;
using System.Numerics;

namespace AstralGates;

public class RenderPipeline
{
    public float Time { get; set; }
    readonly SdfSphere _sphere = CreateSphere();
    readonly SdfBox _box = CreateBox();
    readonly SunLight _sl = new(new (0, -1, 0.5f), new (1), 1);

    public Scene Scene { get; }
    public BsdfRenderer Renderer { get; }
    public Camera Camera { get; } = new() { Position = new(0, 1, -10) };

    public RenderPipeline()
    {

#if PRIKOL
        Scene = new Scene([
            _sphere,
            _box,
            new SdfSphere(new(1, 1, 0), 1),
            new SdfSphere(new(-1, 1, 0), 1),
            new SdfSphere(new(0, 4.5f, -.1f), .5f)
            {
                Material = new DiffuseMaterial(Vector3.Normalize(new(222, 118, 194)))
            },

            CreateFloor(),
        ], [
            //new PointLight(new(2, 5, -3), new(1), 10)
            _sl
        ]);
#else
        Scene = new Scene([
            _box,
            _sphere,
            CreateFloor(),
        ], [
            new PointLight(new(2, 5, -3), new(1), 10)
            //_sl
        ]);
#endif
        Renderer = new BsdfRenderer(Scene);
    }
    public float Angle { get; set; } = 0;

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


    public void UpdateScene(float deltaTime)
    {
        _box.Rotate(new(0, 1, 0), Angle * deltaTime);
        _sphere.Rotate(new(0, 1, 0), Angle * deltaTime);
        _sphere.Set(new Vector3(1, 3f + 2f * Sin(MathF.Tau * Time / 10f), 0));
        Time += deltaTime;
        const float colorPeriod = 1f;
        float green = 0.5f + 0.5f * Sin(MathF.Tau * Time / colorPeriod);
        float red = 1-( 0.5f + 0.5f * Sin(MathF.Tau * Time / colorPeriod));
        float blue =  1f * Sin(MathF.Tau * Time / colorPeriod / 2);
        //_sl.Color = new(red, green, blue);
    }

    public Vector3 ShDr(Vector2 fragCord, Vector2 resolution)
        => Renderer.RenderRay(Camera.Position, GetRayDirection(fragCord, resolution));

    public RaySample ShDrSample(Vector2 fragCord, Vector2 resolution)
        => Renderer.RenderRaySample(Camera.Position, GetRayDirection(fragCord, resolution));

    private Vector3 GetRayDirection(Vector2 fragCord, Vector2 resolution)
    {
        var uv = (fragCord + new Vector2(0.5f) - resolution * 0.5f) / resolution.Y;
        uv.Y = -uv.Y;

        return Camera.GetRayDirection(uv);
    }
}
