using System.Numerics;
using HybridSdfEngine.Input;
using SharpHook.Data;
using SharpHook.Testing;

internal static class InputChecks
{
    public static async Task RunAsync()
    {
        CheckTransitions();
        CheckUnboundKeys();
        CheckMouse();
        await CheckHookAsync();
        Console.WriteLine("PASS: general input transitions, short taps, mouse deltas and hook lifecycle.");
    }

    private static void CheckTransitions()
    {
        var state = new InputState();
        state.SetKey(KeyCode.VcW, true);
        var frame = state.ReadFrame();
        Check(frame.IsDown(KeyCode.VcW) && frame.WasPressed(KeyCode.VcW), "Key down and press");
        state.SetKey(KeyCode.VcW, false);
        Check(frame.IsDown(KeyCode.VcW) && !frame.WasReleased(KeyCode.VcW), "Callbacks must not change the current snapshot");
        frame = state.ReadFrame();
        Check(!frame.IsDown(KeyCode.VcW) && frame.WasReleased(KeyCode.VcW), "Key release");
        state.SetKey(KeyCode.VcA, true);
        state.SetKey(KeyCode.VcA, false);
        frame = state.ReadFrame();
        Check(frame.WasPressed(KeyCode.VcA) && frame.WasReleased(KeyCode.VcA) && !frame.IsDown(KeyCode.VcA), "Do not lose taps between frames");
        frame = state.ReadFrame();
        Check(!frame.WasPressed(KeyCode.VcA) && !frame.WasReleased(KeyCode.VcA), "Transitions last one frame");
        state.SetKey(KeyCode.VcD, true);
        state.ReadFrame();
        state.SetKey(KeyCode.VcD, true);
        frame = state.ReadFrame();
        Check(frame.IsDown(KeyCode.VcD) && !frame.WasPressed(KeyCode.VcD), "Ignore OS key repeat for press transitions");
        state.SetMouseButton(MouseButton.Button2, true);
        state.Reset();
        frame = state.ReadFrame();
        Check(!frame.IsDown(KeyCode.VcD) && frame.WasReleased(KeyCode.VcD), "Reset releases keyboard");
        Check(!frame.IsDown(MouseButton.Button2) && frame.WasReleased(MouseButton.Button2), "Reset releases mouse");
    }

    private static void CheckUnboundKeys()
    {
        var state = new InputState();
        KeyCode[] keys = [KeyCode.VcEnter, KeyCode.VcSpace, KeyCode.VcLeftControl, KeyCode.VcF1, KeyCode.VcEscape];
        foreach (KeyCode key in keys) state.SetKey(key, true);
        var frame = state.ReadFrame();
        foreach (KeyCode key in keys)
            Check(frame.IsDown(key) && frame.WasPressed(key), "All keys are data; there are no reserved control shortcuts");
        foreach (KeyCode key in keys) state.SetKey(key, false);
        frame = state.ReadFrame();
        foreach (KeyCode key in keys)
            Check(!frame.IsDown(key) && frame.WasReleased(key), "Arbitrary key release");
    }

    private static void CheckMouse()
    {
        var state = new InputState();
        state.SetMousePosition(new(400, 300));
        Near(state.ReadFrame().MouseDelta, Vector2.Zero, "First position establishes a baseline");
        state.SetMousePosition(new(410, 305));
        state.SetMousePosition(new(425, 290));
        var frame = state.ReadFrame();
        Near(frame.MouseDelta, new(25, -10), "Accumulate mouse events per frame");
        Near(frame.MousePosition, new(425, 290), "Last absolute position");
        Near(state.ReadFrame().MouseDelta, Vector2.Zero, "Consume delta exactly once");
        state.AddMouseDelta(new(4, -6));
        Near(state.ReadFrame().MouseDelta, new(4, -6), "Relative mouse backend");
        state.SetMouseButton(MouseButton.Button1, true);
        state.SetMouseButton(MouseButton.Button1, false);
        frame = state.ReadFrame();
        Check(frame.WasPressed(MouseButton.Button1) && frame.WasReleased(MouseButton.Button1), "Short mouse click");
        state.Reset();
        state.SetMousePosition(new(-2000, 1000));
        Near(state.ReadFrame().MouseDelta, Vector2.Zero, "Reset mouse baseline");
    }

    private static async Task CheckHookAsync()
    {
        var hook = new TestGlobalHook();
        await using var source = new SharpHookInputSource(hook);
        await source.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        hook.SimulateKeyPress(KeyCode.VcW);
        hook.SimulateMouseMovement(50, 60);
        hook.SimulateMousePress(MouseButton.Button2);
        IInputSource input = source;
        var frame = input.ReadFrame();
        Check(frame.IsDown(KeyCode.VcW) && frame.WasPressed(KeyCode.VcW), "Hook keyboard adapter");
        Check(frame.IsDown(MouseButton.Button2), "Hook mouse adapter");
        hook.SimulateMouseMovement(70, 80);
        Near(input.ReadFrame().MouseDelta, new(20, 20), "Hook mouse movement adapter");
        hook.SimulateKeyRelease(KeyCode.VcW);
        Check(input.ReadFrame().WasReleased(KeyCode.VcW), "Hook release adapter");
        await using (var second = new SharpHookInputSource(new TestGlobalHook()))
        {
            bool rejected = false;
            try { await second.StartAsync(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Reject a second simultaneous hook");
        }
        await source.DisposeAsync();
        Check(source.Completion.IsCompletedSuccessfully, "Disposal stops and awaits the hook");
        Check(!source.State.ReadFrame().IsDown(MouseButton.Button2), "Disposal releases held buttons");
        await using var replacement = new SharpHookInputSource(new TestGlobalHook());
        await replacement.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    public static async Task RunNativeSmokeAsync()
    {
        await using var source = new SharpHookInputSource();
        await source.StartAsync();
        await source.DisposeAsync();
        Check(source.Completion.IsCompletedSuccessfully, "Native hook must stop cleanly");
        Console.WriteLine("PASS: native SharpHook startup and shutdown; no keyboard or mouse events injected.");
    }

    private static void Near(Vector2 actual, Vector2 expected, string message) =>
        Check(Vector2.DistanceSquared(actual, expected) < 1e-8f, message);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
