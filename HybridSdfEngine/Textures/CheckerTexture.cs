using System.Numerics;

namespace HybridSdfEngine.Textures;

public sealed class CheckerTexture<T> : ITexture<T>
{
    public T First { get; }
    public T Second { get; }
    public Vector3 Frequency { get; }
    public TextureSpace Space { get; }

    public CheckerTexture(T first, T second, float frequency = 1f, TextureSpace space = TextureSpace.Local)
        : this(first, second, new Vector3(frequency), space) { }

    public CheckerTexture(T first, T second, Vector3 frequency, TextureSpace space = TextureSpace.Local)
    {
        if (!float.IsFinite(frequency.X) || !float.IsFinite(frequency.Y) || !float.IsFinite(frequency.Z) ||
            frequency.X <= 0f || frequency.Y <= 0f || frequency.Z <= 0f)
            throw new ArgumentOutOfRangeException(nameof(frequency));
        if (!Enum.IsDefined(space)) throw new ArgumentOutOfRangeException(nameof(space));
        First = first;
        Second = second;
        Frequency = frequency;
        Space = space;
    }

    public T Sample(in TextureContext context)
    {
        Vector3 point = Space switch
        {
            TextureSpace.UV => new(context.UV, 0f),
            TextureSpace.Local => context.LocalPosition,
            _ => context.WorldPosition
        };
        // Double intermediates keep large finite coordinates/frequencies from overflowing.
        double parity = Math.Floor((double)point.X * Frequency.X) % 2
            + Math.Floor((double)point.Y * Frequency.Y) % 2
            + Math.Floor((double)point.Z * Frequency.Z) % 2;
        return parity % 2 == 0 ? First : Second;
    }
}
