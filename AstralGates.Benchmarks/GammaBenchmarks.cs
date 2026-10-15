using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HybridSdfEngine.Render;
using System.Numerics;

namespace AstralGates.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, iterationCount: 12)]
[MemoryDiagnoser]
[MedianColumn]
public class GammaBenchmarks
{
    [Params("Scene", "Gradient")]
    public string Input { get; set; } = "Scene";

    private Vector3[] _input = null!;
    private Vector3[] _output = null!;

    [GlobalSetup]
    public void Setup()
    {
        _input = CreateInput(Input);
        _output = new Vector3[_input.Length];
        GammaLookup.Warmup();
    }

    internal static Vector3[] CreateInput(string input)
    {
        const int width = 320, height = 180;
        if (input == "Scene")
        {
            var pipeline = new RenderPipeline();
            pipeline.UpdateScene(0f);
            return new Shader(width, height, pipeline.ShDr).SimpleRender();
        }

        var values = new Vector3[width * height];
        var random = new Random(42);
        for (int i = 0; i < values.Length; i++)
        {
            // Uniform, dark-biased and very dark channels, deterministically shuffled.
            float x = random.NextSingle(), y = random.NextSingle(), z = random.NextSingle();
            values[i] = new(x, y * y, z * 0.001f);
        }
        return values;
    }

    [Benchmark(Baseline = true)]
    public Vector3[] Pow()
    {
        for (int i = 0; i < _input.Length; i++)
            _output[i] = GammaLookup.Pow(_input[i]);
        return _output;
    }

    [Benchmark]
    public Vector3[] LutLinear()
    {
        for (int i = 0; i < _input.Length; i++)
            _output[i] = GammaLookup.Linear(_input[i]);
        return _output;
    }

    [Benchmark]
    public Vector3[] LutDarkSafe()
    {
        for (int i = 0; i < _input.Length; i++)
            _output[i] = GammaLookup.DarkSafe(_input[i]);
        return _output;
    }
}
