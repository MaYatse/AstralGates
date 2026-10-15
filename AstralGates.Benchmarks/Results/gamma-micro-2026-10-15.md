```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-ITCNNX : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=12  LaunchCount=1  
WarmupCount=5  

```
| Method      | Input    | Mean       | Error    | StdDev   | Median     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------ |--------- |-----------:|---------:|---------:|-----------:|------:|--------:|----------:|------------:|
| **Pow**         | **Gradient** | **1,275.9 μs** | **34.87 μs** | **27.22 μs** | **1,268.0 μs** |  **1.00** |    **0.03** |         **-** |          **NA** |
| LutLinear   | Gradient |   317.7 μs |  1.28 μs |  0.85 μs |   317.6 μs |  0.25 |    0.01 |         - |          NA |
| LutDarkSafe | Gradient |   623.2 μs |  3.40 μs |  2.46 μs |   623.0 μs |  0.49 |    0.01 |         - |          NA |
|             |          |            |          |          |            |       |         |           |             |
| **Pow**         | **Scene**    |   **903.3 μs** |  **6.09 μs** |  **4.03 μs** |   **902.3 μs** |  **1.00** |    **0.01** |         **-** |          **NA** |
| LutLinear   | Scene    |   314.8 μs |  0.57 μs |  0.38 μs |   314.7 μs |  0.35 |    0.00 |         - |          NA |
| LutDarkSafe | Scene    |   365.3 μs |  4.52 μs |  3.53 μs |   364.5 μs |  0.40 |    0.00 |         - |          NA |
