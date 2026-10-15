using System.Numerics;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.SDF;
using HybridSdfEngine.Render;

internal static class ExtendedLightChecks
{
    public static void Run()
    {
        CheckSpot();
        CheckArea();
        CheckIntegration();
        Console.WriteLine("PASS: spotlight falloff, area sampling/orientation, quadrature convergence, diffuse integration and validation.");
    }

    private static void CheckSpot()
    {
        var spot = new SpotLight(Vector3.Zero, new(0, 0, 3), Vector3.One, 8f);
        Near(spot.Sample(new(0, 0, 2)).Radiance, new(2), "Spotlight axis");
        Near(spot.Sample(new(0, 0, 4)).Radiance, new(0.5f), "Inverse-square falloff");
        Near(spot.Sample(new(0, 0, -2)).Radiance, Vector3.Zero, "Behind spotlight");
        Near(spot.Sample(new(2, 0, 0)).Radiance, Vector3.Zero, "Outside spotlight cone");
        Near(spot.Sample(Vector3.Zero).Radiance, Vector3.Zero, "Coincident spotlight sample");

        float cosine = (MathF.Cos(spot.InnerAngle) + MathF.Cos(spot.OuterAngle)) / 2f;
        Vector3 middle = new Vector3(MathF.Sqrt(1f - cosine * cosine), 0, cosine) * 2f;
        Near(spot.Sample(middle).Radiance, Vector3.One, "Smooth cone midpoint");
        Near(spot.Sample(new Vector3(MathF.Sin(spot.InnerAngle), 0, MathF.Cos(spot.InnerAngle)) * 2f).Radiance,
            new(2), "Inner boundary");
        Near(spot.Sample(new Vector3(MathF.Sin(spot.OuterAngle), 0, MathF.Cos(spot.OuterAngle)) * 2f).Radiance,
            Vector3.Zero, "Outer boundary");

        spot.SetConeAngles(0.4f, 0.4f);
        Near(spot.Sample(new(0, 0, 2)).Radiance, new(2), "Hard cone edge");
        spot.RayDirection = new(0, -float.MaxValue, 0);
        spot.Position = new(0, 2, 0);
        spot.Color = new(1, 0.5f, 0);
        spot.Intensity = 4f;
        Near(spot.Sample(Vector3.Zero).Radiance, new(1, 0.5f, 0), "Mutable spotlight parameters");

        Invalid(() => spot.RayDirection = Vector3.Zero);
        Invalid(() => spot.RayDirection = new(float.NaN, 0, 0));
        Invalid(() => spot.Position = new(float.PositiveInfinity, 0, 0));
        Invalid(() => spot.SetConeAngles(0.5f, 0.4f));
        Invalid(() => spot.SetConeAngles(0f, MathF.PI / 2f));
        Invalid(() => spot.OuterAngle = float.NaN);
        ILight light = spot;
        if (light.SampleCount != 1) throw new Exception("Spotlight must use one sample");
        Near(light.Sample(Vector3.Zero, 0).Radiance, spot.Sample(Vector3.Zero).Radiance, "Default indexed sampling");
        Invalid(() => light.Sample(Vector3.Zero, 1));
    }

