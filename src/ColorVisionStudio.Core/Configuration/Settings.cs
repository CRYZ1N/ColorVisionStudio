using System.Text.Json;
using System.Text.Json.Serialization;
using ColorVisionStudio.Core.Imaging;
namespace ColorVisionStudio.Core.Configuration;

/// <summary>Strengths normalized 0..1; Vibrance 0..2 (1 neutral); BlurRadius in px 0..20.
/// Defaults are neutral so a fresh install changes nothing. Each filter keeps its own strength.</summary>
public sealed class EffectSettings
{
    public bool MasterEnabled { get; set; } = true;
    public ColorFilterKind SelectedFilter { get; set; } = ColorFilterKind.None;
    public float DeuteranopiaStrength { get; set; } = 1f;
    public float ProtanopiaStrength { get; set; } = 1f;
    public float TritanopiaStrength { get; set; } = 1f;
    public bool ColorFilterEnabled { get; set; } = true;
    public bool VibranceEnabled { get; set; } = true;
    public float Vibrance { get; set; } = 1f;
    public bool DimmingEnabled { get; set; } = true;
    public float Dimming { get; set; } = 0f;
    public bool BlurEnabled { get; set; } = true;
    public float BlurRadius { get; set; } = 0f;

    public EffectSettings Validated()
    {
        var c = (EffectSettings)MemberwiseClone();
        c.DeuteranopiaStrength = Sanitize.Unit(c.DeuteranopiaStrength);
        c.ProtanopiaStrength = Sanitize.Unit(c.ProtanopiaStrength);
        c.TritanopiaStrength = Sanitize.Unit(c.TritanopiaStrength);
        c.Dimming = Sanitize.Unit(c.Dimming);
        c.Vibrance = float.IsFinite(c.Vibrance) ? Math.Clamp(c.Vibrance, 0f, 2f) : 1f;
        c.BlurRadius = float.IsFinite(c.BlurRadius) ? Math.Clamp(c.BlurRadius, 0f, 20f) : 0f;
        if (!Enum.IsDefined(c.SelectedFilter)) c.SelectedFilter = ColorFilterKind.None;
        return c;
    }

    public float ActiveFilterStrength => SelectedFilter switch
    {
        ColorFilterKind.Deuteranopia => DeuteranopiaStrength,
        ColorFilterKind.Protanopia => ProtanopiaStrength,
        ColorFilterKind.Tritanopia => TritanopiaStrength,
        _ => 0f
    };
}

/// <summary>Deterministic order: color filter -> vibrance -> dimming -> blur. Always renders from the original input,
/// so effects cannot accumulate across frames.</summary>
public static class Pipeline
{
    public static FloatImage Render(FloatImage source, EffectSettings raw)
    {
        var s = raw.Validated();
        var o = source.Clone();
        if (!s.MasterEnabled) return o;
        bool cf = s.ColorFilterEnabled && s.SelectedFilter != ColorFilterKind.None && s.ActiveFilterStrength > 0;
        bool vb = s.VibranceEnabled && s.Vibrance != 1f;
        bool dm = s.DimmingEnabled && s.Dimming > 0;
        if (cf || vb || dm)
            for (int i = 0; i < o.Data.Length; i += 3)
            {
                float r = o.Data[i], g = o.Data[i + 1], b = o.Data[i + 2];
                if (cf) (r, g, b) = ColorFilterEffect.Apply(r, g, b, s.SelectedFilter, s.ActiveFilterStrength);
                if (vb) (r, g, b) = VibranceEffect.Apply(r, g, b, s.Vibrance);
                if (dm) (r, g, b) = DimmingEffect.Apply(r, g, b, s.Dimming);
                o.Data[i] = r; o.Data[i + 1] = g; o.Data[i + 2] = b;
            }
        if (s.BlurEnabled && s.BlurRadius > 0) o = BlurEffect.Apply(o, s.BlurRadius);
        return o;
    }
}

public sealed class SettingsFile
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public string SelectedProfile { get; set; } = "Natural";
    public EffectSettings Effects { get; set; } = new();
}

public static class SettingsStore
{
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Never throws on bad input: missing/corrupt/newer-version files yield defaults. Fields are validated individually.</summary>
    public static SettingsFile Load(string path, out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(path)) return new SettingsFile();
            var f = JsonSerializer.Deserialize<SettingsFile>(File.ReadAllText(path), Opt);
            if (f is null) { warning = "Settings file empty; defaults used."; return new SettingsFile(); }
            if (f.Version > SettingsFile.CurrentVersion) { warning = $"Settings version {f.Version} is newer than supported; defaults used."; return new SettingsFile(); }
            f.Effects = (f.Effects ?? new EffectSettings()).Validated();
            return f;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        { warning = "Settings could not be read (" + e.GetType().Name + "); defaults used."; return new SettingsFile(); }
    }

    /// <summary>Atomic write via temp file + replace. Also used for profile export (one profile = one SettingsFile).</summary>
    public static void Save(string path, SettingsFile f)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(f, Opt));
        File.Move(tmp, path, overwrite: true);
    }
}
