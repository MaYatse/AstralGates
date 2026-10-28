using System.Buffers.Text;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace HybridSdfEngine.Render;

/// <summary>Reusable true-color ANSI half-block renderer. Use one instance from one thread.</summary>
public sealed class ConsoleDisplay : IDisposable
{
    private readonly Stream _output;
    private readonly bool _ownsOutput, _terminal, _restoreCursorVisible;
    private readonly nint _consoleHandle;
    private readonly uint _originalConsoleMode;
    private readonly Encoding? _originalOutputEncoding;
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
            // Raw UTF-8 glyph bytes must also be decoded as UTF-8 by the console host.
            _originalOutputEncoding = Console.OutputEncoding;
            Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
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
            try
            {
                if (_originalOutputEncoding is { } encoding) Console.OutputEncoding = encoding;
            }
            finally { _output.Dispose(); }
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

    /// <summary>One pixel per column, two pixel rows per terminal row. An unpaired bottom half is black.</summary>
    public void ColoredGraphic(int resolutionX, int resolutionY, Vector3[] colors) =>
        ColoredGraphic(resolutionX, resolutionY, colors, resolutionX, resolutionY);

    /// <summary>Scales source pixels to the output pixel grid with nearest-neighbor sampling before half-block encoding.</summary>
    public void ColoredGraphic(int sourceWidth, int sourceHeight, Vector3[] colors, int outputWidth, int outputHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Validate(sourceWidth, sourceHeight, colors);
        int count = PixelCount(outputWidth, outputHeight);
        Prepare(outputWidth, outputHeight, count, colored: true);
        if (sourceWidth == outputWidth && sourceHeight == outputHeight)
        {
            for (int i = 0; i < count; i++) _pending[i] = PackColor(colors[i]);
            return;
        }
        int previousSourceRow = -1;
        for (int y = 0; y < outputHeight; y++)
        {
            int sourceRow = (int)((long)y * sourceHeight / outputHeight);
            int targetOffset = y * outputWidth;
            if (sourceRow == previousSourceRow)
            {
                // Repeated rows reuse already quantized pixels, without a full-size RGB buffer.
                Array.Copy(_pending, targetOffset - outputWidth, _pending, targetOffset, outputWidth);
                continue;
            }
            int sourceOffset = sourceRow * sourceWidth;
            int previousSourceColumn = -1, color = 0;
            for (int x = 0; x < outputWidth; x++)
            {
                int sourceColumn = (int)((long)x * sourceWidth / outputWidth);
                if (sourceColumn != previousSourceColumn)
                {
                    color = PackColor(colors[sourceOffset + sourceColumn]);
                    previousSourceColumn = sourceColumn;
                }
                _pending[targetOffset + x] = color;
            }
            previousSourceRow = sourceRow;
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
        if (_colored)
        {
            int foreground = -1, background = -1;
            for (int y = 0; y < _height; y += 2)
                AppendColoredRow(y, compare, ref foreground, ref background);
        }
        else
        {
            for (int y = 0; y < _height; y++) AppendMaskRow(y, compare);
        }
        if (_colored) Append("\u001b[0m"u8);
    }

    private void AppendColoredRow(int row, bool compare, ref int foreground, ref int background)
    {
        bool positioned = false;
        bool hasBottom = row + 1 < _height;
        for (int x = 0; x < _width; x++)
        {
            int index = row * _width + x;
            int top = _pending[index];
            int bottom = hasBottom ? _pending[index + _width] : 0;
            if (compare && _displayed[index] == top && (!hasBottom || _displayed[index + _width] == bottom))
            {
                positioned = false;
                continue;
            }
            if (!positioned)
            {
                Cursor(row / 2 + 1, x + 1);
                positioned = true;
            }
            if (foreground != top)
            {
                Color(top, foreground: true);
                foreground = top;
            }
            if (background != bottom)
            {
                Color(bottom, foreground: false);
                background = bottom;
            }
            Append("▀"u8);
        }
    }

    private void AppendMaskRow(int row, bool compare)
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
                Cursor(row + 1, x + 1);
                positioned = true;
            }
            MaskPixel(pixel);
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

    private static int PackColor(Vector3 pixel) => (Channel(pixel.X) << 16) | (Channel(pixel.Y) << 8) | Channel(pixel.Z);

    private void Color(int color, bool foreground)
    {
        // Maximum RGB command: 19 ASCII bytes. Glyphs are appended separately as UTF-8.
        Ensure(19);
        Span<byte> target = _buffer.AsSpan(_length);
        (foreground ? "\u001b[38;2;"u8 : "\u001b[48;2;"u8).CopyTo(target);
        int offset = 7;
        WriteChannel(target, ref offset, color >> 16);
        target[offset++] = (byte)';';
        WriteChannel(target, ref offset, (color >> 8) & 255);
        target[offset++] = (byte)';';
        WriteChannel(target, ref offset, color & 255);
        target[offset++] = (byte)'m';
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
        int count = PixelCount(width, height);
        if (pixels.Length < count) throw new ArgumentException("Pixel array is smaller than the frame.", nameof(pixels));
        return count;
    }

    private static int PixelCount(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return checked(width * height);
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
            try
            {
                if (_originalOutputEncoding is { } encoding) Console.OutputEncoding = encoding;
            }
            finally
            {
                if (_ownsOutput) _output.Dispose();
            }
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
