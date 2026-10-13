using System.Numerics;

namespace SDFEngine.Render;

public class Shader(
    int resolutionX,
    int resolutionY,
    Func<Vector2, Vector2, Vector3> attachedShader)
{
    private const float _baseGamma = 1f / 2.2f;
    private Vector3[] buffer  = new Vector3[resolutionX * resolutionY];
    //private readonly byte[] byteBuffer = new byte[resolutionX * resolutionY * 4];
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

    //public byte[] RenderRgbaToBytes()
    //{
    //    Parallel.For(0, resolutionY, y =>
    //    {
    //        for (int x = 0; x < resolutionX; x++)
    //        {
    //            var v = attachedShader?.Invoke(new(x, y), new(resolutionX, resolutionY)) ?? new Vector3(0);

    //            int index = (y * resolutionX + x) * 4;
    //            byteBuffer[index + 0] = (byte)Math.Clamp(v.X * 255, 0, 255);
    //            byteBuffer[index + 1] = (byte)Math.Clamp(v.Y * 255, 0, 255);
    //            byteBuffer[index + 2] = (byte)Math.Clamp(v.Z * 255, 0, 255);
    //            byteBuffer[index + 3] = 255;
    //        }
    //    });
    //    return byteBuffer;
    //}

    private static Vector3 GammaCorrection(Vector3 color) => new(
            MathF.Pow(color.X, _baseGamma),
            MathF.Pow(color.Y, _baseGamma),
            MathF.Pow(color.Z, _baseGamma)
        );
}

