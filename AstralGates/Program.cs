using LX;
using AstralGates;
using SDFEngine.Render;
using System.Diagnostics;

//App.OnRun += static () => new LXRenderer();
//App.Run();
await new ConsoleRenderer().Start();


RenderPipeline RenderPipeline = new();

var shader = new Shader(1920, 1080, RenderPipeline.ShDr);

var stopWatch = Stopwatch.StartNew();
shader.RenderRgbaToBytes();
stopWatch.Stop();
Console.WriteLine($"Render bytes: {stopWatch.ElapsedMilliseconds} ms");

stopWatch = Stopwatch.StartNew();
shader.Render();
stopWatch.Stop();
Console.WriteLine($"Render vectors: {stopWatch.ElapsedMilliseconds} ms");
