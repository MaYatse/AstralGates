using BenchmarkDotNet.Running;

if (args is ["--gamma-accuracy"])
{
    AstralGates.Benchmarks.GammaAccuracy.Run();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(AstralGates.Benchmarks.GammaBenchmarks).Assembly).Run(args);
