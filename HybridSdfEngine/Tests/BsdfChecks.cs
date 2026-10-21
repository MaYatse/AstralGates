using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Materials.Scattering;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.Textures;

internal static class BsdfChecks
{
    private static readonly TextureContext Surface = TextureContext.ForWorld(Vector3.Zero, Vector3.UnitY);

    public static void Run()
    {
        CheckDiffuseAndMetal();
        CheckDielectrics();
        CheckSampling();
        CheckTransport();
        CheckTransmissiveShadows();
        CheckParametersAndAllocations();
        Console.WriteLine("PASS: BSDF evaluation/PDF/sampling, GGX reciprocity/energy, Fresnel/Snell/TIR, glass paths, nested media, tinted shadows and allocations.");
    }

    private static void CheckDiffuseAndMetal()
    {
        var diffuse = new DiffuseMaterial(new Vector3(0.8f, 0.4f, 0.2f)).Sample(Surface);
        var context = BsdfContext.Create(diffuse, Vector3.UnitY, Vector3.UnitY);
        Near(diffuse.Bsdf.Evaluate(diffuse, context, Vector3.UnitY), diffuse.Albedo / MathF.PI, "Normalized Lambertian");
        Near(diffuse.Bsdf.Pdf(diffuse, context, Vector3.UnitY), 1f / MathF.PI, "Cosine hemisphere PDF");
        Near(diffuse.Bsdf.Evaluate(diffuse, context, -Vector3.UnitY), Vector3.Zero, "BRDF does not transmit light");
        var back = BsdfContext.Create(diffuse, Vector3.UnitY, -Vector3.UnitY);
        Near(diffuse.Bsdf.Evaluate(diffuse, back, Vector3.UnitY), Vector3.Zero, "Opposite geometric hemispheres are not reflection");
        var ds = diffuse.Bsdf.Sample(diffuse, context, new(0.4f, 0.6f, 0.8f));
        Near(ds.Value * (Vector3.Dot(ds.Direction, Vector3.UnitY) / ds.Pdf), diffuse.Albedo, "Lambertian sample weight");

        var pbr = new PbrMaterial(Vector3.Zero) { Roughness = new ConstantTexture<float>(0.3f) };
        MaterialSample black = pbr.Sample(Surface);
        Check(black.Bsdf.Evaluate(black, context, Vector3.UnitY).X > 0f, "Black dielectric still has specular reflection");
        var mirror = new PbrMaterial(new Vector3(0.9f, 0.7f, 0.3f))
        {
            Roughness = new ConstantTexture<float>(0), Metallic = new ConstantTexture<float>(1)
        }.Sample(Surface);
        var ms = mirror.Bsdf.Sample(mirror, context, new(0.2f, 0.5f, 0.5f));
        Check(ms.IsValid && (ms.Flags & BsdfLobes.Specular) != 0, "Zero roughness metal is a delta mirror");
        Near(ms.Direction, Vector3.UnitY, "Mirror direction");
        Near(ms.Value / ms.Pdf, mirror.Albedo, "Mirror tint and energy");
        Near(mirror.Bsdf.Evaluate(mirror, context, Vector3.UnitY), Vector3.Zero, "Delta mirror is not a finite density");

        var rough = new PbrMaterial(new Vector3(0.7f)) { Metallic = new ConstantTexture<float>(0.6f) }.Sample(Surface);
        Vector3 wi = Vector3.Normalize(new(0.3f, 1, 0.2f)), wo = Vector3.Normalize(new(-0.2f, 1, 0.1f));
        var forward = BsdfContext.Create(rough, Vector3.UnitY, wo);
        var backward = BsdfContext.Create(rough, Vector3.UnitY, wi);
        Near(rough.Bsdf.Evaluate(rough, forward, wi), rough.Bsdf.Evaluate(rough, backward, wo), "GGX reflection reciprocity");
        var random = new Random(371);
        Vector3 energy = Vector3.Zero;
        const int count = 30000;
        for (int i = 0; i < count; i++)
        {
            float y = random.NextSingle(), phi = MathF.Tau * random.NextSingle();
            float r = MathF.Sqrt(1f - y * y);
            wi = new(r * MathF.Cos(phi), y, r * MathF.Sin(phi));
            energy += rough.Bsdf.Evaluate(rough, context, wi) * (y * MathF.Tau);
        }
        energy /= count;
        Check(energy.X > 0f && energy.X <= 1.02f && energy.Y <= 1.02f && energy.Z <= 1.02f, "Opaque hemispherical energy is bounded");
    }

