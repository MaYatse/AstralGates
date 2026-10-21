using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF.Tracing;
using HybridSdfEngine.Textures;

internal static class PrimitiveChecks
{
    public static void Run()
    {
        CheckCylinder();
        CheckCone();
        CheckTorus();
        CheckTriangle();
        CheckTransformsAndTextures();
        CheckSceneIntegration();
        CheckValidation();
        Console.WriteLine("PASS: cylinder, cone, torus and triangle distances, intersections, rigid transforms, UV/TBN, materials and shadows.");
    }

    private static void CheckCylinder()
    {
        var shape = new SdfCylinder(Vector3.Zero, 1, 4);
        Near(shape.DistanceTo(Vector3.Zero), -1, "Cylinder interior");
        Near(shape.DistanceTo(new(2, 3, 0)), MathF.Sqrt(2), "Cylinder outside rim distance");
        Near(shape.DistanceTo(new(0, 3, 0)), 1, "Cylinder cap distance");
        Hit(shape, new(0, 0, -3), Vector3.UnitZ, 2);
        Hit(shape, Vector3.Zero, Vector3.UnitX, 1);
        Hit(shape, new(0, 4, 0), -Vector3.UnitY, 2);
        Hit(shape, new(0, -4, 0), Vector3.UnitY, 2);
        Hit(shape, new(1, 0, -3), Vector3.UnitZ, 3); // Tangent.
        Hit(shape, new(0, 2, 0), Vector3.UnitX, 0); // On cap, parallel ray.
        Hit(shape, Vector3.UnitX, Vector3.UnitY, 0); // On side, parallel ray.
        Miss(shape, new(2, 0, -3), Vector3.UnitZ);
        Miss(shape, new(0, 3, -3), Vector3.UnitZ);
        Near3(shape.GetNormal(new(1, 0, 0)), Vector3.UnitX, "Cylinder side normal");
        Near3(shape.GetNormal(new(0, -2, 0)), -Vector3.UnitY, "Cylinder bottom normal");
    }

    private static void CheckCone()
    {
        var shape = new SdfCone(Vector3.Zero, 1, 2);
        Near(shape.DistanceTo(Vector3.Zero), -1f / MathF.Sqrt(5), "Cone interior distance to slope");
        Near(shape.DistanceTo(new(0, 2, 0)), 1, "Distance above cone apex");
        Near(shape.DistanceTo(new(0, -2, 0)), 1, "Cone below base");
        Near(shape.DistanceTo(new(0.5f, 0, 0)), 0, "Cone side");
        Hit(shape, new(0, 0, -3), Vector3.UnitZ, 2.5f);
        Hit(shape, Vector3.Zero, Vector3.UnitX, 0.5f);
        Hit(shape, new(0, 3, 0), -Vector3.UnitY, 2); // Apex.
        Hit(shape, new(0, -3, 0), Vector3.UnitY, 2); // Base.
        Hit(shape, new(0.5f, 0, -3), Vector3.UnitZ, 3); // Tangent.
        Hit(shape, new(0, -1, 0), Vector3.UnitX, 0);
        // This direction makes the quadratic coefficient zero: the linear root is still a hit.
        Hit(shape, new(0, 0, -1), Vector3.Normalize(new(0, -2, 1)), MathF.Sqrt(5) / 4f);
        Miss(shape, new(0, 2, -3), Vector3.UnitZ);
        Near3(shape.GetNormal(new(0.5f, 0, 0)), Vector3.Normalize(new(2, 1, 0)), "Cone slope normal");
        Near3(shape.GetNormal(new(0, -1, 0)), -Vector3.UnitY, "Cone base normal");
        Check(float.IsFinite(shape.GetNormal(new(0, 1, 0)).LengthSquared()), "Finite apex normal");
    }

