namespace HybridSdfEngine.Textures;

public sealed class ConstantTexture<T>(T value) : ITexture<T>
{
    /// <summary>Update between frames, never during parallel rendering.</summary>
    public T Value { get; set; } = value;
    public T Sample(in TextureContext context) => Value;
}
