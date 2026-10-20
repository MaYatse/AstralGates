using System.Numerics;
using HybridSdfEngine.Materials;
using HybridSdfEngine.Render;
using HybridSdfEngine.SDF;
using HybridSdfEngine.SDF.Light;
using HybridSdfEngine.SDF.Objects;
using HybridSdfEngine.Textures;

internal static class MaterialChecks
{
    public static void Run()
    {
        CheckTextures();
        CheckImages();
        CheckImageLoading();
        CheckCoordinates();
        CheckRendering();
        Console.WriteLine("PASS: material sampling, UV/TBN transforms, image filtering/wrapping/sRGB, normal maps, emission, scalar maps and allocation-free shading.");
    }

    private static void CheckTextures()
    {
        TextureContext context = TextureContext.ForWorld(new(0.1f, 0, 0), Vector3.UnitY);
        var checker = new CheckerTexture<float>(0.2f, 0.8f);
        Near(checker.Sample(context), 0.2f, "Even checker cell");
        Near(checker.Sample(context with { LocalPosition = new(-0.1f, 0, 0) }), 0.8f, "Negative checker coordinates");
        var worldChecker = new CheckerTexture<float>(0.2f, 0.8f, space: TextureSpace.World);
        Near(worldChecker.Sample(context with { LocalPosition = new(-0.1f, 0, 0) }), 0.2f, "Explicit world mapping");
        var uvChecker = new CheckerTexture<float>(0.2f, 0.8f, 2f, TextureSpace.UV);
        Near(uvChecker.Sample(context with { UV = new(0.6f, 0.1f) }), 0.8f, "UV checker mapping");

        var channel = new ChannelTexture(new ConstantTexture<Vector3>(new(0.2f, 0.4f, 0.8f)), TextureChannel.Green);
        Near(channel.Sample(context), 0.4f, "Extract a scalar map channel");
        var triplanar = new TriplanarTexture(new UvTexture());
        context = context with { LocalPosition = new(1, 2, 3) };
        Near(triplanar.Sample(context), new(1, 3, 1), "Axis-aligned triplanar projection");
        context = context with { LocalNormal = Vector3.Normalize(Vector3.One) };
        Near(triplanar.Sample(context), new(5f / 3f, 7f / 3f, 1), "Normalized triplanar blend");
        Near(new TriplanarTexture(new UvTexture(), sharpness: 4096).Sample(context), new(5f / 3f, 7f / 3f, 1),
            "Stable triplanar weights at high sharpness");
        Invalid(() => new CheckerTexture<float>(0, 1, 0f));
        Invalid(() => new TriplanarTexture(new UvTexture(), space: TextureSpace.UV));
        Invalid(() => new ChannelTexture(null!));
    }

    private static void CheckImages()
    {
        TextureContext context = TextureContext.ForWorld(Vector3.Zero, Vector3.UnitY);
        Vector3[] pixels = [Vector3.Zero, Vector3.One];
        var nearest = new ImageTexture(2, 1, pixels, filter: TextureFilter.Nearest);
        pixels[0] = Vector3.UnitX;
        Near(nearest.Sample(context with { UV = new(0.25f, 0.5f) }), Vector3.Zero, "Image owns a snapshot of pixels");
        Near(nearest.Sample(context with { UV = new(-0.25f, 0.5f) }), Vector3.One, "Negative repeat coordinates");
        Near(nearest.Sample(context with { UV = new(1f, 0.5f) }), Vector3.Zero, "Repeat at UV=1");
        var clamped = new ImageTexture(2, 1, [Vector3.Zero, Vector3.One], addressMode: TextureAddressMode.Clamp);
        Near(clamped.Sample(context with { UV = Vector2.Zero }), Vector3.Zero, "Clamp left edge");
        Near(clamped.Sample(context with { UV = Vector2.One }), Vector3.One, "Clamp right edge");
        Near(clamped.Sample(context with { UV = new(0.5f) }), new(0.5f), "Bilinear midpoint");
        var bilinear = new ImageTexture(2, 2, [Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ]);
        Near(bilinear.Sample(context with { UV = new(0.5f) }), new(0.25f), "Four-texel bilinear average");

        var srgb = new ImageTexture(2, 1, [new(0.5f), Vector3.One], TextureColorSpace.Srgb,
            TextureAddressMode.Clamp);
        Near(srgb.Sample(context with { UV = new(0.25f, 0.5f) }), new(0.21404114f), "Decode sRGB at load time");
        Near(srgb.Sample(context with { UV = new(0.5f) }), new((0.21404114f + 1f) / 2), "Filter in linear space");
        var data = ImageTexture.FromRgba32(1, 1, [128, 64, 255, 0], TextureColorSpace.Linear);
        Near(data.Sample(context), new(128f / 255f, 64f / 255f, 1), "RGBA data maps preserve channels and ignore alpha");
        var singleton = new ImageTexture(1, 1, [new(0.3f)]);
        Near(singleton.Sample(context with { UV = new(-40, 90) }), new(0.3f), "Single-texel wrapping");
        Invalid(() => new ImageTexture(0, 1, []));
        Invalid(() => new ImageTexture(2, 1, [Vector3.One]));
        Invalid(() => ImageTexture.FromRgba32(1, 1, [0, 0, 0]));
    }

