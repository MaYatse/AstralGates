using HybridSdfEngine.Render;
using System.Diagnostics;

namespace AstralGates;

internal class ConsoleRenderer
{
    readonly ConsoleDisplay display = new ();
    readonly RenderPipeline pipeline = new ();
    
    public async Task Start()
    {
        bool run = true;
        
        pipeline.Angle = 10f;
        Shader shader = new(Console.WindowWidth / 2, Console.WindowHeight - 2, pipeline.ShDr);

        var time = Stopwatch.StartNew();
        double lastTime = time.Elapsed.TotalSeconds;
        while (run)
        {
            int resX = Console.WindowWidth / 2;
            int resY = Console.WindowHeight - 2;
            if (shader.ResolutionX != resX || shader.ResolutionY != resY)
                shader.SetResolution(resX, resY);

            double currentTime = time.Elapsed.TotalSeconds;
            float deltaTime = (float)(currentTime - lastTime);
            lastTime = currentTime;
            pipeline.UpdateScene(deltaTime);
            
            var test = shader.RenderWithGammaSsaa4();

            display.ColoredGraphic(resX, resY, test);
            display.Render();
            await Task.Delay(0);
        }
    }
}
