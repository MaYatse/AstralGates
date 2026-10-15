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
            var (width, height) = GetResolution();
            Shader shader = new(width, height, pipeline.ShDr);

            var time = Stopwatch.StartNew();
            double lastTime = time.Elapsed.TotalSeconds;
            while (!cancellation.IsCancellationRequested)
            {
                var (resX, resY) = GetResolution();
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

    private static (int Width, int Height) GetResolution() =>
        (Math.Max(1, Console.WindowWidth / 2), Math.Max(1, Console.WindowHeight - 2));
}
