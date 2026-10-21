using System.Numerics;
using AstralGates.Input;
using HybridSdfEngine.Input;
using HybridSdfEngine.Render;
using SharpHook.Data;

var camera = new Camera();
var state = new InputState();
var controller = new FlyCameraController(camera);
Near3(camera.GetRayDirection(Vector2.Zero), Vector3.UnitZ, "Original central ray");
Vector2 uv = new(0.4f, -0.3f);
Near3(camera.GetRayDirection(uv), Vector3.Normalize(new(uv, 1)), "Preserve original projection");
state.SetKey(KeyCode.VcW, true);
controller.Update(state.ReadFrame(), 1);
Near3(camera.Position, Vector3.Zero, "Controls start disabled");
state.SetKey(KeyCode.VcF1, true);
controller.Update(state.ReadFrame(), 1);
Check(controller.Enabled, "F1 enables controls");
Near3(camera.Position, Vector3.Zero, "Ignore stale input in the activation frame");
state.SetKey(KeyCode.VcF1, false);
controller.Update(state.ReadFrame(), 0.5f);
Near3(camera.Position, new(0, 0, 2), "Movement uses seconds");
camera.Position = Vector3.Zero;
state.SetKey(KeyCode.VcD, true);
controller.Update(state.ReadFrame(), 1);
Near(camera.Position.Length(), controller.MoveSpeed, "Normalize diagonal movement");
state.SetKey(KeyCode.VcS, true);
state.SetKey(KeyCode.VcA, true);
Vector3 before = camera.Position;
controller.Update(state.ReadFrame(), 1);
Near3(camera.Position, before, "Opposing directions cancel");

state.Reset();
camera.Position = Vector3.Zero;
camera.SetRotation(MathF.PI / 2f, 0);
state.SetKey(KeyCode.VcW, true);
controller.Update(state.ReadFrame(), 1);
Near3(camera.Position, Vector3.UnitX * controller.MoveSpeed, "Move in the camera's direction");
Near(Vector3.Dot(camera.Right, camera.Forward), 0, "Orthogonal camera basis");
Near3(Vector3.Cross(camera.Forward, camera.Right), camera.Up, "Consistent camera basis");

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
Console.WriteLine("PASS: application camera movement, projection, rotation, diagonal speed and control activation.");

static void Near3(Vector3 actual, Vector3 expected, string message) =>
    Check(Vector3.DistanceSquared(actual, expected) < 1e-8f, message);
static void Near(float actual, float expected, string message) => Check(MathF.Abs(actual - expected) < 1e-5f, message);
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
