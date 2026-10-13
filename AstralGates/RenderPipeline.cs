
using SDFEngine.SDF.Light;
using SDFEngine.SDF.Objects;
using SDFEngine.SDF.Utility;
using System.Numerics;

namespace AstralGates;

public class RenderPipeline
{
    public float Time { get; set; }
    public bool IsLock { get; set; } = false;
    static readonly SdfSphere _sphere = new(new(1, 5, 0), 2);
    static readonly SdfBox _box = new(new(-1, 1, 0), new(1));
    readonly SdCompositor sd = new([
        _sphere,
        _box,
        new SdfPlane(new(0, 0, 0), Vector3.Normalize( new(0, 1, 0))),
    ], [
        new PointLight(new(2, 5, -3))
    ]);
    public float Angle { get; set; } = 0;

    static float Sin(float x) => MathF.Sin(x);
    public Vector3 ShDr(Vector2 fragCord, Vector2 resolution)
    {
        if (!IsLock)
        {
            _sphere.Set(new(1, 5 - Sin(Time / (MathF.PI * 180)  /1000) * 10, 0));
            _box.Rotate(new(0, 1, 0), Angle);
            IsLock = true;
        }
        return ShD(fragCord, resolution);
    }
    
    public Vector3 ShD(Vector2 fragCord, Vector2 resolution)
    {
        // Sample pixel centers; screen Y grows downward, world Y grows upward.
        var uv = (fragCord + new Vector2(0.5f) - resolution * 0.5f) / resolution.Y;
        uv.Y = -uv.Y;

        Vector3 origin = new(0, 1, -10);
        Vector3 direction = Vector3.Normalize(new(uv.X, uv.Y, 1));

        Vector3 color = new(sd.DiffusedRender(origin, direction));

        float gamma = 1 / 2.2f;
        return new(MathF.Pow(color.X, gamma), MathF.Pow(color.Y, gamma), MathF.Pow(color.Z, gamma));
    }
}
