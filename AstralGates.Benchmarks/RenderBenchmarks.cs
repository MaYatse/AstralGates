using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HybridSdfEngine.Render;
using System.Numerics;

namespace AstralGates.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
[MemoryDiagnoser]
[MedianColumn]
public class RenderBenchmarks
{
    [Params(160, 640)]
    public int Width { get; set; }

    private Shader _shader = null!;
    private Func<Vector2, Vector2, Vector3> _shade = null!;
    private Vector3[] _lookupBuffer = null!;
    private Vector2 _resolution;
    private int _height;

    [GlobalSetup]
    public void Setup()
    {
        _height = Width * 9 / 16;
        var pipeline = new RenderPipeline();
        pipeline.UpdateScene(0f);
        _shade = pipeline.ShDr;
        _resolution = new(Width, _height);
        _shader = new Shader(Width, _height, _shade);
        _lookupBuffer = new Vector3[Width * _height];
        GammaLookup.Warmup();

        // Validate against the real Shader path before measuring performance.
        var expected = _shader.RenderWithGamma();
        var actual = GammaLut();
        for (int i = 0; i < actual.Length; i++)
        {
            var error = Vector3.Abs(expected[i] - actual[i]);
            if (!float.IsFinite(actual[i].X + actual[i].Y + actual[i].Z)
                || MathF.Max(error.X, MathF.Max(error.Y, error.Z)) > 0.001f)
                throw new InvalidOperationException($"Gamma LUT mismatch at pixel {i}.");
        }
    }

    [Benchmark(Baseline = true)]
    public Vector3[] GammaPow() => _shader.RenderWithGamma();

    [Benchmark]
    public Vector3[] NoGamma() => _shader.SimpleRender();

    // Same row-parallel, fused render/correction layout as Shader.RenderWithGamma.
    // The experimental implementation stays in the benchmark project.
    [Benchmark]
    public Vector3[] GammaLut()
    {
        Parallel.For(0, _height, y =>
        {
            for (int x = 0; x < Width; x++)
                _lookupBuffer[y * Width + x] = GammaLookup.DarkSafe(_shade(new(x, y), _resolution));
        });
        return _lookupBuffer;
    }

    [Benchmark]
    public Vector3[] Ssaa2() => _shader.RenderWithGammaSsaa2();

    [Benchmark]
    public Vector3[] Ssaa4() => _shader.RenderWithGammaSsaa4();
}
