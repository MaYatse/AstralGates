using System.Numerics;

namespace HybridSdfEngine.Textures;

/// <summary>Coordinates and a right-handed tangent frame at a surface hit.</summary>
public readonly record struct TextureContext(
    Vector3 WorldPosition,
    Vector3 LocalPosition,
    Vector3 GeometricNormal,
    Vector3 LocalNormal,
    Vector2 UV,
    Vector3 Tangent,
    Vector3 Bitangent)
{
    /// <summary>Fallback mapping for custom SDFs without an object-local coordinate system.</summary>
    public static TextureContext ForWorld(Vector3 point, Vector3 normal)
    {
        Vector3 tangent = CreateTangent(normal);
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        return new(point, point, normal, normal,
            new(Vector3.Dot(point, tangent), Vector3.Dot(point, bitangent)), tangent, bitangent);
    }

    public static Vector3 CreateTangent(Vector3 normal)
    {
        Vector3 reference = MathF.Abs(normal.Y) < 0.999f ? Vector3.UnitY : Vector3.UnitZ;
        return Vector3.Normalize(Vector3.Cross(normal, reference));
    }
}
