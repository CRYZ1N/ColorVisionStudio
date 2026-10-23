namespace ColorVisionStudio.Core.Imaging;

/// <summary>Float RGB image, row-major, channels in [0,1], sRGB-encoded. Effects never mutate their input.</summary>
public sealed class FloatImage
{
    public int Width { get; }
    public int Height { get; }
    public float[] Data { get; } // length = W*H*3
    public FloatImage(int w, int h) { Width = w; Height = h; Data = new float[w * h * 3]; }
    public FloatImage Clone() { var c = new FloatImage(Width, Height); Array.Copy(Data, c.Data, Data.Length); return c; }
}

public static class ColorSpace
{
    public static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    public static float ToSrgb(float c)
    {
        c = Math.Clamp(c, 0f, 1f);
        return c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1f / 2.4f) - 0.055f;
    }
    public static float Luma709(float r, float g, float b) => 0.2126f * r + 0.7152f * g + 0.0722f * b;
}
