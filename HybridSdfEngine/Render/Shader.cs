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
    private readonly DenoisedFrameRenderer _denoisedRenderer = new();
    public int ResolutionX => resolutionX;
    public int ResolutionY => resolutionY;
    public bool SpatialDenoisingEnabled { get; set; }


    public void SetResolution(int x, int y)
    {
        resolutionX = x;
        resolutionY = y;
        resolution = new(x, y);
        buffer = new Vector3[resolutionX * resolutionY];
        _denoisedRenderer.Reset();
    }

    public Vector3[] SimpleRender()
    {
        if (SpatialDenoisingEnabled) return _denoisedRenderer.Render(buffer, resolution, resolutionX, resolutionY, sampleShader, 1, gamma: false);
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = attachedShader.Invoke(new(x, y), resolution);
        });

        return buffer;
    }

    public Vector3[] RenderWithGamma()
    {
        if (SpatialDenoisingEnabled) return _denoisedRenderer.Render(buffer, resolution, resolutionX, resolutionY, sampleShader, 1, gamma: true);
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
        if (SpatialDenoisingEnabled) return _denoisedRenderer.Render(buffer, resolution, resolutionX, resolutionY, sampleShader, 4, gamma: true);
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
        if (SpatialDenoisingEnabled) return _denoisedRenderer.Render(buffer, resolution, resolutionX, resolutionY, sampleShader, 2, gamma: true);
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

    internal static Vector3 GammaCorrection(Vector3 color) => new(
            MathF.Pow(color.X, _baseGamma),
            MathF.Pow(color.Y, _baseGamma),
            MathF.Pow(color.Z, _baseGamma)
        );
}

