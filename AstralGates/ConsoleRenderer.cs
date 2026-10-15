using HybridSdfEngine.Render;
using System.Diagnostics;

namespace AstralGates;

internal class ConsoleRenderer
{
    readonly RenderPipeline pipeline = new ();
    
    public async Task Start()
    {
        using var display = new ConsoleDisplay();
        using var cancellation = new CancellationTokenSource();
        void OnCancel(object? sender, ConsoleCancelEventArgs args)
        {
            args.Cancel = true;
            cancellation.Cancel();
        }

        Console.CancelKeyPress += OnCancel;
        try
        {
            pipeline.Angle = 10f;
            Shader shader = new(Console.WindowWidth / 2, Console.WindowHeight - 2, pipeline.ShDr);

            var time = Stopwatch.StartNew();
            double lastTime = time.Elapsed.TotalSeconds;
            while (!cancellation.IsCancellationRequested)
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
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }
}
