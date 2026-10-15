using System.Numerics;

namespace HybridSdfEngine.Textures;

/// <summary>A scalar map from one channel of an RGB texture, e.g. a packed data map.</summary>
public sealed class ChannelTexture : ITexture<float>
{
    public ITexture<Vector3> Source { get; }
    public TextureChannel Channel { get; }

    public ChannelTexture(ITexture<Vector3> source, TextureChannel channel = TextureChannel.Red)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));
        Source = source;
        Channel = channel;
    }

    public float Sample(in TextureContext context)
    {
        Vector3 value = Source.Sample(context);
        return Channel switch { TextureChannel.Red => value.X, TextureChannel.Green => value.Y, _ => value.Z };
    }
}
