//#define PRIKOL
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF;
using HybridSdfEngine.Render;
using System.Numerics;

namespace AstralGates;

public class RenderPipeline
{
    public float Time { get; set; }
    readonly SdfSphere _sphere = new(new(1, 5, 0), 2);
    readonly SdfBox _box = new(new(0, 3, 0), new(0.5f, 1.5f, 0.5f));
    readonly SunLight _sl = new(new (0, -1, 0.5f), new (1), 1);

    public Scene Scene { get; }
    public DiffuseRenderer Renderer { get; }

    public RenderPipeline()
    {

#if PRIKOL
        Scene = new Scene([
            _box,
            new SdfSphere(new(1, 1, 0), 1),
            new SdfSphere(new(-1, 1, 0), 1),
            new SdfSphere(new(0, 4.5f, -.1f), .5f, Vector3.Normalize( new(222, 118, 194))),

            new SdfPlane(new(0, 0, 0), Vector3.Normalize( new(0, 1, 0))),
        ], [
            //new PointLight(new(2, 5, -3), new(1), 10)
            _sl
        ]);
#else
        Scene = new Scene([
            _box,
            _sphere,

            new SdfPlane(new(0, 0, 0), Vector3.Normalize( new(0, 1, 0))),
        ], [
            new PointLight(new(2, 5, -3), new(1), 10)
        ]);
#endif
        Renderer = new DiffuseRenderer(Scene);
    }
    public float Angle { get; set; } = 0;

    static float Sin(float x) => MathF.Sin(x);


    public void UpdateScene(float deltaTime)
    {
        _box.Rotate(new(0, 1, 0), Angle * deltaTime);
        _sphere.Set(new Vector3(5, 0.5f + 2f * Sin(MathF.Tau * Time / 10f), 1));
        Time += deltaTime;
        const float colorPeriod = 1f;
        float green = 0.5f + 0.5f * Sin(MathF.Tau * Time / colorPeriod);
        float red = 1-( 0.5f + 0.5f * Sin(MathF.Tau * Time / colorPeriod));
        float blue =  1f * Sin(MathF.Tau * Time / colorPeriod / 2);
        _sl.Color = new(red, green, blue);
    }

    public Vector3 ShDr(Vector2 fragCord, Vector2 resolution)
    {
        var uv = (fragCord + new Vector2(0.5f) - resolution * 0.5f) / resolution.Y;
        uv.Y = -uv.Y;

        Vector3 origin = new(0, 1, -10);
        Vector3 direction = Vector3.Normalize(new(uv.X, uv.Y, 1));
        Vector3 color = Renderer.RenderRay(origin, direction);

        return color;
    }
}
