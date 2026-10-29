using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

public class AreaLight : ILight
{
    private Vector3 _position;
    private Quaternion _rotation = Quaternion.Identity;
    private Vector3 _right = Vector3.UnitX, _up = Vector3.UnitY;
    private Vector2 _size = Vector2.One;
    private float _area = 1f;
    private int _samplesPerAxis = 2;

    public Vector3 Position
    {
        get => _position;
        set
        {
            LightMath.RequireFinite(value, nameof(value));
            _position = value;
        }
    }

    /// <summary>Panel orientation. The local emitting normal is +Z.</summary>
    public Quaternion Rotation
    {
        get => _rotation;
        set
        {
            float scale = MathF.Max(MathF.Max(MathF.Abs(value.X), MathF.Abs(value.Y)),
                MathF.Max(MathF.Abs(value.Z), MathF.Abs(value.W)));
            if (!float.IsFinite(scale) || scale <= 0f ||
                !float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
                !float.IsFinite(value.Z) || !float.IsFinite(value.W))
                throw new ArgumentException("Rotation must be finite and nonzero.", nameof(value));

            _rotation = Quaternion.Normalize(new(value.X / scale, value.Y / scale, value.Z / scale, value.W / scale));
            _right = Vector3.Transform(Vector3.UnitX, _rotation);
            _up = Vector3.Transform(Vector3.UnitY, _rotation);
            Normal = Vector3.Transform(Vector3.UnitZ, _rotation);
        }
    }

    public Vector3 Normal { get; private set; } = Vector3.UnitZ;

    /// <summary>Full width and height in world units, not half-extents.</summary>
    public Vector2 Size
    {
        get => _size;
        set
        {
            float area = value.X * value.Y;
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) ||
                value.X <= 0f || value.Y <= 0f || !float.IsFinite(area) || area <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Panel dimensions and area must be finite and positive.");
            _size = value;
            _area = area;
        }
    }

    /// <summary>Grid resolution, from 1 to 32. Total cost is SamplesPerAxis squared.</summary>
    public int SamplesPerAxis
    {
        get => _samplesPerAxis;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 32);
            _samplesPerAxis = value;
        }
    }

    public int SampleCount => _samplesPerAxis * _samplesPerAxis;
    public bool TwoSided { get; set; }
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>Emitted radiance multiplier per unit area. A larger panel emits more total light.</summary>
    public float Intensity { get; set; } = 1f;

    public AreaLight() { }

    public AreaLight(Vector3 position, Quaternion rotation, Vector2 size, Vector3 color,
        float intensity, int samplesPerAxis = 2)
    {
        Position = position;
        Rotation = rotation;
        Size = size;
        Color = color;
        Intensity = intensity;
        SamplesPerAxis = samplesPerAxis;
    }

    public LightSample Sample(Vector3 point) => Sample(point, 0);

    public LightSample Sample(Vector3 point, int sampleIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sampleIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(sampleIndex, SampleCount);
        return SampleAt(point, new((sampleIndex % _samplesPerAxis + 0.5f) / _samplesPerAxis,
            (sampleIndex / _samplesPerAxis + 0.5f) / _samplesPerAxis));
    }

    /// <summary>Samples normalized panel coordinates in [0,1] without changing the configured sample grid.</summary>
    public LightSample SampleAt(Vector3 point, Vector2 position)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)
            || position.X < 0 || position.X > 1 || position.Y < 0 || position.Y > 1)
            throw new ArgumentOutOfRangeException(nameof(position));
        float u = (position.X - 0.5f) * Size.X;
        float v = (position.Y - 0.5f) * Size.Y;
        Vector3 samplePosition = Position + _right * u + _up * v;
        Vector3 offset = samplePosition - point;
        float distanceSquared = offset.LengthSquared();
        if (!float.IsFinite(distanceSquared) || distanceSquared <= 1e-12f)
            return default;

        float distance = MathF.Sqrt(distanceSquared);
        Vector3 toLight = offset / distance;
        float cosine = Vector3.Dot(Normal, -toLight);
        cosine = TwoSided ? MathF.Abs(cosine) : MathF.Max(cosine, 0f);
        return new(toLight, Color * (Intensity * _area * cosine / distanceSquared), distance);
    }
}