    private static void CheckTorus()
    {
        var shape = new SdfTorus(Vector3.Zero, 2, 0.5f);
        Near(shape.DistanceTo(Vector3.Zero), 1.5f, "Torus hole is outside");
        Near(shape.DistanceTo(new(2, 0, 0)), -0.5f, "Torus tube interior");
        Near(shape.DistanceTo(new(2.5f, 0, 0)), 0, "Torus outer surface");
        Near(shape.DistanceTo(new(1.5f, 0, 0)), 0, "Torus inner surface");
        Near(shape.DistanceTo(new(2, 1, 0)), 0.5f, "Torus above tube");
        Near3(shape.GetNormal(new(1.5f, 0, 0)), -Vector3.UnitX, "Torus inward-facing hole normal");
        var tracer = new SceneTracer(new Scene([shape]));
        var hit = tracer.Trace(new(0, 0, -4), Vector3.UnitZ);
        Check(hit.Hit && ReferenceEquals(hit.ClosestObject, shape), "Torus SDF tracing");
        Near(hit.Dist, 1.5f, "Torus marched distance");
        Near(tracer.Trace(new(2, 0, 0), Vector3.UnitX).Dist, 0.5f, "Exit torus interior");
        Check(!tracer.Trace(new(0, 3, 0), -Vector3.UnitY, 6).Hit, "Ray through torus hole misses");
    }

    private static SdfTriangle Triangle() => new(Vector3.Zero, Vector3.Zero, new(2, 0, 0), new(0, 2, 0));

    private static void CheckTriangle()
    {
        var shape = Triangle();
        Near(shape.DistanceTo(new(0.5f, 0.5f, 2)), 2, "Triangle distance above face");
        Near(shape.DistanceTo(new(0.5f, 0.5f, -2)), 2, "Triangle unsigned distance below face");
        Near(shape.DistanceTo(new(2, 2, 0)), MathF.Sqrt(2), "Triangle edge distance");
        Near(shape.DistanceTo(new(-1, -1, 0)), MathF.Sqrt(2), "Triangle vertex distance");
        Hit(shape, new(0.5f, 0.5f, -3), Vector3.UnitZ, 3);
        Hit(shape, new(0.5f, 0.5f, 3), -Vector3.UnitZ, 3);
        Hit(shape, new(1, 1, -3), Vector3.UnitZ, 3); // Edge.
        Hit(shape, new(0, 0, -3), Vector3.UnitZ, 3); // Vertex.
        Hit(shape, new(0.5f, 0.5f, 0), Vector3.UnitX, 0);
        Hit(shape, new(-1, 0.5f, 0), Vector3.UnitX, 1); // Coplanar edge entry.
        Miss(shape, new(2, 2, -3), Vector3.UnitZ);
        Miss(shape, new(0.5f, 0.5f, 1), Vector3.UnitX);
        Miss(shape, new(-1, 0.5f, 0), -Vector3.UnitX);
        Near3(shape.GetNormal(Vector3.Zero), Vector3.UnitZ, "Triangle winding normal");
        Near3(new SdfTriangle(Vector3.Zero, shape.A, shape.C, shape.B).GetNormal(Vector3.Zero),
            -Vector3.UnitZ, "Reversing winding flips normal");
        Near2(shape.GetTextureContext(shape.A).UV, Vector2.Zero, "Triangle A UV");
        Near2(shape.GetTextureContext(shape.B).UV, Vector2.UnitX, "Triangle B UV");
        Near2(shape.GetTextureContext(shape.C).UV, Vector2.UnitY, "Triangle C UV");
    }

