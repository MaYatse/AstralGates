using System.Numerics;
using HybridSdfEngine.SDF.Light;

internal static class LightChecks
{
    public static void Run()
    {
        var sun = new SunLight(new(0, -2, 0), Vector3.One, 1f);
        ILight light = sun;
        light.Intensity = 0f;
        light.Color = new(0.25f, 0.5f, 1f);
        Near(light.Sample(Vector3.Zero).Radiance, Vector3.Zero, "Disabled sun");
        light.Intensity = 4f;
        Near(light.Color, new(0.25f, 0.5f, 1f), "Color survives zero intensity");
        Near(light.Sample(Vector3.Zero).Radiance, new(1, 2, 4), "Sun cache updates through ILight");
        Near(light.Sample(Vector3.One).DirectionToLight, Vector3.UnitY, "Color preserves direction");

        sun.RayDirection = new(3, 0, 0);
        Near(sun.RayDirection, Vector3.UnitX, "Normalized ray direction getter");
        Near(sun.Sample(Vector3.Zero).DirectionToLight, -Vector3.UnitX, "Direction points toward sun");
        sun.Color = Vector3.One;
        sun.Intensity = 2f;
        Near(sun.Sample(Vector3.Zero).Radiance, new(2), "Color and intensity update radiance");
        Near(sun.Sample(Vector3.Zero).DirectionToLight, -Vector3.UnitX, "Color and intensity preserve direction");
        if (light.Intensity != 2f || !float.IsPositiveInfinity(sun.Sample(Vector3.Zero).Distance))
            throw new Exception("Sun parameter updates must preserve infinite distance");

        var point = new PointLight(Vector3.UnitY, Vector3.One, 1f);
        light = point;
        point.Position = new(0, 0, 2);
        light.Color = new(1, 0.5f, 0.25f);
        light.Intensity = 8f;
        LightSample sample = light.Sample(Vector3.Zero);
        Near(sample.DirectionToLight, Vector3.UnitZ, "Point light movement");
        Near(sample.Radiance, new(2, 1, 0.5f), "Point light attenuation after edits");
        if (sample.Distance != 2f)
            throw new Exception("Point light distance must follow Position");

        Console.WriteLine("PASS: mutable light parameters, sun cache, direction and point light attenuation.");
    }

    private static void Near(Vector3 actual, Vector3 expected, string message)
    {
        if (!float.IsFinite(actual.LengthSquared()) || Vector3.DistanceSquared(actual, expected) > 1e-10f)
            throw new Exception(message);
    }
}
