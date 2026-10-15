using AstralGates;
using HybridSdfEngine.Render;
using System.Diagnostics;
using System.Numerics;

switch (args)
{
    case []:
        await new ConsoleRenderer().Start();
        break;
    case ["--smoke-test"]:
        RunSmokeTest();
        break;
    case ["--timings"]:
        RunTimings();
        break;
    default:
        Console.Error.WriteLine("Usage: AstralGates [--smoke-test | --timings]");
        Environment.ExitCode = 2;
        break;
}

static void RunSmokeTest()
{
    const int width = 64, height = 36;
    var pipeline = new RenderPipeline();
    var shader = new Shader(width, height, pipeline.ShDr);
    using var output = new MemoryStream();
    using var display = new ConsoleDisplay(output);
    int litPixels = 0;
    for (int frame = 0; frame < 2; frame++)
    {
        pipeline.UpdateScene(1f / 60f);
        Vector3[] pixels = shader.RenderWithGammaSsaa4();
        litPixels = 0;
        foreach (Vector3 pixel in pixels)
        {
            if (!float.IsFinite(pixel.X) || !float.IsFinite(pixel.Y) || !float.IsFinite(pixel.Z))
                throw new InvalidOperationException("The rendered frame contains a non-finite color.");
            if (pixel.LengthSquared() > 0f) litPixels++;
        }
        if (litPixels == 0) throw new InvalidOperationException("The rendered frame is entirely black.");
        display.ColoredGraphic(width, height, pixels);
        display.Render();
    }
    if (output.Length == 0) throw new InvalidOperationException("No display output was produced.");
    Console.WriteLine($"PASS: application rendered 2 frames at {width}x{height} with shadows and SSAA4; " +
        $"{litPixels} lit pixels, {output.Length} output bytes.");
}

static void RunTimings()
{
    var pipeline = new RenderPipeline();
    var shader = new Shader(1920, 1080, pipeline.ShDr);
    Measure("simple", shader.SimpleRender);
    Measure("gamma", shader.RenderWithGamma);
    Measure("ssaa2", shader.RenderWithGammaSsaa2);
    Measure("ssaa4", shader.RenderWithGammaSsaa4);

    static void Measure(string name, Func<Vector3[]> render)
    {
        render();
        var timer = Stopwatch.StartNew();
        render();
        timer.Stop();
        Console.WriteLine($"Render vectors({name}): {timer.Elapsed.TotalMilliseconds:F2} ms");
    }
}
