using System.Numerics;

namespace HybridSdfEngine.Render;

public class Shader(
    int resolutionX,
    int resolutionY,
    Func<Vector2, Vector2, Vector3> attachedShader,
    Func<Vector2, Vector2, RaySample>? sampleShader = null)
{
    private const float _baseGamma = 1f / 2.2f;
    private Vector3[] buffer  = new Vector3[resolutionX * resolutionY];
    private Vector2 resolution = new(resolutionX, resolutionY);
    private Vector3[] _indirect = [], _filtered = [];
    private SurfaceGuide[] _guides = [];
    private readonly SpatialDenoiser _denoiser = new();
    public int ResolutionX => resolutionX;
    public int ResolutionY => resolutionY;
    public bool SpatialDenoisingEnabled { get; set; }


    public void SetResolution(int x, int y)
    {
        resolutionX = x;
        resolutionY = y;
        resolution = new(x, y);
        buffer = new Vector3[resolutionX * resolutionY];
        _indirect = [];
        _filtered = [];
        _guides = [];
    }

    public Vector3[] SimpleRender()
    {
        if (SpatialDenoisingEnabled) return RenderDenoised(1, gamma: false);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = attachedShader.Invoke(new(x, y), resolution);
        });

        return buffer;
    }

    public Vector3[] RenderWithGamma()
    {
        if (SpatialDenoisingEnabled) return RenderDenoised(1, gamma: true);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = GammaCorrection( 
                    attachedShader(new(x, y), resolution) 
                    );
        });

        return buffer;
    }

    public Vector3[] RenderWithGammaSsaa4()
    {
        if (SpatialDenoisingEnabled) return RenderDenoised(4, gamma: true);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = GammaCorrection((
                    attachedShader(new Vector2(x - 0.25f, y - 0.25f), resolution) +
                    attachedShader(new Vector2(x + 0.25f, y - 0.25f), resolution) +
                    attachedShader(new Vector2(x - 0.25f, y + 0.25f), resolution) +
                    attachedShader(new Vector2(x + 0.25f, y + 0.25f), resolution)
                ) * 0.25f);    
        });

        return buffer;
    }

    public Vector3[] RenderWithGammaSsaa2()
    {
        if (SpatialDenoisingEnabled) return RenderDenoised(2, gamma: true);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = GammaCorrection((
                    attachedShader(new Vector2(x - 0.25f, y - 0.25f), resolution) +
                    attachedShader(new Vector2(x + 0.25f, y + 0.25f), resolution)
                ) * 0.5f);    
        });

        return buffer;
    }

    private Vector3[] RenderDenoised(int samples, bool gamma)
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
            for (int x = 0; x < resolutionX; x++) CapturePixel(sample, samples, x, y);
        });
        _denoiser.Filter(_indirect, _guides, _filtered, resolutionX, resolutionY);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
            {
                int index = y * resolutionX + x;
                Vector3 color = buffer[index] + _filtered[index];
                buffer[index] = gamma ? GammaCorrection(color) : color;
            }
        });
        return buffer;
    }

    private void CapturePixel(Func<Vector2, Vector2, RaySample> sample, int samples, int x, int y)
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

    private static Vector3 GammaCorrection(Vector3 color) => new(
            MathF.Pow(color.X, _baseGamma),
            MathF.Pow(color.Y, _baseGamma),
            MathF.Pow(color.Z, _baseGamma)
        );
}

