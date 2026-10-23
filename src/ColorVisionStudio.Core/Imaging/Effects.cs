namespace ColorVisionStudio.Core.Imaging;

public enum ColorFilterKind { None, Deuteranopia, Protanopia, Tritanopia }

public static class Sanitize
{
    public static float Unit(float v) => float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0f;
}

/// <summary>
/// Color-vision SIMULATION using Machado, Oliveira and Fernandes (2009) matrices at severity 1.0,
/// applied in linear RGB. Strength s blends original and simulated colour: out = (1-s)*in + s*sim.
/// This is simulation, not compensation (daltonization); compensation is not implemented.
/// </summary>
public static class ColorFilterEffect
{
    static readonly float[] Prot = { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f };
    static readonly float[] Deut = { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f };
    static readonly float[] Trit = { 1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f };

    public static float[] Matrix(ColorFilterKind k) => k switch
    {
        ColorFilterKind.Protanopia => Prot,
        ColorFilterKind.Deuteranopia => Deut,
        ColorFilterKind.Tritanopia => Trit,
        _ => new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }
    };

    public static (float r, float g, float b) Apply(float r, float g, float b, ColorFilterKind kind, float strength)
    {
        strength = Sanitize.Unit(strength);
        if (kind == ColorFilterKind.None || strength == 0f) return (r, g, b);
        float lr = ColorSpace.ToLinear(r), lg = ColorSpace.ToLinear(g), lb = ColorSpace.ToLinear(b);
        var m = Matrix(kind);
        float sr = m[0] * lr + m[1] * lg + m[2] * lb;
        float sg = m[3] * lr + m[4] * lg + m[5] * lb;
        float sb = m[6] * lr + m[7] * lg + m[8] * lb;
        return (ColorSpace.ToSrgb(lr + (sr - lr) * strength),
                ColorSpace.ToSrgb(lg + (sg - lg) * strength),
                ColorSpace.ToSrgb(lb + (sb - lb) * strength));
    }
}

public static class VibranceEffect
{
    /// <summary>
    /// Software vibrance (NOT the GPU-driver setting). amount: 0..2, 1 = identity.
    /// Moves chroma toward/away from Rec.709 luma. For amount>1 the boost is attenuated for already
    /// saturated pixels (gain = 1 + (a-1)*(1-sat)) to limit clipping; grays are unchanged.
    /// </summary>
    public static (float r, float g, float b) Apply(float r, float g, float b, float amount)
    {
        amount = Math.Clamp(float.IsFinite(amount) ? amount : 1f, 0f, 2f);
        if (amount == 1f) return (r, g, b);
        float y = ColorSpace.Luma709(r, g, b);
        float gain = amount;
        if (amount > 1f)
        {
            float max = MathF.Max(r, MathF.Max(g, b)), min = MathF.Min(r, MathF.Min(g, b));
            float sat = max <= 1e-6f ? 0f : (max - min) / max;
            gain = 1f + (amount - 1f) * (1f - sat);
        }
        return (Math.Clamp(y + (r - y) * gain, 0, 1), Math.Clamp(y + (g - y) * gain, 0, 1), Math.Clamp(y + (b - y) * gain, 0, 1));
    }
}

public static class DimmingEffect
{
    /// <summary>out = in * (1 - dim) in encoded space; uniform scaling preserves channel ratios (hue). dim = 1 gives black.</summary>
    public static (float r, float g, float b) Apply(float r, float g, float b, float dim)
    {
        float k = 1f - Sanitize.Unit(dim);
        return (r * k, g * k, b * k);
    }
}

public static class BlurEffect
{
    /// <summary>Separable Gaussian, sigma = radius/2, kernel half-width ceil(2*radius), clamp-to-edge. Radius 0 = passthrough.</summary>
    public static FloatImage Apply(FloatImage src, float radius)
    {
        radius = float.IsFinite(radius) ? Math.Clamp(radius, 0f, 64f) : 0f;
        if (radius <= 0f || src.Width == 0 || src.Height == 0) return src.Clone();
        float sigma = radius / 2f; int half = (int)MathF.Ceiling(radius * 2f);
        var k = new float[half * 2 + 1]; float sum = 0;
        for (int i = -half; i <= half; i++) { k[i + half] = MathF.Exp(-(i * i) / (2 * sigma * sigma)); sum += k[i + half]; }
        for (int i = 0; i < k.Length; i++) k[i] /= sum;
        var tmp = new FloatImage(src.Width, src.Height); var dst = new FloatImage(src.Width, src.Height);
        Pass(src, tmp, k, half, true); Pass(tmp, dst, k, half, false);
        return dst;
    }

    static void Pass(FloatImage a, FloatImage b, float[] k, int half, bool horiz)
    {
        int w = a.Width, h = a.Height;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                for (int c = 0; c < 3; c++)
                {
                    float acc = 0;
                    for (int i = -half; i <= half; i++)
                    {
                        int sx = horiz ? Math.Clamp(x + i, 0, w - 1) : x;
                        int sy = horiz ? y : Math.Clamp(y + i, 0, h - 1);
                        acc += a.Data[(sy * w + sx) * 3 + c] * k[i + half];
                    }
                    b.Data[(y * w + x) * 3 + c] = acc;
                }
    }
}
