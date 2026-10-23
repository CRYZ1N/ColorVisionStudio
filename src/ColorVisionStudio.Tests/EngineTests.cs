using ColorVisionStudio.Core.Configuration;
using ColorVisionStudio.Core.Imaging;
using Xunit;

public class EngineTests
{
    static FloatImage Img(int w, int h, Func<int, int, (float, float, float)> f)
    { var i = new FloatImage(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) { var (r, g, b) = f(x, y); int k = (y * w + x) * 3; i.Data[k] = r; i.Data[k + 1] = g; i.Data[k + 2] = b; } return i; }

    [Theory] [InlineData(ColorFilterKind.Deuteranopia)] [InlineData(ColorFilterKind.Protanopia)] [InlineData(ColorFilterKind.Tritanopia)]
    public void ZeroStrengthIsIdentity(ColorFilterKind k)
    { var o = ColorFilterEffect.Apply(0.8f, 0.3f, 0.5f, k, 0f); Assert.Equal((0.8f, 0.3f, 0.5f), o); }

    [Theory] [InlineData(ColorFilterKind.Deuteranopia)] [InlineData(ColorFilterKind.Protanopia)] [InlineData(ColorFilterKind.Tritanopia)]
    public void GraysUnchangedAtFullStrength(ColorFilterKind k)
    { var (r, g, b) = ColorFilterEffect.Apply(0.5f, 0.5f, 0.5f, k, 1f); Assert.InRange(r, 0.49f, 0.51f); Assert.InRange(g, 0.49f, 0.51f); Assert.InRange(b, 0.49f, 0.51f); }

    [Fact] public void MidStrengthIsLinearBlendInLinearLight()
    {
        var full = ColorFilterEffect.Apply(1f, 0f, 0f, ColorFilterKind.Protanopia, 1f);
        var half = ColorFilterEffect.Apply(1f, 0f, 0f, ColorFilterKind.Protanopia, .5f);
        float expect = ColorSpace.ToSrgb((1f + ColorSpace.ToLinear(full.r)) / 2f);
        Assert.Equal(expect, half.r, 3);
    }
    [Fact] public void FiltersKeepSeparateStrengths()
    { var s = new EffectSettings { DeuteranopiaStrength = .7f, ProtanopiaStrength = .45f, TritanopiaStrength = .85f, SelectedFilter = ColorFilterKind.Protanopia };
      s.SelectedFilter = ColorFilterKind.Tritanopia; s.SelectedFilter = ColorFilterKind.Deuteranopia;
      Assert.Equal(.7f, s.ActiveFilterStrength); Assert.Equal(.45f, s.ProtanopiaStrength); Assert.Equal(.85f, s.TritanopiaStrength); }
    [Fact] public void InvalidValuesClamp()
    { var v = new EffectSettings { Dimming = float.NaN, Vibrance = 9f, BlurRadius = -3f, ProtanopiaStrength = 2f }.Validated();
      Assert.Equal(0f, v.Dimming); Assert.Equal(2f, v.Vibrance); Assert.Equal(0f, v.BlurRadius); Assert.Equal(1f, v.ProtanopiaStrength); }

