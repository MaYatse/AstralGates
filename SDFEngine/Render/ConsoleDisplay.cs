using System.Buffers.Text;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SDFEngine.Render;

/// <summary>Reusable true-color ANSI renderer. Use one instance from one thread.</summary>
public class ConsoleDisplay
{
    private readonly Stream _output;
    private byte[] _buffer = new byte[4096];
    private int _length;
    private int[] _pending = [], _displayed = [];
    private int _width, _height;
    private bool _hasDisplayedFrame, _colored;

    public ConsoleDisplay() : this(Console.OpenStandardOutput())
    {
        if (!Console.IsOutputRedirected)
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = GetStdHandle(-11);
                if (GetConsoleMode(handle, out uint mode))
                    SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
            }
            Console.CursorVisible = false;
        }
    }

    /// <summary>The caller owns the output stream. Useful for capture and benchmarks.</summary>
    public ConsoleDisplay(Stream output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output must be writable.", nameof(output));
        _output = output;
    }

    public void Render()
    {
        if (_length == 0) return;
        try
        {
            _output.Write(_buffer.AsSpan(0, _length));
            _output.Flush();
        }
        catch
        {
            // A failed write may have changed only part of the terminal screen.
            Invalidate();
            throw;
        }
        if (_colored)
        {
            _pending.CopyTo(_displayed, 0);
            _hasDisplayedFrame = true;
            _length = 0;
        }
        else _hasDisplayedFrame = false;
    }

    /// <summary>Call before preparing a frame after resizing or other console output.</summary>
    public void Invalidate() => _hasDisplayedFrame = false;

    public void MaskGraphics(int resolutionX, int resolutionY, float[] matrix)
    {
        Validate(resolutionX, resolutionY, matrix);
        _length = 0;
        _colored = false;
        Append("\u001b[0m"u8);
        for (int y = 0; y < resolutionY; y++)
        {
            Cursor(y + 1, 1);
            for (int x = 0; x < resolutionX; x++)
            {
                float value = matrix[y * resolutionX + x];
                if (value < 0.2f || float.IsNaN(value)) Append(" "u8);
                else if (value < 0.4f) Append("░"u8);
                else if (value < 0.6f) Append("▒"u8);
                else if (value < 0.8f) Append("▓"u8);
                else Append("█"u8);
            }
        }
    }

    public void ColoredGraphic(int resolutionX, int resolutionY, Vector3[] itog)
    {
        int count = Validate(resolutionX, resolutionY, itog);
        if (_width != resolutionX || _height != resolutionY)
        {
            _pending = new int[count];
            _displayed = new int[count];
            _width = resolutionX;
            _height = resolutionY;
            Invalidate();
        }
        _length = 0;
        _colored = true;
        int activeColor = -1;
        for (int y = 0; y < resolutionY; y++)
        {
            bool positioned = false;
            for (int x = 0; x < resolutionX; x++)
            {
                int index = y * resolutionX + x;
                Vector3 pixel = itog[index];
                int color = (Channel(pixel.X) << 16) | (Channel(pixel.Y) << 8) | Channel(pixel.Z);
                _pending[index] = color;
                if (_hasDisplayedFrame && _displayed[index] == color)
                {
                    positioned = false;
                    continue;
                }
                if (!positioned)
                {
                    Cursor(y + 1, x * 2 + 1);
                    positioned = true;
                }
                if (activeColor != color)
                {
                    ColorAndPixel(color);
                    activeColor = color;
                }
                else Append("  "u8);
            }
        }
        if (_length > 0) Append("\u001b[0m"u8);
    }

    private static int Channel(float value) => value > 0 ? (int)MathF.Min(value * 255, 255) : 0;

    private void ColorAndPixel(int color)
    {
        // Maximum RGB command plus two spaces: 21 ASCII bytes.
        Ensure(21);
        Span<byte> target = _buffer.AsSpan(_length);
        "\u001b[48;2;"u8.CopyTo(target);
        int offset = 7;
        WriteChannel(target, ref offset, color >> 16);
        target[offset++] = (byte)';';
        WriteChannel(target, ref offset, (color >> 8) & 255);
        target[offset++] = (byte)';';
        WriteChannel(target, ref offset, color & 255);
        target[offset++] = (byte)'m';
        target[offset++] = (byte)' ';
        target[offset++] = (byte)' ';
        _length += offset;
    }

    private static void WriteChannel(Span<byte> target, ref int offset, int value)
    {
        if (value >= 100) target[offset++] = (byte)('0' + value / 100);
        if (value >= 10) target[offset++] = (byte)('0' + value / 10 % 10);
        target[offset++] = (byte)('0' + value % 10);
    }

    private static int Validate<T>(int width, int height, T[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (width > (int.MaxValue - 1) / 2) throw new ArgumentOutOfRangeException(nameof(width));
        int count = checked(width * height);
        if (pixels.Length < count) throw new ArgumentException("Pixel array is smaller than the frame.", nameof(pixels));
        return count;
    }

    private void Cursor(int row, int column)
    {
        Append("\u001b["u8);
        Number(row);
        Append(";"u8);
        Number(column);
        Append("H"u8);
    }

    private void Number(int value)
    {
        Ensure(10);
        Utf8Formatter.TryFormat(value, _buffer.AsSpan(_length), out int written);
        _length += written;
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        Ensure(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
    }

    private void Ensure(int additional)
    {
        int required = checked(_length + additional);
        if (required > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(required, checked(_buffer.Length * 2)));
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int stdHandle);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint handle, uint mode);
}