    private static void CheckTransformsAndTextures()
    {
        (SdfPrimitive Shape, Vector3 Surface)[] samples = [
            (new SdfCylinder(Vector3.Zero, 1, 2), new(1, 0.2f, 0)),
            (new SdfCylinder(Vector3.Zero, 1, 2), new(0.2f, 1, 0.3f)),
            (new SdfCone(Vector3.Zero, 1, 2), new(0.5f, 0, 0)),
            (new SdfCone(Vector3.Zero, 1, 2), new(0.3f, -1, 0.2f)),
            (new SdfCone(Vector3.Zero, 1, 2), new(0, 1, 0)),
            (new SdfTorus(Vector3.Zero, 2, 0.5f), new(2, 0.5f, 0)),
            (new SdfTorus(Vector3.Zero, 2, 0.5f), new(1.5f, 0, 0)),
            (Triangle(), new(0.5f, 0.5f, 0))];
        foreach (var (shape, surface) in samples)
        {
            var initial = shape.GetTextureContext(surface);
            Frame(initial);
            Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f)
                * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.7f);
            shape.Rotate(Vector3.UnitX * 3, 0.7f);
            shape.Rotate(Vector3.UnitY, 0.4f);
            shape.Move(new(2, 3, 4));
            shape.Set(new(4, 5, 6));
            Vector3 moved = shape.Position + Vector3.Transform(surface, rotation);
            Near(shape.DistanceTo(moved), 0, "Rigid transform preserves surface distance");
            var context = shape.GetTextureContext(moved);
            Near3(context.LocalPosition, surface, "Texture local coordinates follow transforms");
            Vector2 uvDifference = context.UV - initial.UV;
            if (shape is SdfTorus)
                uvDifference = new(MathF.IEEERemainder(uvDifference.X, 1f), MathF.IEEERemainder(uvDifference.Y, 1f));
            Near2(uvDifference, Vector2.Zero, $"UV follows transforms on {shape.GetType().Name} at {surface}");
            Near3(context.GeometricNormal, Vector3.Transform(initial.GeometricNormal, rotation), "Normal follows rotation");
            Frame(context);
        }

        var random = new Random(452);
        for (int i = 0; i < 150; i++)
        {
            Vector3 axis = Vector3.Normalize(new Vector3(random.NextSingle() + 0.1f, random.NextSingle() + 0.1f, random.NextSingle() + 0.1f));
            float angle = random.NextSingle() * MathF.Tau;
            Quaternion rotation = Quaternion.CreateFromAxisAngle(axis, angle);
            Vector3 position = new(random.NextSingle() * 10, random.NextSingle() * 10, random.NextSingle() * 10);
            SdfPrimitive[] shapes = [new SdfCylinder(position, 1, 2), new SdfCone(position, 1, 2),
                new SdfTriangle(position, Vector3.Zero, new(2, 0, 0), new(0, 2, 0))];
            foreach (var shape in shapes)
            {
                shape.Rotate(axis, angle);
                Vector3 localOrigin = shape is SdfTriangle ? new(0.5f, 0.5f, -3) : new(0, 0, -3);
                Vector3 origin = position + Vector3.Transform(localOrigin, rotation);
                Vector3 direction = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));
                Hit((IRayIntersectable)shape, origin, direction,
                    shape is SdfCylinder ? 2f : shape is SdfCone ? 2.5f : 3f);
            }
        }
    }

    private static void CheckSceneIntegration()
    {
        ISdfObject[] shapes = [new SdfCylinder(Vector3.Zero, 1, 2), new SdfCone(Vector3.Zero, 1, 2),
            new SdfTorus(Vector3.Zero, 2, 0.5f), Triangle()];
        foreach (var shape in shapes)
        {
            Vector3 origin = shape is SdfTriangle ? new(0.5f, 0.5f, -4) : new(0, 0, -4);
            var tracer = new SceneTracer(new Scene([new SdfPlane(new(0, 0, 10), -Vector3.UnitZ), shape]));
            var hit = tracer.Trace(origin, Vector3.UnitZ);
            Check(ReferenceEquals(hit.ClosestObject, shape), "New primitive occludes analytic background");
            Check(tracer.Occluded(origin, Vector3.UnitZ, hit.Dist + 0.1f), "New primitive casts shadows");
            Check(!tracer.Occluded(origin, Vector3.UnitZ, hit.Dist - 0.1f), "Obstacle beyond shadow segment does not block it");
            ((SdfPrimitive)shape).Material = new DiffuseMaterial(Vector3.Zero) { Emission = new ConstantTexture<Vector3>(new(0.2f, 0.4f, 0.8f)) };
            Near3(new DiffuseRenderer(tracer).RenderRay(origin, Vector3.UnitZ), new(0.2f, 0.4f, 0.8f), "Material works on new primitive");
            ((SdfPrimitive)shape).Material = new DiffuseMaterial(Vector3.One);
            Vector3 lightDirection = -shape.GetNormal(hit.CollisionPoint);
            var renderer = new DiffuseRenderer(new Scene([shape], [new SunLight(lightDirection, Vector3.One, 1)]));
            Vector3 lightingOrigin = shape is SdfTriangle ? new(0.5f, 0.5f, 4) : origin;
            Vector3 lightingDirection = shape is SdfTriangle ? -Vector3.UnitZ : Vector3.UnitZ;
            Check(renderer.RenderRay(lightingOrigin, lightingDirection).LengthSquared() > 0.5f, "Front side receives light without self-shadowing");
        }
    }

    private static void CheckValidation()
    {
        Invalid(() => new SdfCylinder(Vector3.Zero, 0, 1));
        Invalid(() => new SdfCylinder(Vector3.Zero, 1, float.NaN));
        Invalid(() => new SdfCone(Vector3.Zero, 1, -1));
        Invalid(() => new SdfTorus(Vector3.Zero, 1, 1));
        Invalid(() => new SdfTorus(Vector3.Zero, float.PositiveInfinity, 1));
        Invalid(() => new SdfTriangle(Vector3.Zero, Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 2));
        Invalid(() => new SdfCone(new(float.NaN, 0, 0), 1, 1));
        Invalid(() => new SdfCylinder(Vector3.Zero, 1, 1).Rotate(Vector3.Zero, 1));
    }

    private static void Hit(IRayIntersectable shape, Vector3 origin, Vector3 direction, float expected)
    {
        Check(shape.TryIntersect(origin, direction, 100, out float distance), "Expected primitive intersection");
        Near(distance, expected, "Primitive intersection distance");
        Near(shape.DistanceTo(origin + direction * distance), 0, "Intersection lies on surface");
        Check(shape.TryIntersect(origin, direction, distance, out _), "Inclusive maximum distance");
        if (distance > 0.01f)
            Check(!shape.TryIntersect(origin, direction, distance - 0.01f, out _), "Reject beyond maximum distance");
    }

    private static void Miss(IRayIntersectable shape, Vector3 origin, Vector3 direction) =>
        Check(!shape.TryIntersect(origin, direction, 100, out _), "Unexpected primitive intersection");
    private static void Frame(TextureContext context)
    {
        Near(context.GeometricNormal.Length(), 1, "Unit normal");
        Near(context.Tangent.Length(), 1, "Unit tangent");
        Near(context.Bitangent.Length(), 1, "Unit bitangent");
        Near(Vector3.Dot(context.GeometricNormal, context.Tangent), 0, "Normal orthogonal to tangent");
        Near3(Vector3.Cross(context.Tangent, context.Bitangent), context.GeometricNormal, "Right-handed TBN");
        Check(float.IsFinite(context.UV.X) && float.IsFinite(context.UV.Y), "Finite UV");
    }

    private static void Near(float actual, float expected, string message) =>
        Check(float.IsFinite(actual) && MathF.Abs(actual - expected) < 0.0003f, $"{message}: {actual} != {expected}");
    private static void Near2(Vector2 actual, Vector2 expected, string message) => Near(Vector2.Distance(actual, expected), 0, message);
    private static void Near3(Vector3 actual, Vector3 expected, string message) => Near(Vector3.Distance(actual, expected), 0, message);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid primitive parameters were accepted.");
    }
}
