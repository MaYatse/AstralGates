using System.Numerics;

namespace HybridSdfEngine.Textures;

/// <summary>Blends three planar color projections; not a tangent-space normal-map projection.</summary>
public sealed class TriplanarTexture : ITexture<Vector3>
{
    public ITexture<Vector3> Source { get; }
    public float Scale { get; }
    public float Sharpness { get; }
    public TextureSpace Space { get; }

    public TriplanarTexture(ITexture<Vector3> source, float scale = 1f, float sharpness = 4f,
        TextureSpace space = TextureSpace.Local)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!float.IsFinite(scale) || scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
        if (!float.IsFinite(sharpness) || sharpness <= 0f) throw new ArgumentOutOfRangeException(nameof(sharpness));
        if (space != TextureSpace.Local && space != TextureSpace.World) throw new ArgumentOutOfRangeException(nameof(space));
        Source = source;
        Scale = scale;
        Sharpness = sharpness;
        Space = space;
    }

    public Vector3 Sample(in TextureContext context)
    {
        Vector3 point = Space == TextureSpace.Local ? context.LocalPosition : context.WorldPosition;
        Vector3 normal = Vector3.Abs(Space == TextureSpace.Local ? context.LocalNormal : context.GeometricNormal);
        float maximum = MathF.Max(normal.X, MathF.Max(normal.Y, normal.Z));
        if (maximum <= 0f) return Source.Sample(context);
        normal /= maximum;
        Vector3 weights = new(MathF.Pow(normal.X, Sharpness), MathF.Pow(normal.Y, Sharpness), MathF.Pow(normal.Z, Sharpness));
        weights /= weights.X + weights.Y + weights.Z;
        Vector3 x = Source.Sample(context with { UV = new Vector2(point.Z, point.Y) * Scale });
        Vector3 y = Source.Sample(context with { UV = new Vector2(point.X, point.Z) * Scale });
        Vector3 z = Source.Sample(context with { UV = new Vector2(point.X, point.Y) * Scale });
        return x * weights.X + y * weights.Y + z * weights.Z;
    }
}
