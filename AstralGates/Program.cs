using LX;
using AstralGates;
using HybridSdfEngine.Render;
using System.Diagnostics;
using System.Numerics;

//App.OnRun += static () => new LXRenderer();
//App.Run();
await new ConsoleRenderer().Start();


RenderPipeline RenderPipeline = new();

var shader = new Shader(1920, 1080, RenderPipeline.ShDr);

var stopWatch = Stopwatch.StartNew();
//shader.RenderRgbaToBytes();
//stopWatch.Stop();
//Console.WriteLine($"Render bytes: {stopWatch.ElapsedMilliseconds} ms");

shader.SimpleRender();
stopWatch = Stopwatch.StartNew();
stopWatch.Stop();
Console.WriteLine($"Render vectors(simple): {stopWatch.ElapsedMilliseconds} ms");

shader.RenderWithGamma();
stopWatch = Stopwatch.StartNew();
shader.RenderWithGamma();
stopWatch.Stop();
Console.WriteLine($"Render vectors(gamma): {stopWatch.ElapsedMilliseconds} ms");

shader.RenderWithGammaSsaa2();
stopWatch = Stopwatch.StartNew();
shader.RenderWithGammaSsaa2();
stopWatch.Stop();
Console.WriteLine($"Render vectors(ssaa2): {stopWatch.ElapsedMilliseconds} ms");

shader.RenderWithGammaSsaa4();
stopWatch = Stopwatch.StartNew();
shader.RenderWithGammaSsaa4();
stopWatch.Stop();
Console.WriteLine($"Render vectors(ssaa4): {stopWatch.ElapsedMilliseconds} ms");
