using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.SDF;

/// <summary>
/// A snapshot of scene membership. Objects and lights can be changed between frames;
/// adding or removing members requires a new scene and tracer.
/// </summary>
public sealed class Scene
{
    private readonly ISdfObject[] _objects;
    private readonly ILight[] _lights;

    public IReadOnlyList<ISdfObject> Objects { get; }
    public IReadOnlyList<ILight> Lights { get; }
    internal ReadOnlySpan<ISdfObject> ObjectSpan => _objects;
    internal ReadOnlySpan<ILight> LightSpan => _lights;

    public Scene(IEnumerable<ISdfObject> objects, IEnumerable<ILight>? lights = null)
    {
        ArgumentNullException.ThrowIfNull(objects);
        _objects = objects.ToArray();
        _lights = lights?.ToArray() ?? [];
        if (Array.Exists(_objects, obj => obj is null))
            throw new ArgumentException("Objects must not contain null entries.", nameof(objects));
        if (Array.Exists(_lights, light => light is null))
            throw new ArgumentException("Lights must not contain null entries.", nameof(lights));
        Objects = Array.AsReadOnly(_objects);
        Lights = Array.AsReadOnly(_lights);
    }
}
