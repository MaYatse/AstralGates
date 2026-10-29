using System.Numerics;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Textures;

namespace HybridSdfEngine.Render.Environment;

/// <summary>Immutable cubemap with precomputed GGX roughness levels and cosine-weighted diffuse illumination.</summary>
public sealed class PrefilteredEnvironmentMap : IEnvironmentLight
{
    private readonly Vector3[][] _levels;
    private readonly int[] _sizes;
    private readonly Vector3[] _diffuse;
    private readonly int _diffuseSize;

    public PrefilteredEnvironmentMap(Func<Vector3, Vector3> radiance, int resolution = 32, int sampleCount = 64)
    {
        ArgumentNullException.ThrowIfNull(radiance);
        ArgumentOutOfRangeException.ThrowIfLessThan(resolution, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(resolution, 256);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sampleCount, 4096);
        int levelCount = 1 + (int)MathF.Log2(resolution);
        _levels = new Vector3[levelCount][];
        _sizes = new int[levelCount];
        for (int level = 0; level < levelCount; level++)
        {
            int size = Math.Max(1, resolution >> level);
            _sizes[level] = size;
            _levels[level] = new Vector3[6 * size * size];
        }
        // The source is evaluated exactly once per base texel, before convolution.
        Fill(_levels[0], resolution, direction =>
        {
            Vector3 color = radiance(direction);
            if (!BsdfMath.IsFinite(color) || color.X < 0 || color.Y < 0 || color.Z < 0)
                throw new ArgumentException("Environment radiance must be finite, nonnegative linear RGB.", nameof(radiance));
            return color;
        });
        for (int level = 1; level < levelCount; level++)
        {
            float roughness = (float)level / (levelCount - 1);
            Fill(_levels[level], _sizes[level], normal => Prefilter(normal, roughness, sampleCount));
        }
        _diffuseSize = Math.Min(16, resolution);
        _diffuse = new Vector3[6 * _diffuseSize * _diffuseSize];
        Fill(_diffuse, _diffuseSize, normal => Diffuse(normal, sampleCount));
    }

    public static PrefilteredEnvironmentMap FromImage(ImageTexture image, int resolution = 32, int sampleCount = 64)
    {
        ArgumentNullException.ThrowIfNull(image);
        return new(direction =>
        {
            Vector2 uv = new(MathF.Atan2(direction.Z, direction.X) / MathF.Tau + 0.5f,
                MathF.Acos(Math.Clamp(direction.Y, -1, 1)) / MathF.PI);
            uv.Y = Math.Clamp(uv.Y, 0.5f / image.Height, 1 - 0.5f / image.Height);
            return image.Sample(new(direction, direction, direction, direction, uv, Vector3.UnitX, Vector3.UnitY));
        }, resolution, sampleCount);
    }

    public Vector3 SampleSpecular(Vector3 direction, float roughness)
    {
        ValidateDirection(direction);
        if (!float.IsFinite(roughness)) throw new ArgumentOutOfRangeException(nameof(roughness));
        float mip = Math.Clamp(roughness, 0, 1) * (_levels.Length - 1);
        int lower = (int)mip, upper = Math.Min(lower + 1, _levels.Length - 1);
        return Vector3.Lerp(EnvironmentSampling.SampleCube(_levels[lower], _sizes[lower], direction),
            EnvironmentSampling.SampleCube(_levels[upper], _sizes[upper], direction), mip - lower);
    }

    public Vector3 SampleDiffuse(Vector3 normal)
    {
        ValidateDirection(normal);
        return EnvironmentSampling.SampleCube(_diffuse, _diffuseSize, normal);
    }

    private Vector3 Prefilter(Vector3 normal, float roughness, int samples)
    {
        Vector3 sum = Vector3.Zero;
        float weights = 0;
        for (int i = 0; i < samples; i++)
        {
            Vector3 half = BsdfMath.ToWorld(EnvironmentSampling.GgxHalf(EnvironmentSampling.Hammersley(i, samples), roughness), normal);
            Vector3 light = Vector3.Reflect(-normal, half);
            float weight = MathF.Max(0, Vector3.Dot(normal, light));
            if (weight <= 0) continue;
            sum += EnvironmentSampling.SampleCube(_levels[0], _sizes[0], light) * weight;
            weights += weight;
        }
        return weights > 0 ? sum / weights : EnvironmentSampling.SampleCube(_levels[0], _sizes[0], normal);
    }

    private Vector3 Diffuse(Vector3 normal, int samples)
    {
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < samples; i++)
        {
            Vector3 direction = BsdfMath.CosineHemisphere(EnvironmentSampling.Hammersley(i, samples), normal);
            sum += EnvironmentSampling.SampleCube(_levels[0], _sizes[0], direction);
        }
        return sum / samples;
    }

    private static void Fill(Vector3[] pixels, int size, Func<Vector3, Vector3> sample)
    {
        // Keep scene capture single-threaded: source delegates can have mutable accounting/state.
        for (int face = 0; face < 6; face++)
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            pixels[(face * size + y) * size + x] = sample(EnvironmentSampling.CubeDirection(face,
                2f * (x + 0.5f) / size - 1, 2f * (y + 0.5f) / size - 1));
    }

    private static void ValidateDirection(Vector3 direction)
    {
        if (!BsdfMath.IsFinite(direction) || direction == Vector3.Zero)
            throw new ArgumentException("Environment direction must be finite and nonzero.", nameof(direction));
    }
}
