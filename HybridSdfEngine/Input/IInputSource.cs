namespace HybridSdfEngine.Input;

/// <summary>A source of keyboard and mouse state, independent of application controls.</summary>
public interface IInputSource : IAsyncDisposable
{
    /// <summary>Completes when the source stops, or faults if input acquisition fails.</summary>
    Task Completion { get; }

    /// <summary>Starts input acquisition and returns when the source is ready.</summary>
    Task StartAsync();

    /// <summary>Consumes pending transitions and mouse motion. Read once per update.</summary>
    InputFrame ReadFrame();
}
