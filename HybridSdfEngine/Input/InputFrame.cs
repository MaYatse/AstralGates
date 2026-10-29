using System.Numerics;

namespace HybridSdfEngine.Input;

/// <summary>A frame snapshot, reused by InputState.ReadFrame. Read it from the update thread only.</summary>
public sealed class InputFrame
{
    internal HashSet<InputKey> KeysDown { get; } = [];
    internal HashSet<InputKey> KeysPressed { get; } = [];
    internal HashSet<InputKey> KeysReleased { get; } = [];
    internal HashSet<InputMouseButton> ButtonsDown { get; } = [];
    internal HashSet<InputMouseButton> ButtonsPressed { get; } = [];
    internal HashSet<InputMouseButton> ButtonsReleased { get; } = [];

    public Vector2 MousePosition { get; internal set; }
    public Vector2 MouseDelta { get; internal set; }
    public bool IsDown(InputKey key) => KeysDown.Contains(key);
    public bool IsDown(InputMouseButton button) => ButtonsDown.Contains(button);
    public bool WasPressed(InputKey key) => KeysPressed.Contains(key);
    public bool WasPressed(InputMouseButton button) => ButtonsPressed.Contains(button);
    public bool WasReleased(InputKey key) => KeysReleased.Contains(key);
    public bool WasReleased(InputMouseButton button) => ButtonsReleased.Contains(button);
}
