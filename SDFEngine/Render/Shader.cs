using System.Numerics;

namespace HybridSdfEngine.Render;

public class Shader(
    int resolutionX,
    int resolutionY,
    Func<Vector2, Vector2, Vector3> attachedShader)
{
    private const float _baseGamma = 1f / 2.2f;
    private Vector3[] buffer  = new Vector3[resolutionX * resolutionY];
    private Vector2 resolution = new(resolutionX, resolutionY);
    public int ResolutionX => resolutionX;
    public int ResolutionY => resolutionY;


    public void SetResolution(int x, int y)
    {
        resolutionX = x;
        resolutionY = y;
        resolution = new(x, y);
        buffer = new Vector3[resolutionX * resolutionY];
    }

    public Vector3[] SimpleRender()
    {
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = attachedShader.Invoke(new(x, y), resolution);
        });

        return buffer;
    }

    public Vector3[] RenderWithGamma()
    {
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

    private static Vector3 GammaCorrection(Vector3 color) => new(
            MathF.Pow(color.X, _baseGamma),
            MathF.Pow(color.Y, _baseGamma),
            MathF.Pow(color.Z, _baseGamma)
        );
}

