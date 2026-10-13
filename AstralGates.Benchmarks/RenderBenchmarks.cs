using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SDFEngine.Render;
using System.Numerics;

namespace AstralGates.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class RenderBenchmarks
{
    private Shader? _shaderLowRes;
    private Shader? _shaderMediumRes;
    private Shader? _shaderHighRes;
    private RenderPipeline? _pipeline;

    [GlobalSetup]
    public void Setup()
    {
        _pipeline = new RenderPipeline();
        _shaderLowRes = new Shader(320, 240, _pipeline.ShDr);
        _shaderMediumRes = new Shader(640, 480, _pipeline.ShDr);
        _shaderHighRes = new Shader(1920, 1080, _pipeline.ShDr);
    }

    [Benchmark]
    public Vector3[] RenderLowRes_Vectors() => _shaderLowRes!.Render();

    [Benchmark]
    public byte[] RenderLowRes_Bytes() => _shaderLowRes!.RenderRgbaToBytes();

    [Benchmark]
    public Vector3[] RenderMediumRes_Vectors() => _shaderMediumRes!.Render();

    [Benchmark]
    public byte[] RenderMediumRes_Bytes() => _shaderMediumRes!.RenderRgbaToBytes();

    [Benchmark]
    public Vector3[] RenderHighRes_Vectors() => _shaderHighRes!.Render();

    [Benchmark]
    public byte[] RenderHighRes_Bytes() => _shaderHighRes!.RenderRgbaToBytes();
}
