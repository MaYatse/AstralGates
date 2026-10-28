using System.Numerics;
using HybridSdfEngine.SDF.Objects;

namespace HybridSdfEngine.Render;

/// <summary>Primary-hit data for spatial filtering. Normals are unit vectors in world space; depth is ray distance.</summary>
public readonly record struct SurfaceGuide(float Depth, Vector3 Normal, Vector3 Albedo, ISdfObject? Object, bool Filterable);

/// <summary>Linear radiance and primary-hit guides collected during the original path traversal.</summary>
public readonly record struct RaySample(Vector3 Color, Vector3 DirectRadiance, SurfaceGuide Surface);
