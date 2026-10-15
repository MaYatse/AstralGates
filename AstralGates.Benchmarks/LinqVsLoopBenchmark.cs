using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HybridSdfEngine.SDF.Objects;
using System.Numerics;

namespace AstralGates.Benchmarks;

[SimpleJob(RuntimeMoniker.Net10_0)]
[MemoryDiagnoser]
public class LinqVsLoopBenchmark
{
    private List<ISdfObject> _objects = null!;
    private Vector3 _testPoint = new(1, 2, 3);

    [GlobalSetup]
    public void Setup()
    {
        _objects = [
            new SdfSphere(new Vector3(0, 0, 0), 2f),
            new SdfSphere(new Vector3(5, 0, 0), 1.5f),
            new SdfPlane(new Vector3(0, -5, 0), new Vector3(0, 1, 0))
        ];
    }

    [Benchmark(Baseline = true)]
    public float Linq_Min()
    {
        return _objects.Min(o => o.DistanceTo(_testPoint));
    }

    [Benchmark]
    public float Loop_Foreach()
    {
        float minDist = float.MaxValue;
        foreach (var obj in _objects)
        {
            float dist = obj.DistanceTo(_testPoint);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    [Benchmark]
    public float Loop_For()
    {
        float minDist = float.MaxValue;
        for (int i = 0; i < _objects.Count; i++)
        {
            float dist = _objects[i].DistanceTo(_testPoint);
            if (dist < minDist) minDist = dist;
        }
        return minDist;
    }

    // Симуляция реального использования - много вызовов подряд
    [Benchmark]
    public float Linq_Min_1000Calls()
    {
        float sum = 0;
        for (int i = 0; i < 1000; i++)
            sum += _objects.Min(o => o.DistanceTo(_testPoint));
        return sum;
    }

    [Benchmark]
    public float Loop_1000Calls()
    {
        float sum = 0;
        for (int i = 0; i < 1000; i++)
        {
            float minDist = float.MaxValue;
            foreach (var obj in _objects)
            {
                float dist = obj.DistanceTo(_testPoint);
                if (dist < minDist) minDist = dist;
            }
            sum += minDist;
        }
        return sum;
    }
}
