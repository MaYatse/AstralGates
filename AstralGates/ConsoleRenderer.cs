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
        pipeline.Angle = .01f;
        Shader shader = new(Console.WindowWidth / 2, Console.WindowHeight - 2, pipeline.ShDr);

        while (run)
        {
            int resX = Console.WindowWidth / 2;
            int resY = Console.WindowHeight - 2;
            if (shader.ResolutionX != resX || shader.ResolutionY != resY)
                shader.SetResolution(resX, resY);

            pipeline.Time = (float)time.Elapsed.TotalMicroseconds;
            pipeline.UpdateScene();
            
            var test = shader.RenderWithGamma();

            display.ColoredGraphic(resX, resY, test);
            display.Render();
            await Task.Delay(0);
        }
    }
}