    private static void CheckDielectrics()
    {
        var glass = new DielectricMaterial().Sample(Surface);
        var context = BsdfContext.Create(glass, Vector3.UnitY, Vector3.UnitY);
        var reflected = glass.Bsdf.Sample(glass, context, new(0.01f, 0.3f, 0.4f));
        Near(reflected.Pdf, 0.04f, "Normal-incidence Fresnel for IOR 1.5");
        Check((reflected.Flags & BsdfLobes.Reflection) != 0, "Fresnel reflection selected");
        Near(reflected.Direction, Vector3.UnitY, "Glass reflection direction");
        var transmitted = glass.Bsdf.Sample(glass, context, new(0.9f, 0.3f, 0.4f));
        Near(transmitted.Pdf, 0.96f, "Complementary transmission probability");
        Near(transmitted.Direction, -Vector3.UnitY, "Normal-incidence refraction");
        Near(transmitted.Value * (1f / transmitted.Pdf), new Vector3(1f / 2.25f), "Radiance eta-squared correction");

        Vector3 wo = Vector3.Normalize(new(0.6f, 0.8f, 0));
        context = BsdfContext.Create(glass, Vector3.UnitY, wo);
        transmitted = glass.Bsdf.Sample(glass, context, new(0.99f, 0.2f, 0.2f));
        Near(MathF.Abs(transmitted.Direction.X), 0.4f, "Snell's law");
        context = BsdfContext.Create(glass, Vector3.UnitY, Vector3.Normalize(new(0.9f, -0.4358899f, 0)));
        var tir = glass.Bsdf.Sample(glass, context, new(0.99f, 0.2f, 0.2f));
        Check(tir.IsValid && (tir.Flags & BsdfLobes.Reflection) != 0, "Total internal reflection");
        Near(tir.Pdf, 1, "TIR has unit reflection probability");

        var matched = new DielectricMaterial { IndexOfRefraction = new ConstantTexture<float>(1), Roughness = new ConstantTexture<float>(0.8f) }.Sample(Surface);
        context = BsdfContext.Create(matched, Vector3.UnitY, Vector3.UnitY);
        var pass = matched.Bsdf.Sample(matched, context, new(0.5f));
        Near(pass.Direction, -Vector3.UnitY, "Matched IOR passes straight through regardless of roughness");
        Near(pass.Value / pass.Pdf, Vector3.One, "Matched IOR preserves radiance");

        var rough = new DielectricMaterial { Roughness = new ConstantTexture<float>(0.4f) }.Sample(Surface);
        var enter = BsdfContext.Create(rough, Vector3.UnitY, Vector3.UnitY);
        var exit = BsdfContext.Create(rough, Vector3.UnitY, -Vector3.UnitY);
        Vector3 ft = rough.Bsdf.Evaluate(rough, enter, -Vector3.UnitY);
        Vector3 reverse = rough.Bsdf.Evaluate(rough, exit, Vector3.UnitY);
        Check(ft.X > 0f, "Rough glass has a finite BTDF");
        Near(ft * 2.25f, reverse, "Transmission reciprocity includes eta squared", 0.003f);
    }

    private static void CheckSampling()
    {
        var random = new Random(713);
        IMaterial[] materials = [new DiffuseMaterial(), new PbrMaterial(),
            new PbrMaterial { Metallic = new ConstantTexture<float>(1) },
            new DielectricMaterial { Roughness = new ConstantTexture<float>(0.5f) }];
        foreach (IMaterial material in materials)
        {
            MaterialSample m = material.Sample(Surface);
            var c = BsdfContext.Create(m, Vector3.UnitY, Vector3.Normalize(new(0.3f, 0.8f, 0.2f)));
            int accepted = 0, reflected = 0, transmitted = 0;
            for (int i = 0; i < 3000; i++)
            {
                var sample = m.Bsdf.Sample(m, c, new(random.NextSingle(), random.NextSingle(), random.NextSingle()));
                if (!sample.IsValid) continue;
                accepted++;
                if ((sample.Flags & BsdfLobes.Transmission) != 0) transmitted++;
                else reflected++;
                Near(sample.Direction.Length(), 1, "Unit sampled direction");
                Near(sample.Pdf, m.Bsdf.Pdf(m, c, sample.Direction), "Sample PDF matches evaluation", 0.002f);
                Near(sample.Value, m.Bsdf.Evaluate(m, c, sample.Direction), "Sample BSDF matches evaluation", 0.002f);
                Check(sample.Value.X >= 0 && sample.Value.Y >= 0 && sample.Value.Z >= 0, "Nonnegative BSDF");
            }
            Check(accepted > 1500, "Useful BSDF acceptance rate");
            if (material is DielectricMaterial) Check(transmitted > 0 && reflected > 0, "Rough BSDF samples reflection and transmission");
        }
    }

