using System.Numerics;
using HybridSdfEngine.Render;

namespace AstralGates;

/// <summary>Displays rendered pixels and the application's console status.</summary>
internal sealed class ConsoleRenderer : IDisposable
{
    private readonly ConsoleDisplay _display = new();

    public void Present(int renderWidth, int renderHeight, Vector3[] pixels, int width, int height)
    {
        _display.ColoredGraphic(renderWidth, renderHeight, pixels, width, height);
        _display.Render();
    }

    public void Dispose() => _display.Dispose();

    public static (int Width, int Height) GetResolution() =>
        (Math.Max(1, Console.WindowWidth - 1), Math.Max(1, Console.WindowHeight - 2) * 2);

    public void DrawInputStatus(int width, int height, Shader shader, bool enabled, int ssaa, int renderScale, RenderMode mode)
    {
        int row = (height + 1) / 2;
        if (Console.IsOutputRedirected || row >= Console.WindowHeight) return;
        string denoise = "BSDF only";
        if (mode == RenderMode.Bsdf) denoise = shader.SpatialDenoisingEnabled ? "Spatial" : "Off";
        string text = $"F5: {mode} | F3: {renderScale}% | F2 SSAA: {(ssaa == 0 ? "off" : ssaa.ToString())} | F4: {denoise} | CPU: {shader.ResolutionX}x{shader.ResolutionY} | Grid: {width}x{height} px ({width}x{row} cells) | Input: {(enabled ? "ON" : "OFF")} | F1 toggle | WASD move | Hold RMB to look | Esc pause | Ctrl+C exit";
        int available = Math.Min(width, Math.Max(0, Console.WindowWidth - 1));
        Console.SetCursorPosition(0, row);
        Console.Write(text[..Math.Min(text.Length, available)].PadRight(available));
    }
}
