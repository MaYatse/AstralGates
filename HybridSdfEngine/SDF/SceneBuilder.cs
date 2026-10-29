using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.SDF;

/// <summary>Edits scene membership between snapshots. Objects and lights remain shared references.</summary>
public sealed class SceneBuilder
{
    public List<ISdfObject> Objects { get; } = [];
    public List<ILight> Lights { get; } = [];

    public SceneBuilder() { }

    public SceneBuilder(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Objects.AddRange(scene.Objects);
        Lights.AddRange(scene.Lights);
    }

    /// <summary>Create a new membership snapshot. Recreate its tracer and renderers after membership changes.</summary>
    public Scene Build() => new(Objects, Lights);
}
