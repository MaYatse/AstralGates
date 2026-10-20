using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace HybridSdfEngine.Export;

/// <summary>
/// Streams display-space RGB frames to FFmpeg. Call CompleteAsync to publish the GIF;
/// disposing an incomplete writer cancels it. Use one caller at a time.
/// </summary>
public sealed class GifWriter : IAsyncDisposable
{
    private readonly Process _encoder;
    private readonly Task<string> _errors;
    private readonly string _temporaryPath;
    private readonly bool _overwrite;
    private readonly byte[] _rgb;
    private bool _completed, _faulted, _disposed;

    public string OutputPath { get; }
    public int Width { get; }
    public int Height { get; }
    public int FramesPerSecond { get; }
    public int FrameCount { get; private set; }

    [SuppressMessage("Security", "S4036", Justification = "PATH lookup intentionally supports an installed FFmpeg; callers can also provide an absolute executable path.")]
    public GifWriter(string outputPath, int width, int height, int framesPerSecond = 25,
        bool overwrite = false, string ffmpegPath = "ffmpeg")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(framesPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(framesPerSecond, 100);
        _rgb = new byte[checked(width * height * 3)];
        OutputPath = Path.GetFullPath(outputPath);
        if (!overwrite && File.Exists(OutputPath))
            throw new IOException($"The output file already exists: {OutputPath}");
        Width = width;
        Height = height;
        FramesPerSecond = framesPerSecond;
        _overwrite = overwrite;
        string directory = Path.GetDirectoryName(OutputPath)!;
        Directory.CreateDirectory(directory);
        _temporaryPath = Path.Combine(directory, $".{Path.GetFileName(OutputPath)}.{Guid.NewGuid():N}.partial.gif");

        var start = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true
        };
        string[] arguments = [
            "-hide_banner", "-loglevel", "error", "-nostdin",
            "-f", "rawvideo", "-pixel_format", "rgb24",
            "-video_size", string.Create(CultureInfo.InvariantCulture, $"{width}x{height}"),
            "-framerate", framesPerSecond.ToString(CultureInfo.InvariantCulture), "-i", "pipe:0",
            "-filter_complex",
            "[0:v]split[a][b];[a]palettegen=stats_mode=single:reserve_transparent=0[p];[b][p]paletteuse=new=1:dither=sierra2_4a",
            "-loop", "0", "-f", "gif", _temporaryPath
        ];
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        _encoder = new Process { StartInfo = start };
        try
        {
            _encoder.Start();
        }
        catch (Win32Exception error)
        {
            _encoder.Dispose();
            throw new InvalidOperationException(
                $"Could not start FFmpeg '{ffmpegPath}'. Install FFmpeg and add it to PATH, or provide its executable path.", error);
        }
        _errors = _encoder.StandardError.ReadToEndAsync();
    }

    /// <summary>
    /// Copies one frame before returning. Input is gamma/display encoded, like Shader.RenderWithGamma;
    /// values are clipped to [0,1], NaN is black. No additional gamma conversion is applied.
    /// </summary>
    public async Task WriteFrameAsync(ReadOnlyMemory<Vector3> pixels, CancellationToken cancellationToken = default)
    {
        CheckWritable();
        if (pixels.Length != Width * Height)
            throw new ArgumentException("The pixel count must match the GIF dimensions.", nameof(pixels));
        cancellationToken.ThrowIfCancellationRequested();
        EncodeRgb(pixels.Span);
        try
        {
            await _encoder.StandardInput.BaseStream.WriteAsync(_rgb, cancellationToken).ConfigureAwait(false);
            FrameCount++;
        }
        catch (IOException error)
        {
            _faulted = true;
            Terminate();
            string details = await _errors.ConfigureAwait(false);
            throw new IOException($"FFmpeg could not accept the frame. {details}", error);
        }
        catch (OperationCanceledException)
        {
            _faulted = true;
            throw;
        }
    }

    /// <summary>Closes the encoder and moves the finished GIF into place after a successful exit.</summary>
    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed) return;
        CheckWritable();
        if (FrameCount == 0) throw new InvalidOperationException("Cannot save a GIF without frames.");
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            _encoder.StandardInput.Close();
            await _encoder.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            string details = await _errors.ConfigureAwait(false);
            if (_encoder.ExitCode != 0)
                throw new IOException($"FFmpeg exited with code {_encoder.ExitCode}. {details}");
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(_temporaryPath, OutputPath, _overwrite);
            _completed = true;
        }
        catch
        {
            _faulted = true;
            throw;
        }
    }

    private void CheckWritable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completed || _faulted) throw new InvalidOperationException("The GIF writer is no longer accepting frames.");
    }

    private void EncodeRgb(ReadOnlySpan<Vector3> pixels)
    {
        for (int i = 0; i < pixels.Length; i++)
        {
            Vector3 pixel = pixels[i];
            _rgb[i * 3] = Channel(pixel.X);
            _rgb[i * 3 + 1] = Channel(pixel.Y);
            _rgb[i * 3 + 2] = Channel(pixel.Z);
        }
    }

    private static byte Channel(float value) => value > 0f ? (byte)MathF.Min(value * 255f, 255f) : (byte)0;

    private void Terminate()
    {
        if (_encoder.HasExited) return;
        try { _encoder.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { /* The process exited between the check and Kill. */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (!_completed) Terminate();
            await _encoder.WaitForExitAsync().ConfigureAwait(false);
            await _errors.ConfigureAwait(false);
        }
        finally
        {
            _encoder.Dispose();
            if (File.Exists(_temporaryPath)) File.Delete(_temporaryPath);
        }
        GC.SuppressFinalize(this);
    }
}
