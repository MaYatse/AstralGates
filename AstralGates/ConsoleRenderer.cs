using SDFEngine.Render;
using System.Diagnostics;

namespace AstralGates;

internal class ConsoleRenderer
{
    readonly ConsoleDisplay display = new ();
    readonly RenderPipeline pipeline = new ();

    public async Task Start()
    {
        bool run = true;
        var time = Stopwatch.StartNew();
        while (run)
        {
            int resX = Console.WindowWidth / 2;
            int resY = Console.WindowHeight - 2;

            pipeline.Time = (float)time.Elapsed.TotalMicroseconds;
            var shader = new Shader(resX, resY, pipeline.ShDr);
            var test = shader.Render();
            pipeline.IsLock = false;
            pipeline.Angle+= 0.0001f;

            display.ColoredGraphic(resX, resY, test);
            display.Render();
            await Task.Delay(0);
        }
    }
}
