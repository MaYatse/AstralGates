using LX;
using SDFEngine.Render;

namespace AstralGates;

internal class LXRenderer : Control
{
    readonly RenderPipeline RenderPipeline = new();
    readonly PictureBox image = new();
    readonly Shader shader;
    public LXRenderer()
    {
        shader = new(1920 / 2, 1080 / 2, RenderPipeline.ShDr);
        
        AddToRoot();
        image.Image = ToImg();

        image.Alignment = Alignment.Fill;
        image.AddToRoot();
    }

    Image ToImg()
    {
        
        byte[] bytes = shader.RenderRgbaToBytes();

        Image img = LX.Image.LoadFromRGBA(1920 / 2, 1080 / 2, bytes);
        return img;
    }
}
