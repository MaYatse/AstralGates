using System.Globalization;
using HybridSdfEngine.Export;
using HybridSdfEngine.Render;

namespace AstralGates;

internal static class GifRenderCommand
{
    public const string Usage = "--gif [output.gif [width height fps seconds]] [--overwrite]";

    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        void OnCancel(object? sender, ConsoleCancelEventArgs eventArgs)
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        }
        Console.CancelKeyPress += OnCancel;
        try
        {
            var options = Parse(args);
            await using var writer = new GifWriter(options.Output, options.Width, options.Height,
                options.Fps, options.Overwrite);
            var pipeline = new RenderPipeline { Angle = 1f };
            var shader = new Shader(options.Width, options.Height, pipeline.ShDr);
            float deltaTime = 1f / options.Fps;
            Console.WriteLine($"Rendering {options.Frames} frames at {options.Width}x{options.Height}, {options.Fps} FPS...");
            for (int frame = 0; frame < options.Frames; frame++)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                pipeline.UpdateScene(deltaTime);
                var pixels = shader.RenderWithGammaSsaa4();
                await writer.WriteFrameAsync(pixels, cancellation.Token);
                if ((frame + 1) % options.Fps == 0 || frame + 1 == options.Frames)
                    Console.WriteLine($"Frames: {frame + 1}/{options.Frames}");
            }
            await writer.CompleteAsync(cancellation.Token);
            Console.WriteLine($"Saved: {writer.OutputPath}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("GIF export canceled. No incomplete GIF was published.");
            return 130;
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidOperationException or FormatException or OverflowException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"GIF export failed: {error.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= OnCancel;
        }
    }

    private static Options Parse(string[] args)
    {
        string[] values = args[1..];
        bool overwrite = values.Length > 0 && values[^1] == "--overwrite";
        if (overwrite) values = values[..^1];
        if (values.Length != 0 && values.Length != 1 && values.Length != 5)
            throw new ArgumentException($"Usage: {Usage}");
        string output = values.Length > 0 ? values[0] : Path.Combine("artifacts", "render.gif");
        int width = 320, height = 180, fps = 25;
        double seconds = 4;
        if (values.Length == 5)
        {
            width = int.Parse(values[1], CultureInfo.InvariantCulture);
            height = int.Parse(values[2], CultureInfo.InvariantCulture);
            fps = int.Parse(values[3], CultureInfo.InvariantCulture);
            seconds = double.Parse(values[4], CultureInfo.InvariantCulture);
        }
        if (!double.IsFinite(seconds) || seconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(args), "Duration must be finite and positive.");
        ArgumentOutOfRangeException.ThrowIfLessThan(fps, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fps, 100);
        double frames = Math.Ceiling(seconds * fps);
        if (!double.IsFinite(frames) || frames > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(args), "Too many frames requested.");
        return new(output, width, height, fps, (int)frames, overwrite);
    }

    private sealed record Options(string Output, int Width, int Height, int Fps, int Frames, bool Overwrite);
}
