using System.Numerics;

namespace HybridSdfEngine.Render;

/// <summary>Captures per-pixel guides and filters indirect light in linear space.</summary>
internal sealed class DenoisedFrameRenderer
{
    private Vector3[] _indirect = [], _filtered = [];
    private SurfaceGuide[] _guides = [];
    private readonly SpatialDenoiser _denoiser = new();

    public void Reset()
    {
        _indirect = [];
        _filtered = [];
        _guides = [];
    }

    public Vector3[] Render(Vector3[] buffer, Vector2 resolution, int resolutionX, int resolutionY,
        Func<Vector2, Vector2, RaySample>? sampleShader, int samples, bool gamma)
    {
        var sample = sampleShader ?? throw new InvalidOperationException("Spatial denoising requires a shader that returns RaySample data.");
        if (_guides.Length != buffer.Length)
        {
            _guides = new SurfaceGuide[buffer.Length];
            _indirect = new Vector3[buffer.Length];
            _filtered = new Vector3[buffer.Length];
        }
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++) CapturePixel(buffer, resolution, resolutionX, sample, samples, x, y);
        });
        _denoiser.Filter(_indirect, _guides, _filtered, resolutionX, resolutionY);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
            {
                int index = y * resolutionX + x;
                Vector3 color = buffer[index] + _filtered[index];
                buffer[index] = gamma ? Shader.GammaCorrection(color) : color;
            }
        });
        return buffer;
    }

    private void CapturePixel(Vector3[] buffer, Vector2 resolution, int resolutionX,
        Func<Vector2, Vector2, RaySample> sample, int samples, int x, int y)
    {
        Vector3 color = Vector3.Zero, direct = Vector3.Zero;
        SurfaceGuide guide = default;
        for (int i = 0; i < samples; i++)
        {
            Vector2 offset = samples switch
            {
                2 => i == 0 ? new(-0.25f, -0.25f) : new(0.25f, 0.25f),
                4 => new((i & 1) == 0 ? -0.25f : 0.25f, i < 2 ? -0.25f : 0.25f),
                _ => Vector2.Zero
            };
            RaySample current = sample(new Vector2(x, y) + offset, resolution);
            color += current.Color;
            direct += current.DirectRadiance;
            if (i == 0) guide = current.Surface;
            else if (!current.Surface.Filterable || !SpatialDenoiser.Compatible(guide, current.Surface))
                guide = guide with { Filterable = false };
        }
        int index = y * resolutionX + x;
        buffer[index] = direct / samples;
        _indirect[index] = Vector3.Max(color / samples - buffer[index], Vector3.Zero);
        _guides[index] = guide;
    }

}
