using System.Numerics;
using HybridSdfEngine.Render;
using SharpHook.Data;

namespace HybridSdfEngine.Input;

/// <summary>F1 toggles controls, Escape disables them; WASD moves and right-drag looks around.</summary>
public sealed class FlyCameraController(Camera camera)
{
    private readonly Camera _camera = camera ?? throw new ArgumentNullException(nameof(camera));
    private float _moveSpeed = 4f;
    private float _mouseSensitivity = 0.0025f;

    public bool Enabled { get; set; }
    public float MoveSpeed
    {
        get => _moveSpeed;
        set { Validate(value); _moveSpeed = value; }
    }

    /// <summary>Radians per mouse coordinate unit. Mouse displacement is not multiplied by delta time.</summary>
    public float MouseSensitivity
    {
        get => _mouseSensitivity;
        set { Validate(value); _mouseSensitivity = value; }
    }

    public void Update(InputFrame input, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(deltaTime);
        if (input.WasPressed(KeyCode.VcEscape))
        {
            Enabled = false;
            return;
        }
        if (input.WasPressed(KeyCode.VcF1))
        {
            Enabled = !Enabled;
            return; // Ignore movement accumulated before activation.
        }
        if (!Enabled) return;

        if (input.IsDown(MouseButton.Button2) && !input.WasPressed(MouseButton.Button2))
            _camera.SetRotation(_camera.Yaw + input.MouseDelta.X * MouseSensitivity,
                _camera.Pitch - input.MouseDelta.Y * MouseSensitivity);

        float forward = (input.IsDown(KeyCode.VcW) ? 1f : 0f) - (input.IsDown(KeyCode.VcS) ? 1f : 0f);
        float right = (input.IsDown(KeyCode.VcD) ? 1f : 0f) - (input.IsDown(KeyCode.VcA) ? 1f : 0f);
        Vector3 direction = _camera.Forward * forward + _camera.Right * right;
        if (direction.LengthSquared() > 1f) direction = Vector3.Normalize(direction);
        _camera.Position += direction * (MoveSpeed * deltaTime);
    }

    private static void Validate(float value)
    {
        if (!float.IsFinite(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
