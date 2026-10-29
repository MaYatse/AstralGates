using System.Numerics;
using HybridSdfEngine.Input;
using HybridSdfEngine.Render;

namespace AstralGates.Input;

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
        if (input.WasPressed(InputKey.Escape))
        {
            Enabled = false;
            return;
        }
        if (input.WasPressed(InputKey.F1))
        {
            Enabled = !Enabled;
            return; // Ignore movement accumulated before activation.
        }
        if (!Enabled) return;

        if (input.IsDown(InputMouseButton.Button2) && !input.WasPressed(InputMouseButton.Button2))
            _camera.SetRotation(_camera.Yaw + input.MouseDelta.X * MouseSensitivity,
                _camera.Pitch - input.MouseDelta.Y * MouseSensitivity);

        float forward = (input.IsDown(InputKey.W) ? 1f : 0f) - (input.IsDown(InputKey.S) ? 1f : 0f);
        float right = (input.IsDown(InputKey.D) ? 1f : 0f) - (input.IsDown(InputKey.A) ? 1f : 0f);
        Vector3 direction = _camera.Forward * forward + _camera.Right * right;
        if (direction.LengthSquared() > 1f) direction = Vector3.Normalize(direction);
        _camera.Position += direction * (MoveSpeed * deltaTime);
    }

    private static void Validate(float value)
    {
        if (!float.IsFinite(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
    }
}
