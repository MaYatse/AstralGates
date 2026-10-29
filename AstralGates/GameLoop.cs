using HybridSdfEngine.Render;
using System.Diagnostics;
using HybridSdfEngine.Input;
using AstralGates.Input;

namespace AstralGates;

internal sealed class GameLoop
{
    readonly RenderPipeline pipeline = new ();
    
    public async Task Start()
    {
        using var display = new ConsoleRenderer();
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
            pipeline.UpdateScene(0);
            pipeline.BakeRealtimeEnvironment();
            var settings = new RenderSettings();
            pipeline.Mode = settings.Mode;
            var (width, height) = ConsoleRenderer.GetResolution();
            Shader shader = new(width, height, pipeline.ShDr, pipeline.ShDrSample);

            var time = Stopwatch.StartNew();
            double lastTime = time.Elapsed.TotalSeconds;
            const double fixedStep = 1.0 / 60.0;
            double accumulated = 0;
            var lastStatus = (Width: 0, Height: 0, Enabled: false, Ssaa: -1, Scale: 0, Denoise: false, Mode: RenderMode.Bsdf);
            while (!cancellation.IsCancellationRequested)
            {
                if (input.Completion.IsCompleted)
                {
                    await input.Completion;
                    throw new InvalidOperationException("The input hook stopped unexpectedly.");
                }
                var (resX, resY) = ConsoleRenderer.GetResolution();
                double currentTime = time.Elapsed.TotalSeconds;
                float deltaTime = (float)Math.Min(currentTime - lastTime, 0.1);
                lastTime = currentTime;
                InputFrame inputFrame = input.ReadFrame();
                settings.HandleInput(inputFrame);
                pipeline.Mode = settings.Mode;
                shader.SpatialDenoisingEnabled = settings.DenoisingEnabled;
                int renderWidth = settings.Scale(resX);
                int renderHeight = settings.Scale(resY);
                if (shader.ResolutionX != renderWidth || shader.ResolutionY != renderHeight)
                    shader.SetResolution(renderWidth, renderHeight);
                controller.Update(inputFrame, deltaTime);
                accumulated += deltaTime;
                while (accumulated >= fixedStep)
                {
                    pipeline.UpdateScene((float)fixedStep);
                    accumulated -= fixedStep;
                }
            
                var pixels = settings.Ssaa switch
                {
                    2 => shader.RenderWithGammaSsaa2(),
                    4 => shader.RenderWithGammaSsaa4(),
                    _ => shader.RenderWithGamma()
                };

                display.Present(renderWidth, renderHeight, pixels, resX, resY);
                var status = (Width: resX, Height: resY, Enabled: controller.Enabled, Ssaa: settings.Ssaa, Scale: settings.ScalePercent, Denoise: shader.SpatialDenoisingEnabled, Mode: pipeline.Mode);
                if (status != lastStatus)
                {
                    display.DrawInputStatus(resX, resY, shader, controller.Enabled, settings.Ssaa, settings.ScalePercent, pipeline.Mode);
                    lastStatus = status;
                }
                if (!Console.IsInputRedirected)
                    while (Console.KeyAvailable) Console.ReadKey(intercept: true);
            }
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

}
