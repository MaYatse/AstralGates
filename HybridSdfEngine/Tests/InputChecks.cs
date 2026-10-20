using System.Numerics;
using HybridSdfEngine.Input;
using HybridSdfEngine.Render;
using SharpHook.Data;
using SharpHook.Testing;

internal static class InputChecks
{
    public static async Task RunAsync()
    {
        CheckTransitions();
        CheckMouse();
        CheckCamera();
        await CheckHookAsync();
        Console.WriteLine("PASS: input transitions, short taps, mouse deltas, camera movement/rotation, controls and hook lifecycle.");
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

    private static void CheckCamera()
    {
        var camera = new Camera();
        var state = new InputState();
        var controller = new FlyCameraController(camera);
        Near(camera.GetRayDirection(Vector2.Zero), Vector3.UnitZ, "Original central ray");
        Vector2 uv = new(0.4f, -0.3f);
        Near(camera.GetRayDirection(uv), Vector3.Normalize(new(uv, 1)), "Preserve original projection");
        state.SetKey(KeyCode.VcW, true);
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Position, Vector3.Zero, "Controls start disabled");
        state.SetKey(KeyCode.VcF1, true);
        controller.Update(state.ReadFrame(), 1);
        Check(controller.Enabled, "F1 enables controls");
        Near(camera.Position, Vector3.Zero, "Ignore stale input in the activation frame");
        state.SetKey(KeyCode.VcF1, false);
        controller.Update(state.ReadFrame(), 0.5f);
        Near(camera.Position, new(0, 0, 2), "Movement uses seconds");
        camera.Position = Vector3.Zero;
        state.SetKey(KeyCode.VcD, true);
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Position.Length(), controller.MoveSpeed, "Normalize diagonal movement");
        state.SetKey(KeyCode.VcS, true);
        state.SetKey(KeyCode.VcA, true);
        Vector3 before = camera.Position;
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Position, before, "Opposing directions cancel");

        state.Reset();
        camera.Position = Vector3.Zero;
        camera.SetRotation(MathF.PI / 2f, 0);
        state.SetKey(KeyCode.VcW, true);
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Position, Vector3.UnitX * controller.MoveSpeed, "Move in the camera's direction");
        Near(Vector3.Dot(camera.Right, camera.Forward), 0, "Orthogonal camera basis");
        Near(Vector3.Cross(camera.Forward, camera.Right), camera.Up, "Consistent camera basis");

        state.Reset();
        camera.SetRotation(0, 0);
        state.AddMouseDelta(new(100, 100));
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Yaw, 0, "Mouse look requires RMB");
        state.SetMouseButton(MouseButton.Button2, true);
        state.AddMouseDelta(new(1000, 1000));
        controller.Update(state.ReadFrame(), 1);
        Near(camera.Yaw, 0, "Ignore mouse delta before the initial grab");
        state.AddMouseDelta(new(100, -50));
        controller.Update(state.ReadFrame(), 0);
        Near(camera.Yaw, 100 * controller.MouseSensitivity, "Mouse yaw is independent of frame duration");
        Near(camera.Pitch, 50 * controller.MouseSensitivity, "Mouse up looks up");
        state.AddMouseDelta(new(0, -100000));
        controller.Update(state.ReadFrame(), 0);
        Check(camera.Pitch < MathF.PI / 2f && camera.Forward.Y > 0.99f, "Pitch limit avoids flipping");
        Near(camera.Forward.Length(), 1, "Unit forward after clamping");
        Near(camera.Up.Length(), 1, "Unit up after clamping");
        state.SetKey(KeyCode.VcEscape, true);
        controller.Update(state.ReadFrame(), 1);
        Check(!controller.Enabled, "Escape disables controls");
    }

    private static async Task CheckHookAsync()
    {
        var hook = new TestGlobalHook();
        await using var source = new SharpHookInputSource(hook);
        await source.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        hook.SimulateKeyPress(KeyCode.VcW);
        hook.SimulateMouseMovement(50, 60);
        hook.SimulateMousePress(MouseButton.Button2);
        var frame = source.State.ReadFrame();
        Check(frame.IsDown(KeyCode.VcW) && frame.WasPressed(KeyCode.VcW), "Hook keyboard adapter");
        Check(frame.IsDown(MouseButton.Button2), "Hook mouse adapter");
        hook.SimulateMouseMovement(70, 80);
        Near(source.State.ReadFrame().MouseDelta, new(20, 20), "Hook mouse movement adapter");
        hook.SimulateKeyRelease(KeyCode.VcW);
        Check(source.State.ReadFrame().WasReleased(KeyCode.VcW), "Hook release adapter");
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
    private static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(Vector3.DistanceSquared(actual, expected) < 1e-8f, message);
    private static void Near(float actual, float expected, string message) => Check(MathF.Abs(actual - expected) < 1e-5f, message);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