    private static void CheckTransport()
    {
        var mirror = new SdfPlane(Vector3.Zero, Vector3.UnitY)
        {
            Material = new PbrMaterial { Roughness = new ConstantTexture<float>(0), Metallic = new ConstantTexture<float>(1) }
        };
        var emitter = new SdfSphere(new(0, 3, 0), 0.5f) { Material = Emissive(new(0.7f, 0.3f, 0.1f)) };
        var renderer = new BsdfRenderer(new Scene([mirror, emitter]));
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), new(0.7f, 0.3f, 0.1f), "Mirror sees secondary emissive object");
        renderer.MaxBounces = 0;
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), Vector3.Zero, "Bounce limit disables reflection");
        var glass = new SdfSphere(Vector3.Zero, 1) { Material = new DielectricMaterial() };
        renderer = new BsdfRenderer(new Scene([glass])) { BackgroundColor = new(0.4f, 0.6f, 0.8f), SampleCount = 256, MaxBounces = 16 };
        Near(renderer.RenderRay(new(0, 0, -3), Vector3.UnitZ), renderer.BackgroundColor, "Closed clear glass preserves a constant environment", 0.015f);
        var nested = new SdfSphere(Vector3.Zero, 0.5f)
        {
            Material = new DielectricMaterial { IndexOfRefraction = new ConstantTexture<float>(1.33f) }
        };
        renderer = new BsdfRenderer(new Scene([glass, nested])) { BackgroundColor = new(0.4f, 0.6f, 0.8f), SampleCount = 256, MaxBounces = 24 };
        Near(renderer.RenderRay(new(0, 0, -3), Vector3.UnitZ), renderer.BackgroundColor, "Nested closed media IOR stack", 0.02f);
        renderer = new BsdfRenderer(new Scene([new SdfPlane(Vector3.Zero, Vector3.UnitY)]))
        {
            BackgroundColor = Vector3.One, TraceDiffuseBounces = true
        };
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), Vector3.One, "Diffuse environment bounce");
        renderer.TraceDiffuseBounces = false;
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), Vector3.Zero, "Original direct-only diffuse policy");
    }

    private static void CheckTransmissiveShadows()
    {
        var floor = new SdfPlane(Vector3.Zero, Vector3.UnitY);
        var glass = new SdfSphere(new(0, 2, 0), 0.5f) { Material = new DielectricMaterial(new Vector3(0.5f, 1, 1)) };
        var renderer = new BsdfRenderer(new Scene([floor, glass], [new SunLight(-Vector3.UnitY, Vector3.One, 1)]));
        Vector3 lit = renderer.RenderRay(new(2, 1, 0), Vector3.Normalize(new(-2, -1, 0)));
        Near(lit, new Vector3(0.25f, 1, 1) * (0.96f * 0.96f), "Two glass boundaries produce a tinted Fresnel shadow", 0.001f);
        renderer.TransparentShadowsEnabled = false;
        Near(renderer.RenderRay(new(2, 1, 0), Vector3.Normalize(new(-2, -1, 0))), Vector3.Zero, "Opaque shadow option");
        glass.Material = new DiffuseMaterial();
        renderer.TransparentShadowsEnabled = true;
        Near(renderer.RenderRay(new(2, 1, 0), Vector3.Normalize(new(-2, -1, 0))), Vector3.Zero, "Material changes update shadow policy");
    }

    private static void CheckParametersAndAllocations()
    {
        var pbr = new PbrMaterial { Roughness = new ConstantTexture<float>(2), Metallic = new ConstantTexture<float>(-1) };
        var m = pbr.Sample(Surface);
        Near(m.Roughness, 1, "Roughness range");
        Near(m.Metallic, 0, "Metallic range");
        Invalid(() => pbr.IndexOfRefraction = 0);
        Invalid(() => new PbrMaterial { Roughness = new ConstantTexture<float>(float.NaN) }.Sample(Surface));
        Invalid(() => new DiffuseMaterial(new Vector3(float.PositiveInfinity)).Sample(Surface));
        Invalid(() => new DielectricMaterial { IndexOfRefraction = new ConstantTexture<float>(float.NaN) }.Sample(Surface));
        var shape = new SdfSphere(Vector3.Zero, 1) { Material = new DielectricMaterial() };
        var renderer = new BsdfRenderer(new Scene([shape])) { BackgroundColor = Vector3.One };
        for (int i = 0; i < 100; i++) renderer.RenderRay(new(0, 0, -3), Vector3.UnitZ);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++) renderer.RenderRay(new(0, 0, -3), Vector3.UnitZ);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "BSDF transport must not allocate per ray");
    }

    private static DiffuseMaterial Emissive(Vector3 color) => new(Vector3.Zero) { Emission = new ConstantTexture<Vector3>(color) };
    private static void Near(float actual, float expected, string message, float epsilon = 0.0001f) =>
        Check(float.IsFinite(actual) && MathF.Abs(actual - expected) <= epsilon, $"{message}: {actual} != {expected}");
    private static void Near(Vector3 actual, Vector3 expected, string message, float epsilon = 0.0001f)
    {
        Near(actual.X, expected.X, message, epsilon);
        Near(actual.Y, expected.Y, message, epsilon);
        Near(actual.Z, expected.Z, message, epsilon);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid BSDF parameter accepted.");
    }
}
