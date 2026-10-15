namespace HybridSdfEngine.Textures;

/// <summary>A spatial parameter source. Sampling must be safe for concurrent rendering.</summary>
public interface ITexture<out T>
{
    T Sample(in TextureContext context);
}

public enum TextureSpace { UV, Local, World }
public enum TextureColorSpace { Linear, Srgb }
public enum TextureAddressMode { Repeat, Clamp }
public enum TextureFilter { Nearest, Bilinear }
public enum TextureChannel { Red, Green, Blue }
