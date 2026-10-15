```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9457/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-ITCNNX : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Runtime=.NET 10.0  IterationCount=12  LaunchCount=1  
WarmupCount=5  

```
| Method   | Width | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|--------- |------ |-----------:|----------:|----------:|-----------:|------:|--------:|-------:|----------:|------------:|
| **GammaPow** | **160**   |   **205.7 μs** |   **2.67 μs** |   **1.77 μs** |   **206.4 μs** |  **1.00** |    **0.01** | **0.4883** |   **4.78 KB** |        **1.00** |
|          |       |            |           |           |            |       |         |        |           |             |
| **GammaPow** | **640**   | **2,519.6 μs** | **190.53 μs** | **137.77 μs** | **2,497.5 μs** |  **1.00** |    **0.07** |      **-** |   **4.76 KB** |        **1.00** |