    [Fact] public void Vibrance100IsIdentity() => Assert.Equal((0.2f, 0.6f, 0.9f), VibranceEffect.Apply(.2f, .6f, .9f, 1f));
    [Fact] public void Vibrance0IsGrayscale()
    { var (r, g, b) = VibranceEffect.Apply(.9f, .2f, .1f, 0f); Assert.Equal(r, g, 4); Assert.Equal(g, b, 4); }
    [Fact] public void VibranceKeepsGrayAndRange()
    { var (r, g, b) = VibranceEffect.Apply(.4f, .4f, .4f, 2f); Assert.Equal(.4f, r, 5); Assert.Equal(.4f, b, 5);
      foreach (var c in new[] { VibranceEffect.Apply(1f, 0f, 0f, 2f), VibranceEffect.Apply(0f, 0f, 0f, 2f) })
      { Assert.InRange(c.r, 0, 1); Assert.False(float.IsNaN(c.g)); } }
    [Fact] public void VibranceBoostIncreasesSaturation()
    { static float S((float r, float g, float b) c) => Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b));
      Assert.True(S(VibranceEffect.Apply(.6f, .5f, .4f, 1.5f)) > S((.6f, .5f, .4f))); }

    [Fact] public void Dimming()
    { Assert.Equal((.3f, .6f, .9f), DimmingEffect.Apply(.3f, .6f, .9f, 0f)); Assert.Equal((0f, 0f, 0f), DimmingEffect.Apply(.3f, .6f, .9f, 1f));
      var (r, g, b) = DimmingEffect.Apply(.2f, .4f, .8f, .5f); Assert.Equal(2f, g / r, 3); Assert.Equal(2f, b / g, 3); }

    [Fact] public void BlurZeroPreserves()
    { var i = Img(5, 5, (x, y) => (x / 5f, y / 5f, .5f)); Assert.Equal(i.Data, BlurEffect.Apply(i, 0).Data); }
    [Fact] public void BlurSpreadsAndKeepsSizeAndMean()
    { var i = Img(21, 21, (x, y) => x == 10 && y == 10 ? (1f, 1f, 1f) : (0f, 0f, 0f));
      var o = BlurEffect.Apply(i, 2f); Assert.Equal(21, o.Width); Assert.True(o.Data[(10 * 21 + 10) * 3] < 1f); Assert.True(o.Data[(10 * 21 + 12) * 3] > 0f);
      Assert.Equal(1f, o.Data.Where((_, k) => k % 3 == 0).Sum(), 2); }
    [Theory] [InlineData(1, 1)] [InlineData(1, 7)] [InlineData(3, 2)]
    public void BlurSmallImagesDontCrash(int w, int h) => Assert.Equal(w, BlurEffect.Apply(Img(w, h, (x, y) => (1f, 0f, 0f)), 8f).Width);

    [Fact] public void MasterOffAndIndividualToggles()
    { var src = Img(4, 4, (x, y) => (.8f, .3f, .2f));
      var s = new EffectSettings { SelectedFilter = ColorFilterKind.Deuteranopia, Vibrance = 1.5f, Dimming = .3f, BlurRadius = 2f, MasterEnabled = false };
      Assert.Equal(src.Data, Pipeline.Render(src, s).Data);
      s.MasterEnabled = true; s.DimmingEnabled = false; s.ColorFilterEnabled = false; s.VibranceEnabled = false; s.BlurEnabled = false;
      Assert.Equal(src.Data, Pipeline.Render(src, s).Data); }
    [Fact] public void RenderDoesNotMutateSourceOrAccumulate()
    { var src = Img(3, 3, (x, y) => (.7f, .2f, .3f)); var copy = src.Data.ToArray(); var s = new EffectSettings { Vibrance = 1.8f };
      var a = Pipeline.Render(src, s); var b = Pipeline.Render(src, s); Assert.Equal(copy, src.Data); Assert.Equal(a.Data, b.Data); }

    [Fact] public void SettingsRoundTripAndCorruption()
    { var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
      SettingsStore.Save(p, new SettingsFile { SelectedProfile = "X", Effects = new EffectSettings { Vibrance = 1.3f, SelectedFilter = ColorFilterKind.Tritanopia } });
      var l = SettingsStore.Load(p, out var w); Assert.Null(w); Assert.Equal(1.3f, l.Effects.Vibrance); Assert.Equal(ColorFilterKind.Tritanopia, l.Effects.SelectedFilter);
      File.WriteAllText(p, "{ not json"); l = SettingsStore.Load(p, out w); Assert.NotNull(w); Assert.Equal(1f, l.Effects.Vibrance);
      File.WriteAllText(p, "{\"Version\":99}"); SettingsStore.Load(p, out w); Assert.Contains("newer", w);
      File.WriteAllText(p, "{\"Effects\":{\"Dimming\":5}}"); Assert.Equal(1f, SettingsStore.Load(p, out _).Effects.Dimming);
      File.Delete(p); Assert.Equal("Natural", SettingsStore.Load(p, out w).SelectedProfile); Assert.Null(w); }
}
