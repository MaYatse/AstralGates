using System.Numerics;
using SharpHook.Data;

namespace HybridSdfEngine.Input;

/// <summary>Thread-safe event accumulation with one consumer calling ReadFrame before each update.</summary>
public sealed class InputState
{
    private readonly object _sync = new();
    private readonly InputFrame _pending = new();
    private readonly InputFrame _frame = new();
    private bool _hasMousePosition;

    public void SetKey(KeyCode key, bool down)
    {
        lock (_sync)
            SetButton(_pending.KeysDown, _pending.KeysPressed, _pending.KeysReleased, key, down);
    }

    public void SetMouseButton(MouseButton button, bool down)
    {
        if (button == MouseButton.NoButton) return;
        lock (_sync)
            SetButton(_pending.ButtonsDown, _pending.ButtonsPressed, _pending.ButtonsReleased, button, down);
    }

    private static void SetButton<T>(HashSet<T> held, HashSet<T> pressed, HashSet<T> released, T key, bool down)
    {
        if (down)
        {
            if (held.Add(key)) pressed.Add(key);
        }
        else if (held.Remove(key)) released.Add(key);
    }

    public void SetMousePosition(Vector2 position)
    {
        Validate(position);
        lock (_sync)
        {
            if (_hasMousePosition) _pending.MouseDelta += position - _pending.MousePosition;
            _pending.MousePosition = position;
            _hasMousePosition = true;
        }
    }

    public void AddMouseDelta(Vector2 delta)
    {
        Validate(delta);
        lock (_sync) _pending.MouseDelta += delta;
    }

    /// <summary>Returns the same snapshot object each time. Transitions and mouse delta are consumed once.</summary>
    public InputFrame ReadFrame()
    {
        lock (_sync)
        {
            Copy(_pending.KeysDown, _frame.KeysDown);
            Copy(_pending.KeysPressed, _frame.KeysPressed);
            Copy(_pending.KeysReleased, _frame.KeysReleased);
            Copy(_pending.ButtonsDown, _frame.ButtonsDown);
            Copy(_pending.ButtonsPressed, _frame.ButtonsPressed);
            Copy(_pending.ButtonsReleased, _frame.ButtonsReleased);
            _frame.MousePosition = _pending.MousePosition;
            _frame.MouseDelta = _pending.MouseDelta;
            _pending.KeysPressed.Clear();
            _pending.KeysReleased.Clear();
            _pending.ButtonsPressed.Clear();
            _pending.ButtonsReleased.Clear();
            _pending.MouseDelta = Vector2.Zero;
            return _frame;
        }
    }

    /// <summary>Releases all held buttons and forgets mouse history, for example after focus loss.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _pending.KeysPressed.Clear();
            _pending.ButtonsPressed.Clear();
            _pending.KeysReleased.UnionWith(_pending.KeysDown);
            _pending.ButtonsReleased.UnionWith(_pending.ButtonsDown);
            _pending.KeysDown.Clear();
            _pending.ButtonsDown.Clear();
            _pending.MouseDelta = Vector2.Zero;
            _hasMousePosition = false;
        }
    }

    private static void Copy<T>(HashSet<T> source, HashSet<T> destination)
    {
        destination.Clear();
        foreach (T item in source) destination.Add(item);
    }

    private static void Validate(Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            throw new ArgumentOutOfRangeException(nameof(value), "Mouse coordinates must be finite.");
    }
}
