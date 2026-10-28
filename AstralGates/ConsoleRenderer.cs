using HybridSdfEngine.Render;
using System.Diagnostics;
using HybridSdfEngine.Input;
using AstralGates.Input;
using SharpHook.Data;

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
            int ssaa = 0;
            int renderScale = 100;
            var lastStatus = (Width: 0, Height: 0, Enabled: false, Ssaa: -1, Scale: 0);
            while (!cancellation.IsCancellationRequested)
            {
                if (input.Completion.IsCompleted)
                {
                    await input.Completion;
                    throw new InvalidOperationException("The input hook stopped unexpectedly.");
                }
                var (resX, resY) = GetResolution();
                double currentTime = time.Elapsed.TotalSeconds;
                float deltaTime = (float)(currentTime - lastTime);
                lastTime = currentTime;
                InputFrame inputFrame = input.ReadFrame();
                if (inputFrame.WasPressed(KeyCode.VcF2))
                    ssaa = ssaa switch { 0 => 2, 2 => 4, _ => 0 };
                if (inputFrame.WasPressed(KeyCode.VcF3))
                    renderScale = renderScale switch { 100 => 75, 75 => 50, _ => 100 };
                int renderWidth = Math.Max(1, (int)(((long)resX * renderScale + 50) / 100));
                int renderHeight = Math.Max(1, (int)(((long)resY * renderScale + 50) / 100));
                if (shader.ResolutionX != renderWidth || shader.ResolutionY != renderHeight)
                    shader.SetResolution(renderWidth, renderHeight);
                controller.Update(inputFrame, MathF.Min(deltaTime, 0.1f));
                pipeline.UpdateScene(deltaTime);
            
                var pixels = ssaa switch
                {
                    2 => shader.RenderWithGammaSsaa2(),
                    4 => shader.RenderWithGammaSsaa4(),
                    _ => shader.RenderWithGamma()
                };

                display.ColoredGraphic(renderWidth, renderHeight, pixels, resX, resY);
                display.Render();
                var status = (Width: resX, Height: resY, Enabled: controller.Enabled, Ssaa: ssaa, Scale: renderScale);
                if (status != lastStatus)
                {
                    DrawInputStatus(resX, resY, renderWidth, renderHeight, controller.Enabled, ssaa, renderScale);
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
        (Math.Max(1, Console.WindowWidth - 1), Math.Max(1, Console.WindowHeight - 2) * 2);

    private static void DrawInputStatus(int width, int height, int renderWidth, int renderHeight, bool enabled, int ssaa, int renderScale)
    {
        int row = (height + 1) / 2;
        if (Console.IsOutputRedirected || row >= Console.WindowHeight) return;
        string text = $"F3: {renderScale}% | CPU: {renderWidth}x{renderHeight} | Grid: {width}x{height} px ({width}x{row} cells) | F2 SSAA: {(ssaa == 0 ? "off" : ssaa.ToString())} | Input: {(enabled ? "ON" : "OFF")} | F1 toggle | WASD move | Hold RMB to look | Esc pause | Ctrl+C exit";
        int available = Math.Min(width, Math.Max(0, Console.WindowWidth - 1));
        Console.SetCursorPosition(0, row);
        Console.Write(text[..Math.Min(text.Length, available)].PadRight(available));
    }
}
