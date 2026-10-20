using System.Numerics;

namespace HybridSdfEngine.Render;

/// <summary>Perspective camera facing +Z at zero yaw/pitch. Update only between rendered frames.</summary>
public sealed class Camera
{
    private const float PitchLimit = MathF.PI / 2f - 0.01f;
    private Vector3 _position;

    public Vector3 Position
    {
        get => _position;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(nameof(value), "Camera position must be finite.");
            _position = value;
        }
    }

    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public Vector3 Forward { get; private set; } = Vector3.UnitZ;
    public Vector3 Right { get; private set; } = Vector3.UnitX;
    public Vector3 Up { get; private set; } = Vector3.UnitY;

    public void SetRotation(float yaw, float pitch)
    {
        if (!float.IsFinite(yaw)) throw new ArgumentOutOfRangeException(nameof(yaw));
        if (!float.IsFinite(pitch)) throw new ArgumentOutOfRangeException(nameof(pitch));
        Yaw = MathF.IEEERemainder(yaw, MathF.Tau);
        Pitch = Math.Clamp(pitch, -PitchLimit, PitchLimit);
        var (sinYaw, cosYaw) = MathF.SinCos(Yaw);
        var (sinPitch, cosPitch) = MathF.SinCos(Pitch);
        Forward = new(sinYaw * cosPitch, sinPitch, cosYaw * cosPitch);
        Right = new(cosYaw, 0f, -sinYaw);
        Up = Vector3.Cross(Forward, Right);
    }

    public Vector3 GetRayDirection(Vector2 uv) => Vector3.Normalize(Forward + uv.X * Right + uv.Y * Up);
}
