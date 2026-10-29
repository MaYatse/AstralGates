using HybridSdfEngine.Input;
using HybridSdfEngine.Render;

namespace AstralGates;

/// <summary>Interactive quality settings; input bindings belong to the application.</summary>
internal sealed class RenderSettings
{
    public int Ssaa { get; private set; }
    public int ScalePercent { get; private set; } = 100;
    public bool SpatialDenoising { get; private set; }
    public RenderMode Mode { get; private set; } = RenderMode.Realtime;
    public bool DenoisingEnabled => SpatialDenoising && Mode == RenderMode.Bsdf;

    public void HandleInput(InputFrame input)
    {
        if (input.WasPressed(InputKey.F2)) Ssaa = Ssaa switch { 0 => 2, 2 => 4, _ => 0 };
        if (input.WasPressed(InputKey.F3)) ScalePercent = ScalePercent switch { 100 => 75, 75 => 50, _ => 100 };
        if (input.WasPressed(InputKey.F5)) Mode = Mode == RenderMode.Realtime ? RenderMode.Bsdf : RenderMode.Realtime;
        if (input.WasPressed(InputKey.F4) && Mode == RenderMode.Bsdf) SpatialDenoising = !SpatialDenoising;
    }

    public int Scale(int size) => Math.Max(1, (int)(((long)size * ScalePercent + 50) / 100));
}
