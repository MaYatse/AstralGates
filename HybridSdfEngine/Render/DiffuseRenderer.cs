using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Tracing;

namespace HybridSdfEngine.Render;

/// <summary>Compatibility name for the BSDF renderer. Existing diffuse scenes retain their intensity convention.</summary>
public sealed class DiffuseRenderer : BsdfRenderer
{
    public DiffuseRenderer(Scene scene) : base(scene) { }
    public DiffuseRenderer(SceneTracer tracer) : base(tracer) { }
}
