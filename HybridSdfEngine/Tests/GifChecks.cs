using System.Numerics;
using System.Text;
using HybridSdfEngine.Export;
using StbImageSharp;

internal static class GifChecks
{
    public static async Task RunAsync()
    {
        string directory = Directory.CreateTempSubdirectory("AstralGates-GifChecks-").FullName;
        string output = Path.Combine(directory, "test animation.gif");
        try
        {
            Vector3[] pixels = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
                Vector3.Zero, Vector3.One, new(float.NaN, -1f, float.PositiveInfinity)];
            await using (var writer = new GifWriter(output, 3, 2))
            {
                await ThrowsAsync<InvalidOperationException>(() => writer.CompleteAsync());
                await ThrowsAsync<ArgumentException>(() => writer.WriteFrameAsync(new Vector3[1]));
                await writer.WriteFrameAsync(pixels);
                Array.Fill(pixels, Vector3.One);
                await writer.WriteFrameAsync(pixels);
                Check(!File.Exists(output), "Publish only after successful completion.");
                await writer.CompleteAsync();
                await writer.CompleteAsync();
                Check(writer.FrameCount == 2, "Frame counter.");
                await ThrowsAsync<InvalidOperationException>(() => writer.WriteFrameAsync(pixels));
            }

            using (var input = File.OpenRead(output))
            {
                int count = 0;
                foreach (var frame in ImageResult.AnimatedGifFramesFromStream(input, ColorComponents.RedGreenBlueAlpha))
                {
                    Check(frame.Width == 3 && frame.Height == 2, "Non-square dimensions and orientation.");
                    Check(frame.DelayInMs == 40, "25 FPS must produce a 40 ms delay.");
                    byte[] expected = count == 0
                        ? [255, 0, 0, 0, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0, 0, 255]
                        : Enumerable.Repeat((byte)255, 18).ToArray();
                    for (int i = 0; i < 6; i++)
                        for (int channel = 0; channel < 3; channel++)
                            Check(frame.Data[i * 4 + channel] == expected[i * 3 + channel], "Pixel channels or frame buffer reuse.");
                    count++;
                }
                Check(count == 2, "GIF contains both frames.");
            }
            byte[] original = await File.ReadAllBytesAsync(output);
            int loop = original.AsSpan().IndexOf(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
            Check(loop >= 0 && original.AsSpan(loop + 11, 5).SequenceEqual(new byte[] { 3, 1, 0, 0, 0 }), "Infinite looping.");
            Throws<IOException>(() => new GifWriter(output, 3, 2));
            Throws<ArgumentOutOfRangeException>(() => new GifWriter(output, 0, 2));
            Throws<ArgumentOutOfRangeException>(() => new GifWriter(output, 3, 2, 0));
            Throws<InvalidOperationException>(() => new GifWriter(Path.Combine(directory, "missing.gif"), 3, 2,
                ffmpegPath: Path.Combine(directory, "missing-ffmpeg.exe")));

            await using (var canceled = new GifWriter(output, 3, 2, overwrite: true))
            {
                await canceled.WriteFrameAsync(pixels);
                await ThrowsAsync<OperationCanceledException>(() => canceled.WriteFrameAsync(pixels, new CancellationToken(true)));
            }
            byte[] afterCancel = await File.ReadAllBytesAsync(output);
            Check(original.SequenceEqual(afterCancel), "Canceled export preserves the existing GIF.");

            await using (var replacement = new GifWriter(output, 3, 2, overwrite: true))
            {
                Array.Fill(pixels, Vector3.Zero);
                await replacement.WriteFrameAsync(pixels);
                await replacement.CompleteAsync();
            }
            byte[] afterReplace = await File.ReadAllBytesAsync(output);
            Check(!original.SequenceEqual(afterReplace), "Explicit overwrite publishes a new GIF.");
            Check(Directory.GetFiles(directory).Length == 1, "Temporary files are cleaned up.");
            Console.WriteLine("GIF checks passed: encoding, pixels, timing, looping, overwrite and cancellation.");
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