    private static void CheckArea()
    {
        var area = new AreaLight(Vector3.Zero, Quaternion.Identity, new(2, 2), Vector3.One, 1f, 1);
        Near(area.Sample(new(0, 0, 2)).Radiance, Vector3.One, "Panel area and distance weighting");
        Near(area.Sample(new(0, 0, -2)).Radiance, Vector3.Zero, "Panel back face");
        Near(area.Sample(new(2, 0, 0)).Radiance, Vector3.Zero, "Grazing panel");
        Near(area.Sample(Vector3.Zero).Radiance, Vector3.Zero, "On panel sample");
        area.TwoSided = true;
        Near(area.Sample(new(0, 0, -2)).Radiance, Vector3.One, "Two-sided panel");
        area.TwoSided = false;

        area.SamplesPerAxis = 2;
        Vector3 point = new(0, 0, 3);
        Vector3 centroid = Vector3.Zero;
        for (int i = 0; i < area.SampleCount; i++)
        {
            LightSample sample = area.Sample(point, i);
            Vector3 position = point + sample.DirectionToLight * sample.Distance;
            Near(new(MathF.Abs(position.X), MathF.Abs(position.Y), position.Z), new(0.5f, 0.5f, 0), "Grid placement");
            centroid += position;
        }
        Near(centroid / area.SampleCount, area.Position, "Centered sample grid");

        // Rotate a nonsquare panel, its receiving point, and all sample directions together.
        area.Size = new(2, 1);
        LightSample[] original = new LightSample[area.SampleCount];
        for (int i = 0; i < original.Length; i++) original[i] = area.Sample(point, i);
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new(1, 2, 3)), 0.7f);
        area.Rotation = new(rotation.X * 3, rotation.Y * 3, rotation.Z * 3, rotation.W * 3);
        area.Position = new(2, 3, 4);
        Vector3 movedPoint = area.Position + Vector3.Transform(point, rotation);
        for (int i = 0; i < original.Length; i++)
        {
            LightSample moved = area.Sample(movedPoint, i);
            Near(moved.Radiance, original[i].Radiance, "Radiance under rigid transform");
            Near(moved.DirectionToLight, Vector3.Transform(original[i].DirectionToLight, rotation), "Rotated sample directions");
        }

        Invalid(() => area.Size = new(0, 1));
        Invalid(() => area.Size = new(float.PositiveInfinity, 1));
        Invalid(() => area.Rotation = new(0, 0, 0, 0));
        Invalid(() => area.Rotation = new(float.NaN, 0, 0, 1));
        Invalid(() => area.SamplesPerAxis = 0);
        Invalid(() => area.SamplesPerAxis = 33);
        Invalid(() => area.Sample(point, -1));
        Invalid(() => area.Sample(point, area.SampleCount));
    }

    private static void CheckIntegration()
    {
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var spot = new SpotLight(new(0, 2, 0), -Vector3.UnitY, Vector3.One, 8f);
        var scene = new DiffuseRenderer(new Scene([plane], [spot]));
        Near(scene.RenderRay(Vector3.UnitY, -Vector3.UnitY), new(2), "Spotlight diffuse integration");

        var area = new AreaLight(new(0, 1, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f),
            new(2, 2), Vector3.One, 1f, 1);
        scene = new DiffuseRenderer(new Scene([plane], [area]));
        // Independent dense integration: cos(receiver) * cos(emitter) / distance squared.
        const int resolution = 256;
        double reference = 0;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            double u = (x + 0.5) * 2 / resolution - 1;
            double v = (y + 0.5) * 2 / resolution - 1;
            double distanceSquared = 1 + u * u + v * v;
            reference += 4 / (distanceSquared * distanceSquared * resolution * resolution);
        }

        double previousError = double.PositiveInfinity;
        foreach (int samples in new[] {1, 2, 4, 8, 16})
        {
            area.SamplesPerAxis = samples;
            Vector3 result = scene.RenderRay(new(0, 0.5f, 0), -Vector3.UnitY);
            double error = Math.Abs(result.X - reference);
            if (!float.IsFinite(result.X) || error >= previousError)
                throw new Exception("Area quadrature must converge toward independent integration without changing total energy");
            previousError = error;
        }
        if (previousError > 0.002) throw new Exception("Area quadrature did not converge");

        area.SamplesPerAxis = 2;
        area.Color = new(0.2f, 0.5f, 1);
        Vector3 first = scene.RenderRay(new(0, 0.5f, 0), -Vector3.UnitY);
        area.Intensity = 2f;
        Near(scene.RenderRay(new(0, 0.5f, 0), -Vector3.UnitY), first * 2, "Mutable panel intensity");
    }

    private static void Near(Vector3 actual, Vector3 expected, string message)
    {
        if (!float.IsFinite(actual.LengthSquared()) || Vector3.DistanceSquared(actual, expected) > 1e-8f)
            throw new Exception($"{message}: expected {expected}, got {actual}");
    }

    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid light parameter was accepted");
    }
}
