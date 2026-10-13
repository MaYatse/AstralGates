using System.Numerics;

namespace SDFEngine.Render;

public class Shader(
    int resolutionX,
    int resolutionY,
    Func<Vector2, Vector2, Vector3> attachedShader)
{
    private readonly Vector3[] buffer  = new Vector3[resolutionX * resolutionY];
    private readonly byte[] byteBuffer = new byte[resolutionX * resolutionY * 4];

    public Vector3[] Render()
    {
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
                buffer[y * resolutionX + x] = attachedShader?.Invoke(new(x, y), new(resolutionX, resolutionY)) ?? new Vector3(0);
        });

        return buffer;
    }

    public byte[] RenderRgbaToBytes()
    {
        Parallel.For(0, resolutionY, y =>
        {
            for (int x = 0; x < resolutionX; x++)
            {
                var v = attachedShader?.Invoke(new(x, y), new(resolutionX, resolutionY)) ?? new Vector3(0);

                int index = (y * resolutionX + x) * 4;
                byteBuffer[index + 0] = (byte)Math.Clamp(v.X * 255, 0, 255);
                byteBuffer[index + 1] = (byte)Math.Clamp(v.Y * 255, 0, 255);
                byteBuffer[index + 2] = (byte)Math.Clamp(v.Z * 255, 0, 255);
                byteBuffer[index + 3] = 255;
            }
        });
        return byteBuffer;
    }
}

