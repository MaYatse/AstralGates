using System.Numerics;
using HybridSdfEngine.Render;

internal static class ConsoleDisplayStateChecks
{
    public static void Run()
    {
        using var output = new MemoryStream();
        using var display = new ConsoleDisplay(output);
        Vector3[] frame = [Vector3.One, Vector3.Zero];
        display.ColoredGraphic(2, 1, frame);
        display.Render();
        byte[] fullFrame = output.ToArray();
        output.SetLength(0);

        display.ColoredGraphic(2, 1, frame);
        display.Invalidate();
        display.Render();
        Equal(output.ToArray(), fullFrame, "Invalidation after preparation must repaint");

        output.SetLength(0);
        display.Render();
        Equal(output.ToArray(), [], "Rendering without a pending color frame must emit nothing");

        display.MaskGraphics(2, 1, [0f, 1f]);
        display.Render();
        output.SetLength(0);
        display.Render();
        Equal(output.ToArray(), [], "Rendering without a pending mask must emit nothing");
        display.MaskGraphics(2, 1, [0f, 1f]);
        display.Render();
        Equal(output.ToArray(), [], "An unchanged mask must emit nothing");

        CheckWriteRecovery();

        display.Dispose();
        display.Dispose();
        if (!output.CanWrite) throw new Exception("Dispose must leave a caller-owned stream open");
        ExpectDisposed(display.Render);
        ExpectDisposed(() => display.ColoredGraphic(2, 1, frame));
        ExpectDisposed(() => display.MaskGraphics(2, 1, [0f, 1f]));

        Console.WriteLine("PASS: pending-frame invalidation, mask caching, write recovery, disposal and stream ownership.");
    }

    private static void CheckWriteRecovery()
    {
        using var output = new PartialWriteStream();
        using var display = new ConsoleDisplay(output);
        display.ColoredGraphic(2, 1, [Vector3.Zero, Vector3.Zero]);
        display.Render();
        output.SetLength(0);

        Vector3[] frame = [Vector3.One, Vector3.Zero];
        display.ColoredGraphic(2, 1, frame);
        output.FailNextWrite = true;
        try
        {
            display.Render();
            throw new Exception("A failed write must propagate its exception");
        }
        catch (IOException) { }

        output.SetLength(0);
        display.Render();
        byte[] recovered = output.ToArray();
        output.SetLength(0);
        display.Invalidate();
        display.ColoredGraphic(2, 1, frame);
        display.Render();
        Equal(recovered, output.ToArray(), "A retry after a partial write must repaint the whole frame");
    }

    private static void Equal(byte[] actual, byte[] expected, string message)
    {
        if (!actual.AsSpan().SequenceEqual(expected)) throw new Exception(message);
    }

    private static void ExpectDisposed(Action action)
    {
        try
        {
            action();
            throw new Exception("A disposed display must reject rendering");
        }
        catch (ObjectDisposedException) { }
    }

    private sealed class PartialWriteStream : MemoryStream
    {
        public bool FailNextWrite { get; set; }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (FailNextWrite)
            {
                FailNextWrite = false;
                base.Write(buffer[..1]);
                throw new IOException("Simulated partial write");
            }
            base.Write(buffer);
        }
    }
}
