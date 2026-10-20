using System.Numerics;
using SharpHook.Data;

namespace HybridSdfEngine.Input;

/// <summary>A frame snapshot, reused by InputState.ReadFrame. Read it from the update thread only.</summary>
public sealed class InputFrame
{
    internal HashSet<KeyCode> KeysDown { get; } = [];
    internal HashSet<KeyCode> KeysPressed { get; } = [];
    internal HashSet<KeyCode> KeysReleased { get; } = [];
    internal HashSet<MouseButton> ButtonsDown { get; } = [];
    internal HashSet<MouseButton> ButtonsPressed { get; } = [];
    internal HashSet<MouseButton> ButtonsReleased { get; } = [];

    public Vector2 MousePosition { get; internal set; }
    public Vector2 MouseDelta { get; internal set; }
    public bool IsDown(KeyCode key) => KeysDown.Contains(key);
    public bool IsDown(MouseButton button) => ButtonsDown.Contains(button);
    public bool WasPressed(KeyCode key) => KeysPressed.Contains(key);
    public bool WasPressed(MouseButton button) => ButtonsPressed.Contains(button);
    public bool WasReleased(KeyCode key) => KeysReleased.Contains(key);
    public bool WasReleased(MouseButton button) => ButtonsReleased.Contains(button);
}