    private static void CheckImageLoading()
    {
        // 3x2, uncompressed 24-bit BMP: rows are stored bottom to top, channels are BGR.
        byte[] bmp = Convert.FromHexString(
            "424D4E000000000000003600000028000000030000000200000001001800" +
            "000000001800000000000000000000000000000000000000" +
            "FF4080FFFFFF0000000000000000FF00FF00FF0000000000");
        TextureContext context = TextureContext.ForWorld(Vector3.Zero, Vector3.UnitY);
        using var stream = new MemoryStream();
        stream.Write([1, 2, 3, 4]);
        stream.Write(bmp);
        stream.Position = 4;
        var srgb = ImageTexture.FromStream(stream);
        Check(stream.CanRead, "Image loading must leave the caller's stream open");
        Check(srgb.Width == 3 && srgb.Height == 2, "Decoded non-square image dimensions");
        Check(srgb.AddressMode == TextureAddressMode.Repeat && srgb.Filter == TextureFilter.Bilinear,
            "Default image sampling options");
        Near(srgb.Sample(context with { UV = new(1f / 6f, 0.75f) }),
            new(0.2158605f, 0.05126946f, 1), "Loaded colors are decoded from sRGB once");

        string path = Path.Combine(Path.GetTempPath(), $"AstralGates-Texture-{Guid.NewGuid():N}.bmp");
        try
        {
            File.WriteAllBytes(path, bmp);
            var linear = ImageTexture.FromFile(path, TextureColorSpace.Linear,
                TextureAddressMode.Clamp, TextureFilter.Nearest);
            Check(linear.Width == 3 && linear.Height == 2, "File decoding dimensions");
            Check(linear.AddressMode == TextureAddressMode.Clamp && linear.Filter == TextureFilter.Nearest,
                "Custom image sampling options");
            Vector3[] expected = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
                new(128f / 255f, 64f / 255f, 1), Vector3.One, Vector3.Zero];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 3; x++)
                    Near(linear.Sample(context with { UV = new((x + 0.5f) / 3, (y + 0.5f) / 2) }),
                        expected[y * 3 + x], "Decoded channel order and row orientation");
            using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally { File.Delete(path); }

