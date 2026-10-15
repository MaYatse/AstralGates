using System.Numerics;

namespace HybridSdfEngine.SDF.Light;

public class SpotLight : ILight
{
    private Vector3 _position;
    private Vector3 _rayDirection;
    private float _innerAngle, _outerAngle, _cosInner, _cosOuter;

    public Vector3 Position
    {
        get => _position;
        set
        {
            LightMath.RequireFinite(value, nameof(value));
            _position = value;
        }
    }

    /// <summary>Normalized direction from the source toward the illuminated region.</summary>
    public Vector3 RayDirection
    {
        get => _rayDirection;
        set => _rayDirection = LightMath.Normalize(value, nameof(value));
    }

    public Vector3 Color { get; set; }
    public float Intensity { get; set; }

    /// <summary>Half-angle in radians, measured from the cone axis; full brightness inside.</summary>
    public float InnerAngle
    {
        get => _innerAngle;
        set => SetConeAngles(value, _outerAngle);
    }

    /// <summary>Half-angle in radians; zero illumination outside. Must be below pi/2.</summary>
    public float OuterAngle
    {
        get => _outerAngle;
        set => SetConeAngles(_innerAngle, value);
    }

    public SpotLight() : this(Vector3.Zero, Vector3.UnitZ, Vector3.One, 1f) { }

    public SpotLight(Vector3 position, Vector3 rayDirection, Vector3 color, float intensity,
        float innerAngle = MathF.PI / 8f, float outerAngle = MathF.PI / 6f)
    {
        Position = position;
        RayDirection = rayDirection;
        Color = color;
        Intensity = intensity;
        SetConeAngles(innerAngle, outerAngle);
    }

    /// <summary>Updates both half-angles atomically. Equal angles produce a hard edge.</summary>
    public void SetConeAngles(float innerAngle, float outerAngle)
    {
        if (!float.IsFinite(outerAngle) || outerAngle <= 0f || outerAngle >= MathF.PI / 2f)
            throw new ArgumentOutOfRangeException(nameof(outerAngle));
        if (!float.IsFinite(innerAngle) || innerAngle < 0f || innerAngle > outerAngle)
            throw new ArgumentOutOfRangeException(nameof(innerAngle));

        _innerAngle = innerAngle;
        _outerAngle = outerAngle;
        _cosInner = MathF.Cos(innerAngle);
        _cosOuter = MathF.Cos(outerAngle);
    }

    public LightSample Sample(Vector3 point)
    {
        Vector3 offset = Position - point;
        float distanceSquared = offset.LengthSquared();
        if (!float.IsFinite(distanceSquared) || distanceSquared <= 1e-12f)
            return default;

        float distance = MathF.Sqrt(distanceSquared);
        Vector3 toLight = offset / distance;
        float cosine = Vector3.Dot(RayDirection, -toLight);
        float falloff = ConeFalloff(cosine);
        return new(toLight, Color * (Intensity * falloff / distanceSquared), distance);
    }

    private float ConeFalloff(float cosine)
    {
        if (cosine >= _cosInner) return 1f;
        if (cosine <= _cosOuter) return 0f;
        float t = (cosine - _cosOuter) / (_cosInner - _cosOuter);
        return t * t * (3f - 2f * t);
    }
}
