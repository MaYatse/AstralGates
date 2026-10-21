using HybridSdfEngine.Render;
using System.Diagnostics;
using HybridSdfEngine.Input;
using AstralGates.Input;

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
            await using IInputSource input = new SharpHookInputSource();
            await input.StartAsync();
            var controller = new FlyCameraController(pipeline.Camera);
            pipeline.Angle = 10f;
            var (width, height) = GetResolution();
            Shader shader = new(width, height, pipeline.ShDr);

            var time = Stopwatch.StartNew();
            double lastTime = time.Elapsed.TotalSeconds;
            var lastStatus = (Width: 0, Height: 0, Enabled: false);
            while (!cancellation.IsCancellationRequested)
            {
                if (input.Completion.IsCompleted)
                {
                    await input.Completion;
                    throw new InvalidOperationException("The input hook stopped unexpectedly.");
                }
                var (resX, resY) = GetResolution();
                if (shader.ResolutionX != resX || shader.ResolutionY != resY)
                    shader.SetResolution(resX, resY);

                double currentTime = time.Elapsed.TotalSeconds;
                float deltaTime = (float)(currentTime - lastTime);
                lastTime = currentTime;
                controller.Update(input.ReadFrame(), MathF.Min(deltaTime, 0.1f));
                pipeline.UpdateScene(deltaTime);
            
                var test = shader.RenderWithGammaSsaa4();

                display.ColoredGraphic(resX, resY, test);
                display.Render();
                var status = (Width: resX, Height: resY, Enabled: controller.Enabled);
                if (status != lastStatus)
                {
                    DrawInputStatus(resX, resY, controller.Enabled);
                    lastStatus = status;
                }
                if (!Console.IsInputRedirected)
                    while (Console.KeyAvailable) Console.ReadKey(intercept: true);
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

    private static void DrawInputStatus(int width, int row, bool enabled)
    {
        if (Console.IsOutputRedirected) return;
        string text = $"Input: {(enabled ? "ON" : "OFF")} | F1 toggle | WASD move | Hold RMB to look | Esc pause | Ctrl+C exit";
        int available = Math.Min(width * 2, Math.Max(0, Console.WindowWidth - 1));
        Console.SetCursorPosition(0, row);
        Console.Write(text[..Math.Min(text.Length, available)].PadRight(available));
    }
}
