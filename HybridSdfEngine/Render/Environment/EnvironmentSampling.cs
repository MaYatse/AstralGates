using System.Numerics;

namespace HybridSdfEngine.Render.Environment;

internal static class EnvironmentSampling
{
    public static Vector2 Hammersley(int index, int count)
    {
        uint bits = (uint)index;
        bits = (bits << 16) | (bits >> 16);
        bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
        bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
        bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
        bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
        return new((float)index / count, bits * 2.3283064365386963e-10f);
    }

    public static Vector3 GgxHalf(Vector2 u, float roughness)
    {
        float alpha = MathF.Max(roughness * roughness, 0.001f);
        float cosine = MathF.Sqrt((1 - u.Y) / (1 + (alpha * alpha - 1) * u.Y));
        float sine = MathF.Sqrt(MathF.Max(0, 1 - cosine * cosine));
        var (sin, cos) = MathF.SinCos(MathF.Tau * u.X);
        return new(sine * cos, sine * sin, cosine);
    }

    public static Vector3 CubeDirection(int face, float u, float v) => Vector3.Normalize(face switch
    {
        0 => new Vector3(1, -v, -u),
        1 => new Vector3(-1, -v, u),
        2 => new Vector3(u, 1, v),
        3 => new Vector3(u, -1, -v),
        4 => new Vector3(u, -v, 1),
        _ => new Vector3(-u, -v, -1)
    });

    public static Vector3 SampleCube(Vector3[] pixels, int size, Vector3 direction)
    {
        Vector3 absolute = Vector3.Abs(direction);
        int face;
        float u, v, major;
        if (absolute.X >= absolute.Y && absolute.X >= absolute.Z)
        {
            face = direction.X >= 0 ? 0 : 1;
            major = absolute.X;
            u = direction.X >= 0 ? -direction.Z : direction.Z;
            v = -direction.Y;
        }
        else if (absolute.Y >= absolute.Z)
        {
            face = direction.Y >= 0 ? 2 : 3;
            major = absolute.Y;
            u = direction.X;
            v = direction.Y >= 0 ? direction.Z : -direction.Z;
        }
        else
        {
            face = direction.Z >= 0 ? 4 : 5;
            major = absolute.Z;
            u = direction.Z >= 0 ? direction.X : -direction.X;
            v = -direction.Y;
        }
        return Bilinear(pixels, size, face * size * size, (u / major + 1) * 0.5f, (v / major + 1) * 0.5f);
    }

    public static Vector3 Bilinear(Vector3[] pixels, int size, int offset, float u, float v)
    {
        float x = Math.Clamp(u * size - 0.5f, 0, size - 1);
        float y = Math.Clamp(v * size - 0.5f, 0, size - 1);
        int x0 = (int)x, y0 = (int)y;
        int x1 = Math.Min(x0 + 1, size - 1), y1 = Math.Min(y0 + 1, size - 1);
        return Vector3.Lerp(Vector3.Lerp(pixels[offset + y0 * size + x0], pixels[offset + y0 * size + x1], x - x0),
            Vector3.Lerp(pixels[offset + y1 * size + x0], pixels[offset + y1 * size + x1], x - x0), y - y0);
    }
}