        Invalid(() => ImageTexture.FromFile(" "));
        Invalid(() => ImageTexture.FromStream(null!));
        using var closed = new MemoryStream();
        closed.Close();
        Invalid(() => ImageTexture.FromStream(closed));
    }

    private static void CheckCoordinates()
    {
        var plane = new SdfPlane(new(1, 2, 3), Vector3.UnitY);
        Vector3 local = new(0.25f, 0, -0.75f);
        TextureContext original = plane.GetTextureContext(new Vector3(1, 2, 3) + local);
        Near(new Vector3(original.UV, 0), new(0.25f, 0.75f, 0), "Plane coordinates");
        Quaternion x = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.7f);
        Quaternion y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f);
        plane.Rotate(Vector3.UnitX, 0.7f);
        plane.Rotate(Vector3.UnitY, 0.4f);
        plane.Set(new(4, 5, 6));
        TextureContext moved = plane.GetTextureContext(new Vector3(4, 5, 6) + Vector3.Transform(Vector3.Transform(local, x), y));
        Near(moved.LocalPosition, local, "Plane local position survives multiple rotations");
        Near(new Vector3(moved.UV, 0), new(original.UV, 0), "Plane UV moves with object");
        Frame(moved);

        var box = new SdfBox(Vector3.Zero, new(1, 2, 3));
        foreach (Vector3 face in new Vector3[] {new(1, 0, 0), new(-1, 0, 0), new(0, 2, 0), new(0, -2, 0), new(0, 0, 3), new(0, 0, -3)})
        {
            TextureContext context = box.GetTextureContext(face);
            Near(new Vector3(context.UV, 0), new(0.5f, 0.5f, 0), "Box face center UV");
            Frame(context);
        }
        local = new(0.3f, 2, 1.2f);
        original = box.GetTextureContext(local);
        box.Rotate(Vector3.UnitY, 0.4f);
        box.Move(new(4, 5, 6));
        moved = box.GetTextureContext(new Vector3(4, 5, 6) + Vector3.Transform(local, y));
        Near(moved.LocalPosition, local, "Box local position");
        Near(new Vector3(moved.UV, 0), new(original.UV, 0), "Box UV moves with object");
        Frame(moved);

        var sphere = new SdfSphere(Vector3.Zero, 1);
        Near(new Vector3(sphere.GetTextureContext(Vector3.UnitX).UV, 0), new(0.5f, 0.5f, 0), "Sphere equator UV");
        Near(sphere.GetTextureContext(Vector3.UnitY).UV.Y, 0f, "Sphere north pole");
        Near(sphere.GetTextureContext(-Vector3.UnitY).UV.Y, 1f, "Sphere south pole");
        Frame(sphere.GetTextureContext(Vector3.UnitY));
        Frame(sphere.GetTextureContext(-Vector3.UnitY));
        original = sphere.GetTextureContext(Vector3.UnitX);
        sphere.Rotate(Vector3.UnitY, 0.4f);
        sphere.Move(new(2, 3, 4));
        moved = sphere.GetTextureContext(new Vector3(2, 3, 4) + Vector3.Transform(Vector3.UnitX, y));
        Near(new Vector3(moved.UV, 0), new(original.UV, 0), "Rotating a sphere rotates its texture");
        Frame(moved);
    }

    private static void CheckRendering()
    {
        var texture = new CountingTexture();
        var plane = new SdfPlane(Vector3.Zero, Vector3.UnitY, Vector3.UnitX)
        {
            Material = new DiffuseMaterial(texture)
        };
        var light = new SunLight(-Vector3.UnitY, Vector3.One, 1f);
        var renderer = new DiffuseRenderer(new Scene([plane], [light, light, light]));
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), Vector3.UnitZ * 3f, "Material replaces legacy color");
        Check(texture.Calls == 1, "Read texture once per hit, not once per light");

        var emissive = new DiffuseMaterial(Vector3.Zero)
        {
            Emission = new ConstantTexture<Vector3>(new(2, 1, 0.5f))
        };
        plane.Material = emissive;
        Near(new DiffuseRenderer(new Scene([plane])).RenderRay(Vector3.UnitY, -Vector3.UnitY), new(2, 1, 0.5f),
            "Emission works without lights");

        var material = new DiffuseMaterial(Vector3.One)
        {
            NormalMap = new ConstantTexture<Vector3>(new(0.8f, 0.5f, 0.9f))
        };
        plane.Material = material;
        renderer = new DiffuseRenderer(new Scene([plane], [light]));
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), new(0.8f), "Tangent-space normal affects diffuse lighting");
        material.NormalMap = new ConstantTexture<Vector3>(new(0.5f, 0.5f, 1));
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), Vector3.One, "Flat normal map preserves appearance");

        float grazing = 0.01f;
        material.NormalMap = new ConstantTexture<Vector3>(new((MathF.Sqrt(1 - grazing * grazing) + 1) / 2, 0.5f, (grazing + 1) / 2));
        var sdfPlane = new MappedPlane(material);
        renderer = new DiffuseRenderer(new Scene([sdfPlane], [light]));
        Near(renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY), new(grazing), "Shadow bias must use the geometric normal");

        material.NormalMap = null;
        for (int i = 0; i < 100; i++) renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) renderer.RenderRay(Vector3.UnitY, -Vector3.UnitY);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "Material shading must not allocate per ray");
    }

    private static void Frame(TextureContext context)
    {
        Near(context.Tangent.Length(), 1, "Unit tangent");
        Near(context.Bitangent.Length(), 1, "Unit bitangent");
        Near(Vector3.Dot(context.Tangent, context.GeometricNormal), 0, "Orthogonal tangent");
        Near(Vector3.Cross(context.Tangent, context.Bitangent), context.GeometricNormal, "Right-handed frame");
    }

    private static void Near(float actual, float expected, string message) => Near(new Vector3(actual), new(expected), message);
    private static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(float.IsFinite(actual.LengthSquared()) && Vector3.DistanceSquared(actual, expected) < 1e-8f,
            $"{message}: expected {expected}, got {actual}");
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Invalid(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new Exception("Invalid texture parameter was accepted");
    }

    private sealed class UvTexture : ITexture<Vector3>
    {
        public Vector3 Sample(in TextureContext context) => new(context.UV, 1);
    }
    private sealed class CountingTexture : ITexture<Vector3>
    {
        public int Calls { get; private set; }
        public Vector3 Sample(in TextureContext context) { Calls++; return Vector3.UnitZ; }
    }
    private sealed class MappedPlane(IMaterial material) : ISdfObject
    {
        public IMaterial Material => material;
        public float DistanceTo(Vector3 point) => point.Y;
        public Vector3 GetNormal(Vector3 point) => Vector3.UnitY;
        public void Move(Vector3 axis) => throw new NotSupportedException();
        public void Set(Vector3 position) => throw new NotSupportedException();
        public void Rotate(Vector3 axis, float angle) => throw new NotSupportedException();
    }
}
