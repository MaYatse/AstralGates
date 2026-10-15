using System.Numerics;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Tracing;

internal static class RayTracingChecks
{
    public static void Run()
    {
        var sphere = new SdfSphere(Vector3.Zero, 1f);
        Hit(sphere, new(0, 0, -3), Vector3.UnitZ, 2f);
        Hit(sphere, Vector3.Zero, Vector3.UnitX, 1f);
        Hit(sphere, new(1, 0, -3), Vector3.UnitZ, 3f); // Tangent.
        Hit(sphere, Vector3.UnitX, Vector3.UnitX, 0f);
        Check(!sphere.TryIntersect(new(0, 0, -3), -Vector3.UnitZ, 10f, out _), "Sphere behind ray");
        Check(!sphere.TryIntersect(new(0, 0, -3), Vector3.UnitZ, 1.9f, out _), "Sphere beyond limit");
        Check(sphere.TryIntersect(new(0, 0, -3), Vector3.UnitZ, 2f, out _), "Sphere exactly at limit");
        Hit(new SdfSphere(new(0, 0, 1000), 0.01f), Vector3.Zero, Vector3.UnitZ, 999.99f);

        var plane = new SdfPlane(Vector3.Zero, new(0, 2, 0));
        Near(plane.DistanceTo(new(0, 3, 0)), 3f, "Plane normal normalization");
        Hit(plane, Vector3.UnitY, -Vector3.UnitY, 1f);
        Hit(plane, Vector3.Zero, Vector3.UnitX, 0f);
        Check(!plane.TryIntersect(Vector3.UnitY, Vector3.UnitX, 10f, out _), "Parallel plane miss");
        Check(!plane.TryIntersect(Vector3.UnitY, Vector3.UnitY, 10f, out _), "Plane behind ray");
        Hit(plane, new(0, 1e-6f, 0), Vector3.Normalize(new(1, -1e-8f, 0)), 100f);
        plane.Rotate(Vector3.UnitX, MathF.PI / 2f);
        Hit(plane, new(0, 0, -3), Vector3.UnitZ, 3f);

        var box = new SdfBox(Vector3.Zero, Vector3.One);
        Hit(box, new(0, 0, -3), Vector3.UnitZ, 2f);
        Hit(box, Vector3.Zero, Vector3.UnitX, 1f);
        Hit(box, Vector3.UnitX, Vector3.UnitY, 0f); // Origin on a parallel face.
        Hit(box, new(1, 0, -3), Vector3.UnitZ, 2f);
        Check(!box.TryIntersect(new(2, 0, -3), Vector3.UnitZ, 10f, out _), "Box parallel slab miss");
        Check(!box.TryIntersect(new(0, 0, -3), -Vector3.UnitZ, 10f, out _), "Box behind ray");
        Check(!box.TryIntersect(new(0, 0, -3), Vector3.UnitZ, 1.9f, out _), "Box beyond limit");
        UnitNormal(box.GetNormal(new(1, 1, 0)), "Box edge normal");
        UnitNormal(box.GetNormal(Vector3.One), "Box corner normal");

        var random = new Random(123);
        for (int i = 0; i < 500; i++)
        {
            var center = new Vector3(random.NextSingle() * 10, random.NextSingle() * 10, random.NextSingle() * 10);
            var axis = Vector3.Normalize(new Vector3(0.3f + random.NextSingle(), 0.3f + random.NextSingle(), 0.3f + random.NextSingle()));
            float angle = random.NextSingle() * MathF.Tau;
            var rotation = Quaternion.CreateFromAxisAngle(axis, angle);
            var rotatedBox = new SdfBox(center, new(1, 2, 3));
            rotatedBox.Rotate(axis * 3f, angle); // Non-unit input axis must not distort geometry.
            var localOrigin = new Vector3(random.NextSingle() - 0.5f, random.NextSingle() - 0.5f, -5);
            var origin = center + Vector3.Transform(localOrigin, rotation);
            var direction = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, rotation));
            Hit(rotatedBox, origin, direction, 2f);
            UnitNormal(rotatedBox.GetNormal(origin + direction * 2f), "Rotated box normal");
            Hit(new SdfSphere(center, 1), center - direction * 5f, direction, 4f);
        }

        var nearSphere = new SdfSphere(new(0, 0, 3), 1);
        var backPlane = new SdfPlane(new(0, 0, 10), -Vector3.UnitZ);
        foreach (var scene in new[] { new SceneTracer(new Scene([backPlane, nearSphere])), new SceneTracer(new Scene([nearSphere, backPlane])) })
        {
            var hit = scene.Trace(Vector3.Zero, new(0, 0, 7));
            Check(ReferenceEquals(hit.ClosestObject, nearSphere), "Nearest analytic object independent of order");
            Near(hit.Dist, 2f, "Trace world-distance normalization");
            Near(Vector3.Distance(hit.CollisionPoint, new(0, 0, 2)), 0f, "Hit point consistency");
        }

        var sdfFront = new SdfOnly(nearSphere);
        var mixed = new SceneTracer(new Scene([backPlane, sdfFront]));
        var frontHit = mixed.Trace(Vector3.Zero, Vector3.UnitZ);
        Check(ReferenceEquals(frontHit.ClosestObject, sdfFront), "SDF occludes analytic plane");
        Near(frontHit.Dist, 2f, "SDF hit distance");
        Check(sdfFront.DistanceCalls == 2, "No second object scan after marching hit");

        var sdfBack = new SdfOnly(new SdfSphere(new(0, 0, 20), 1));
        var backHit = new SceneTracer(new Scene([sdfBack, backPlane])).Trace(Vector3.Zero, Vector3.UnitZ);
        Check(ReferenceEquals(backHit.ClosestObject, backPlane), "Analytic plane occludes SDF");
        Check(sdfBack.DistanceCalls == 1, "Stop marching at analytic distance");

        var sdfInside = new SdfOnly(sphere);
        Near(new SceneTracer(new Scene([sdfInside])).Trace(Vector3.Zero, Vector3.UnitX).Dist, 1f, "Exit from SDF interior");
        Check(!new SceneTracer(new Scene([sdfBack])).Trace(Vector3.Zero, Vector3.UnitZ, 10f).Hit, "SDF beyond range");
        Check(!new SceneTracer(new Scene([])).Trace(Vector3.Zero, Vector3.UnitZ).Hit, "Empty scene");

        var slow = new SdfOnly(nearSphere, 0.01f);
        var exhausted = new SceneTracer(new Scene([slow, backPlane])).Trace(Vector3.Zero, Vector3.UnitZ, maxSteps: 1);
        Check(exhausted.StepLimitReached && !exhausted.Hit, "Budget exhaustion must not expose unverified background");

        var exactOnly = new ExactOnly(nearSphere);
        Check(new SceneTracer(new Scene([exactOnly])).Trace(Vector3.Zero, Vector3.UnitZ).Hit, "Analytic path never calls DistanceTo");
        var moving = new SdfSphere(new(0, 0, 3), 1);
        var movingScene = new SceneTracer(new Scene([moving]));
        moving.Move(new(0, 0, 2));
        Near(movingScene.Trace(Vector3.Zero, Vector3.UnitZ).Dist, 4f, "Transforms remain visible after scene construction");

        Console.WriteLine("PASS: analytic primitives, 500 random rigid transforms, bounds/parallel/tangent/inside rays, mixed occlusion, SDF fallback, step limits, finite edge normals.");
    }

    private static void Hit(IRayIntersectable obj, Vector3 origin, Vector3 direction, float expected)
    {
        Check(obj.TryIntersect(origin, direction, 2000f, out float distance), "Expected intersection");
        Near(distance, expected, "Intersection distance");
        Near(obj.DistanceTo(origin + direction * distance), 0f, "Intersection is on surface");
    }

    private static void Near(float actual, float expected, string message) =>
        Check(float.IsFinite(actual) && MathF.Abs(actual - expected) <= 0.0002f, $"{message}: {actual} != {expected}");

    private static void UnitNormal(Vector3 normal, string message) => Near(normal.Length(), 1f, message);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private sealed class SdfOnly(ISdfObject shape, float factor = 1f) : ISdfObject
    {
        public int DistanceCalls { get; private set; }
        public float DistanceTo(Vector3 point) { DistanceCalls++; return shape.DistanceTo(point) * factor; }
        public void Move(Vector3 offset) => shape.Move(offset);
        public void Set(Vector3 position) => shape.Set(position);
        public void Rotate(Vector3 axis, float angle) => shape.Rotate(axis, angle);
    }

    private sealed class ExactOnly(IRayIntersectable shape) : IRayIntersectable
    {
        public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance, out float distance) =>
            shape.TryIntersect(origin, direction, maxDistance, out distance);
        public float DistanceTo(Vector3 point) => throw new Exception("Analytic object was marched");
        public void Move(Vector3 offset) => shape.Move(offset);
        public void Set(Vector3 position) => shape.Set(position);
        public void Rotate(Vector3 axis, float angle) => shape.Rotate(axis, angle);
    }
}
