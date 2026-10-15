using System.Buffers.Text;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;

namespace HybridSdfEngine.Render;

/// <summary>Reusable true-color ANSI renderer. Use one instance from one thread.</summary>
public sealed class ConsoleDisplay : IDisposable
{
    private readonly Stream _output;
    private readonly bool _ownsOutput, _terminal, _restoreCursorVisible;
    private readonly nint _consoleHandle;
    private readonly uint _originalConsoleMode;
    private byte[] _buffer = new byte[4096];
    private int _length;
    private int[] _pending = [], _displayed = [];
    private int _width, _height, _displayedWidth, _displayedHeight;
    private bool _hasPendingFrame, _hasDisplayedFrame, _colored, _displayedColored;
    private bool _cursorHidden, _disposed;

    public ConsoleDisplay() : this(Console.OpenStandardOutput())
    {
        _ownsOutput = true;
        _terminal = !Console.IsOutputRedirected;
        if (!_terminal) return;
        try
        {
            _restoreCursorVisible = !OperatingSystem.IsWindows() || Console.CursorVisible;
            if (OperatingSystem.IsWindows())
            {
                _consoleHandle = GetStdHandle(-11);
                if (!GetConsoleMode(_consoleHandle, out _originalConsoleMode) ||
                    !SetConsoleMode(_consoleHandle, _originalConsoleMode | 0x0004))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch
        {
            _output.Dispose();
            throw;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_hasPendingFrame) return;
        BuildFrame();
        try
        {
            if (_length > 0)
            {
                // Mark before writing: even a failed write may have hidden the cursor.
                if (_terminal) _cursorHidden = true;
                _output.Write(_buffer.AsSpan(0, _length));
                _output.Flush();
            }
        }
        catch
        {
            // A failed write may have changed only part of the terminal screen.
            Invalidate();
            throw;
        }
        // Keep the last successfully displayed snapshot without copying a whole frame.
        (_pending, _displayed) = (_displayed, _pending);
        _displayedWidth = _width;
        _displayedHeight = _height;
        _displayedColored = _colored;
        _hasDisplayedFrame = true;
        _hasPendingFrame = false;
    }

    /// <summary>Force the next pending frame to repaint after resizing or other console output.</summary>
    public void Invalidate() => _hasDisplayedFrame = false;

    public void MaskGraphics(int resolutionX, int resolutionY, float[] matrix)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int count = Validate(resolutionX, resolutionY, matrix);
        Prepare(resolutionX, resolutionY, count, colored: false);
        for (int i = 0; i < count; i++)
        {
            float value = matrix[i];
            _pending[i] = MaskLevel(value);
        }
    }

    public void ColoredGraphic(int resolutionX, int resolutionY, Vector3[] colors)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int count = Validate(resolutionX, resolutionY, colors);
        Prepare(resolutionX, resolutionY, count, colored: true);
        for (int i = 0; i < count; i++)
        {
            Vector3 pixel = colors[i];
            _pending[i] = (Channel(pixel.X) << 16) | (Channel(pixel.Y) << 8) | Channel(pixel.Z);
        }
    }

    private void Prepare(int width, int height, int count, bool colored)
    {
        if (_pending.Length < count) Array.Resize(ref _pending, count);
        if (_displayed.Length < count) Array.Resize(ref _displayed, count);
        _width = width;
        _height = height;
        _colored = colored;
        _hasPendingFrame = true;
    }

    private void BuildFrame()
    {
        _length = 0;
        bool compare = _hasDisplayedFrame && _width == _displayedWidth &&
            _height == _displayedHeight && _colored == _displayedColored;
        int count = _width * _height;
        if (compare && _pending.AsSpan(0, count).SequenceEqual(_displayed.AsSpan(0, count))) return;
        // Reset inherited terminal attributes, including reverse video.
        Append("\u001b[0m"u8);
        if (_terminal && !_cursorHidden) Append("\u001b[?25l"u8);
        int activeColor = -1;
        int cellWidth = _colored ? 2 : 1;
        for (int y = 0; y < _height; y++)
            AppendRow(y, cellWidth, compare, ref activeColor);
        if (_colored) Append("\u001b[0m"u8);
    }

    private void AppendRow(int row, int cellWidth, bool compare, ref int activeColor)
    {
        bool positioned = false;
        for (int x = 0; x < _width; x++)
        {
            int index = row * _width + x;
            int pixel = _pending[index];
            if (compare && _displayed[index] == pixel)
            {
                positioned = false;
                continue;
            }
            if (!positioned)
            {
                Cursor(row + 1, x * cellWidth + 1);
                positioned = true;
            }
            if (!_colored) MaskPixel(pixel);
            else if (activeColor != pixel)
            {
                ColorAndPixel(pixel);
                activeColor = pixel;
            }
            else Append("  "u8);
        }
    }

    private static int MaskLevel(float value)
    {
        if (value < 0.2f || float.IsNaN(value)) return 0;
        if (value < 0.4f) return 1;
        if (value < 0.6f) return 2;
        if (value < 0.8f) return 3;
        return 4;
    }

    private void MaskPixel(int level)
    {
        switch (level)
        {
            case 0: Append(" "u8); break;
            case 1: Append("░"u8); break;
            case 2: Append("▒"u8); break;
            case 3: Append("▓"u8); break;
            default: Append("█"u8); break;
        }
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
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, (int.MaxValue - 1) / 2);
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

    /// <summary>Restore terminal state. A stream supplied by the caller remains open.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_cursorHidden)
            {
                _output.Write(_restoreCursorVisible ? "\u001b[0m\u001b[?25h"u8 : "\u001b[0m"u8);
                _output.Flush();
            }
        }
        finally
        {
            if (OperatingSystem.IsWindows() && _consoleHandle != 0)
                SetConsoleMode(_consoleHandle, _originalConsoleMode);
            if (_ownsOutput) _output.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int stdHandle);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint handle, uint mode);
}
