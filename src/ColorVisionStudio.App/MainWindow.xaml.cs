using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVisionStudio.Core.Configuration;
using ColorVisionStudio.Core.Imaging;

namespace ColorVisionStudio.App;

public partial class MainWindow : Window
{
    const int W = 480, H = 360;
    readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVisionStudio", "settings.json");
    readonly FloatImage _src = TestPattern();
    readonly WriteableBitmap _bmp = new(W, H, 96, 96, PixelFormats.Bgr24, null);
    readonly byte[] _buf = new byte[W * H * 3];
    readonly List<Action> _syncUi = new();
    EffectSettings _s;
    bool _ready;

    public MainWindow()
    {
        InitializeComponent();
        Preview.Source = _bmp;
        _s = SettingsStore.Load(_path, out var warn).Effects;
        BuildPanel();
        Closing += (_, _) => { try { SettingsStore.Save(_path, new SettingsFile { Effects = _s }); } catch { } };
        _ready = true;
        Render();
        if (warn != null) Status.Text = warn;
    }

    void BuildPanel()
    {
        Panel.Children.Add(new TextBlock { Text = "ColorVision Studio", FontSize = 20, FontWeight = FontWeights.SemiBold });
        Panel.Children.Add(new TextBlock { Text = "Live preview of filters on a test image. Does not yet filter your desktop.", Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12) });

        var master = new CheckBox { Content = "Effects enabled (master)", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 12) };
        master.Checked += (_, _) => { _s.MasterEnabled = true; Render(); };
        master.Unchecked += (_, _) => { _s.MasterEnabled = false; Render(); };
        _syncUi.Add(() => master.IsChecked = _s.MasterEnabled);
        Panel.Children.Add(master);

        Panel.Children.Add(Label("Color-vision simulation"));
        var combo = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var n in new[] { "None (original)", "Deuteranopia", "Protanopia", "Tritanopia" }) combo.Items.Add(n);
        combo.SelectionChanged += (_, _) => { if (!_ready) return; _s.SelectedFilter = (ColorFilterKind)combo.SelectedIndex; Render(); };
        _syncUi.Add(() => combo.SelectedIndex = (int)_s.SelectedFilter);
        Panel.Children.Add(combo);

        Slider("Deuteranopia strength", 0, 100, 100, v => $"{v:0}%", () => _s.DeuteranopiaStrength * 100, v => _s.DeuteranopiaStrength = (float)(v / 100));
        Slider("Protanopia strength", 0, 100, 100, v => $"{v:0}%", () => _s.ProtanopiaStrength * 100, v => _s.ProtanopiaStrength = (float)(v / 100));
        Slider("Tritanopia strength", 0, 100, 100, v => $"{v:0}%", () => _s.TritanopiaStrength * 100, v => _s.TritanopiaStrength = (float)(v / 100));
        Slider("Digital vibrance (100% = original)", 0, 200, 100, v => $"{v:0}%", () => _s.Vibrance * 100, v => _s.Vibrance = (float)(v / 100));
        Slider("Dimming", 0, 100, 0, v => $"{v:0}%", () => _s.Dimming * 100, v => _s.Dimming = (float)(v / 100));
        Slider("Blur radius", 0, 20, 0, v => $"{v:0.0} px", () => _s.BlurRadius, v => _s.BlurRadius = (float)v);

        var reset = new Button { Content = "Reset all effects", Margin = new Thickness(0, 14, 0, 0), Padding = new Thickness(8, 6, 8, 6) };
        reset.Click += (_, _) => { _ready = false; _s = new EffectSettings(); foreach (var a in _syncUi) a(); _ready = true; Render(); };
        Panel.Children.Add(reset);
        foreach (var a in _syncUi) a();
    }

    static TextBlock Label(string t) => new() { Text = t, Margin = new Thickness(0, 6, 0, 2), Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xB9, 0xC2)) };

    void Slider(string label, double min, double max, double def, Func<double, string> fmt, Func<double> get, Action<double> set)
    {
        var value = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right };
        var head = new DockPanel(); head.Children.Add(value); head.Children.Add(Label(label));
        var sl = new Slider { Minimum = min, Maximum = max, SmallChange = (max - min) / 100, LargeChange = (max - min) / 10, Value = def };
        var rst = new Button { Content = "↺", Width = 26, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Reset to default" };
        rst.Click += (_, _) => sl.Value = def;
        sl.ValueChanged += (_, e) => { value.Text = fmt(e.NewValue); if (!_ready) return; set(e.NewValue); Render(); };
        _syncUi.Add(() => { sl.Value = get(); value.Text = fmt(sl.Value); });
        var row = new DockPanel(); DockPanel.SetDock(rst, Dock.Right); row.Children.Add(rst); row.Children.Add(sl);
        Panel.Children.Add(head); Panel.Children.Add(row);
    }

    void Render()
    {
        var o = Pipeline.Render(_src, _s);
        for (int i = 0; i < o.Data.Length; i += 3)
        {
            _buf[i] = (byte)(Math.Clamp(o.Data[i + 2], 0f, 1f) * 255f + .5f); // B
            _buf[i + 1] = (byte)(Math.Clamp(o.Data[i + 1], 0f, 1f) * 255f + .5f);
            _buf[i + 2] = (byte)(Math.Clamp(o.Data[i], 0f, 1f) * 255f + .5f);
        }
        _bmp.WritePixels(new Int32Rect(0, 0, W, H), _buf, W * 3, 0);
        Status.Text = _s.MasterEnabled ? "Preview of built-in test image (hue sweep, skin tones, RGB blocks, gray ramp, fine stripes)." : "Master switch off: original image shown.";
    }

    static (float, float, float) Hsv(float h, float s, float v)
    {
        h = (h % 1f) * 6f; int i = (int)h; float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        return i switch { 0 => (v, t, p), 1 => (q, v, p), 2 => (p, v, t), 3 => (p, q, v), 4 => (t, p, v), _ => (v, p, q) };
    }

    static FloatImage TestPattern()
    {
        var im = new FloatImage(W, H);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W; (float r, float g, float b) c;
                if (y < H * 0.30) c = Hsv(u, 1f, 1f);                                   // hue sweep
                else if (y < H * 0.45) c = Hsv(0.07f, 0.25f + 0.35f * u, 0.55f + 0.4f * u); // skin-tone-ish ramp
                else if (y < H * 0.60) c = u < 0.33f ? (0.85f, 0.1f, 0.1f) : u < 0.66f ? (0.1f, 0.7f, 0.15f) : (0.1f, 0.2f, 0.9f); // R/G/B blocks
                else if (y < H * 0.75) c = (u, u, u);                                   // gray ramp
                else c = (x / 3) % 2 == 0 ? (1f, 1f, 1f) : (0f, 0f, 0f);                // fine stripes
                int k = (y * W + x) * 3; im.Data[k] = c.r; im.Data[k + 1] = c.g; im.Data[k + 2] = c.b;
            }
        return im;
    }
}
