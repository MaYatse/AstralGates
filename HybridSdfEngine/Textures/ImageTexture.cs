using System.Numerics;

namespace HybridSdfEngine.Textures;

/// <summary>Immutable RGB image. Row zero corresponds to V=0; sampling uses texel centers.</summary>
public sealed class ImageTexture : ITexture<Vector3>
{
    private readonly Vector3[] _pixels;
    public int Width { get; }
    public int Height { get; }
    public TextureAddressMode AddressMode { get; }
    public TextureFilter Filter { get; }

    public ImageTexture(int width, int height, ReadOnlySpan<Vector3> pixels,
        TextureColorSpace colorSpace = TextureColorSpace.Linear,
        TextureAddressMode addressMode = TextureAddressMode.Repeat,
        TextureFilter filter = TextureFilter.Bilinear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height))
            throw new ArgumentException("The pixel count must match the image dimensions.", nameof(pixels));
        if (!Enum.IsDefined(colorSpace)) throw new ArgumentOutOfRangeException(nameof(colorSpace));
        if (!Enum.IsDefined(addressMode)) throw new ArgumentOutOfRangeException(nameof(addressMode));
        if (!Enum.IsDefined(filter)) throw new ArgumentOutOfRangeException(nameof(filter));
        Width = width;
        Height = height;
        AddressMode = addressMode;
        Filter = filter;
        _pixels = pixels.ToArray();
        for (int i = 0; i < _pixels.Length; i++)
        {
            Vector3 pixel = _pixels[i];
            if (!float.IsFinite(pixel.X) || !float.IsFinite(pixel.Y) || !float.IsFinite(pixel.Z))
                throw new ArgumentException("Image pixels must be finite.", nameof(pixels));
            if (colorSpace == TextureColorSpace.Srgb)
            {
                if (pixel.X < 0 || pixel.Y < 0 || pixel.Z < 0 || pixel.X > 1 || pixel.Y > 1 || pixel.Z > 1)
                    throw new ArgumentException("sRGB pixels must be in [0, 1].", nameof(pixels));
                _pixels[i] = new(ToLinear(pixel.X), ToLinear(pixel.Y), ToLinear(pixel.Z));
            }
        }
    }

    /// <summary>Creates an RGB texture from decoded RGBA8 pixels. Alpha is ignored.</summary>
    public static ImageTexture FromRgba32(int width, int height, ReadOnlySpan<byte> rgba,
        TextureColorSpace colorSpace = TextureColorSpace.Srgb,
        TextureAddressMode addressMode = TextureAddressMode.Repeat,
        TextureFilter filter = TextureFilter.Bilinear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int count = checked(width * height);
        if (rgba.Length != checked(count * 4))
            throw new ArgumentException("Expected exactly four bytes per pixel.", nameof(rgba));
        var pixels = new Vector3[count];
        for (int i = 0; i < count; i++)
            pixels[i] = new Vector3(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2]) / 255f;
        return new(width, height, pixels, colorSpace, addressMode, filter);
    }

    public Vector3 Sample(in TextureContext context)
    {
        Vector2 uv = context.UV;
        if (!float.IsFinite(uv.X) || !float.IsFinite(uv.Y)) return Vector3.Zero;
        float u = Address(uv.X), v = Address(uv.Y);
        if (Filter == TextureFilter.Nearest)
            return Pixel((int)MathF.Floor(u * Width), (int)MathF.Floor(v * Height));

        float x = u * Width - 0.5f, y = v * Height - 0.5f;
        int left = (int)MathF.Floor(x), top = (int)MathF.Floor(y);
        float tx = x - left, ty = y - top;
        Vector3 a = Vector3.Lerp(Pixel(left, top), Pixel(left + 1, top), tx);
        Vector3 b = Vector3.Lerp(Pixel(left, top + 1), Pixel(left + 1, top + 1), tx);
        return Vector3.Lerp(a, b, ty);
    }

    private float Address(float value) => AddressMode == TextureAddressMode.Repeat
        ? value - MathF.Floor(value) : Math.Clamp(value, 0f, 1f);

    private Vector3 Pixel(int x, int y)
    {
        x = Index(x, Width);
        y = Index(y, Height);
        return _pixels[y * Width + x];
    }

    private int Index(int value, int size) => AddressMode == TextureAddressMode.Repeat
        ? (value % size + size) % size : Math.Clamp(value, 0, size - 1);

    private static float ToLinear(float value) => value <= 0.04045f
        ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
}
