# ColorVision Studio (work in progress, phase 1 of 3)

## Status (honest)
| Item | State |
|---|---|
| Core engine (color-vision simulation, vibrance, dimming, blur, pipeline) | Written; maths cross-checked in Python/numpy, **C# not compiled** |
| Settings JSON (versioned, tolerant load, atomic save) | Written, **not compiled** |
| xUnit tests | Written, **never run** (no .NET SDK in authoring environment) |
| HLSL shader (colour/vibrance/dim) | Written, **not compiled** |
| WPF UI, profiles manager, tray, hotkeys, previews | **Not started** |
| Desktop capture / fullscreen presentation | **Not started** |
| `.exe` | **Not produced** |

## Build (Windows 10/11, .NET 8 SDK)
`powershell -File scripts\build.ps1` restores, builds, and runs tests. Publishing the exe needs the App project (phase 2).

## Design decisions
- Colour filters: Machado et al. 2009 severity-1.0 matrices in linear RGB; strength = linear blend with the original. **Simulation only**, no compensation mode yet.
- Vibrance: software chroma scaling around Rec.709 luma, attenuated for saturated pixels. It does **not** touch NVIDIA/AMD/Intel driver settings.
- Dimming: scale in encoded space (preserves hue); 100% = black.
- Blur: separable Gaussian, sigma = radius/2, clamp-to-edge.
- Order: filter -> vibrance -> dimming -> blur, always from the original frame.

## Windows limits
Windows has no supported API for post-processing the final desktop frame of every app. Realistic modes: (A) preview on sample images; (B) Windows Graphics Capture of a monitor rendered in a separate fullscreen/borderless window (protected content may appear black, adds latency, needs a hotkey/Esc to exit); (C) gamma-ramp/color-matrix APIs (e.g. the Magnification API colour effect) can apply colour matrices system-wide without capture, but not blur. Dimming and the three colour filters could use (C); blur needs (B).
