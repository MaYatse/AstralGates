using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF;
using HybridSdfEngine.Render;
using System.Numerics;

namespace AstralGates.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class SdfBenchmarks
{
    private SdfSphere? _sphere;
    private SdfPlane? _plane;
    private DiffuseRenderer? _renderer;
    private Vector3 _testOrigin;
    private Vector3 _testDirection;

    [GlobalSetup]
    public void Setup()
    {
        _sphere = new SdfSphere(new Vector3(0, 0, 0), 2f);
        _plane = new SdfPlane(new Vector3(0, -5, 0), new Vector3(0, 1, 0));
        _renderer = new DiffuseRenderer(new Scene(
            [_sphere, _plane],
            [new PointLight(new Vector3(2, 5, -3), Vector3.One, 1f)]
        ));
        _testOrigin = new Vector3(0, 1, -10);
        _testDirection = Vector3.Normalize(new Vector3(0, 0, 1));
    }

    [Benchmark]
    public float SphereDistance() => _sphere!.DistanceTo(_testOrigin);

    [Benchmark]
    public float PlaneDistance() => _plane!.DistanceTo(_testOrigin);

    [Benchmark]
    public Vector3 DiffuseRender() => _renderer!.RenderRay(_testOrigin, _testDirection);

    [Benchmark]
    public void DiffuseRender_1000Rays()
    {
        for (int i = 0; i < 1000; i++)
        {
            var dir = Vector3.Normalize(new Vector3(
                (i % 32 - 16) / 16f,
                (i / 32 - 16) / 16f,
                1
            ));
            _renderer!.RenderRay(_testOrigin, dir);
        }
    }
}
