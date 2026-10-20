using System.Numerics;
using SharpHook;

namespace HybridSdfEngine.Input;

/// <summary>Owns a global SharpHook hook. Start and dispose sequentially; only one may run per process.</summary>
public sealed class SharpHookInputSource : IAsyncDisposable
{
    private static readonly SemaphoreSlim GlobalHookSlot = new(1, 1);
    private readonly IGlobalHook _hook;
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _completion;
    private bool _ownsSlot, _disposed;

    public InputState State { get; } = new();
    public Task Completion => _completion ?? Task.CompletedTask;

    /// <summary>The optional hook is owned and disposed by this source.</summary>
    public SharpHookInputSource(IGlobalHook? hook = null)
    {
        _hook = hook ?? new EventLoopGlobalHook();
        _hook.HookEnabled += OnEnabled;
        _hook.HookDisabled += OnDisabled;
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        _hook.MousePressed += OnMousePressed;
        _hook.MouseReleased += OnMouseReleased;
        _hook.MouseMoved += OnMouseMoved;
        _hook.MouseDragged += OnMouseMoved;
        _hook.MouseMovedRelative += OnMouseMovedRelative;
        _hook.MouseDraggedRelative += OnMouseMovedRelative;
    }

    /// <summary>Returns once the native hook is ready, or propagates its startup error.</summary>
    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_completion is not null) throw new InvalidOperationException("This input source has already been started.");
        if (!await GlobalHookSlot.WaitAsync(0).ConfigureAwait(false))
            throw new InvalidOperationException("Only one global input source may run at a time.");
        _ownsSlot = true;
        _completion = _hook.RunAsync();
        Task ready = await Task.WhenAny(_started.Task, _completion).ConfigureAwait(false);
        await ready.ConfigureAwait(false);
        if (!_started.Task.IsCompleted)
            throw new InvalidOperationException("The input hook stopped before becoming ready.");
    }

    private void OnEnabled(object? sender, HookEventArgs args) => _started.TrySetResult();
    private void OnDisabled(object? sender, HookEventArgs args) => State.Reset();
    private void OnKeyPressed(object? sender, KeyboardHookEventArgs args) => State.SetKey(args.Data.KeyCode, true);
    private void OnKeyReleased(object? sender, KeyboardHookEventArgs args) => State.SetKey(args.Data.KeyCode, false);
    private void OnMousePressed(object? sender, MouseHookEventArgs args) => State.SetMouseButton(args.Data.Button, true);
    private void OnMouseReleased(object? sender, MouseHookEventArgs args) => State.SetMouseButton(args.Data.Button, false);
    private void OnMouseMoved(object? sender, MouseHookEventArgs args) => State.SetMousePosition(new Vector2(args.Data.X, args.Data.Y));
    private void OnMouseMovedRelative(object? sender, MouseHookEventArgs args) => State.AddMouseDelta(new Vector2(args.Data.X, args.Data.Y));

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _hook.Dispose();
            if (_completion is not null) await _completion.ConfigureAwait(false);
        }
        finally
        {
            State.Reset();
            if (_ownsSlot) GlobalHookSlot.Release();
        }
        GC.SuppressFinalize(this);
    }
}
