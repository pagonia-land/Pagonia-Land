# Third-Party Notices

The Pagonia Land **downloadable binaries** bundle third-party open-source components, so this file
acknowledges them and their licenses. (The repository *source* under MIT / CC BY 4.0 is covered by
[LICENSE](LICENSE) and [LICENSE-DOCS.md](LICENSE-DOCS.md); this file is specifically about code that
ships *inside* the released artifacts.)

Each component is used under its own upstream license — the license named below is the one declared
by that project's NuGet package. Licenses are unmodified; follow the links for the full text.

## Command-line tools (`pagonia-patcher` / `pagonia-paker` / `pagonia-manager`)

Published as Native AOT single-file binaries that statically include:

| Component | Version | License | Project |
| --- | --- | --- | --- |
| YamlDotNet | 18.0.0 | MIT | https://github.com/aaubry/YamlDotNet |
| JsonSchema.Net | 9.2.2 | MIT¹ | https://github.com/json-everything/json-everything |
| Spectre.Console | 0.57.1 | MIT | https://github.com/spectreconsole/spectre.console |
| System.IO.Hashing | 10.0.9 | MIT | https://github.com/dotnet/runtime |

¹ JsonSchema.Net is MIT-licensed. The json-everything project additionally offers an optional
"Open Source Maintenance Fee" for its own pre-built binary releases, applicable only to
revenue-generating users above a revenue threshold; it does not affect use of the MIT-licensed
library compiled from source, and this non-commercial community project is exempt regardless.

## Pagonia Land app

Published self-contained (the .NET runtime plus the following are bundled into the single file):

| Component | Version | License | Project |
| --- | --- | --- | --- |
| Avalonia (+ `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`) | 12.0.5 | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Controls.DataGrid | 12.0.1 | MIT | https://github.com/AvaloniaUI/Avalonia |
| SkiaSharp (+ native `libSkiaSharp`) | 3.119.4 | MIT | https://github.com/mono/SkiaSharp |
| HarfBuzzSharp (+ native `libHarfBuzzSharp`) | 8.3.1.3 | MIT | https://github.com/mono/SkiaSharp |
| BCnEncoder.Net | 2.3.0 | MIT OR Unlicense | https://github.com/Nominom/BCnEncoder.NET |
| System.IO.Hashing | 10.0.9 | MIT | https://github.com/dotnet/runtime |

### Bundled typeface — Inter

`Avalonia.Fonts.Inter` embeds the **Inter** typeface, which is licensed under the
**SIL Open Font License, Version 1.1** — *not* MIT. Inter is © The Inter Project Authors
(https://github.com/rsms/inter); the OFL text is at https://openfontlicense.org. The font is used
unmodified as the app's default UI typeface.

### Native rendering libraries

The SkiaSharp / HarfBuzzSharp NuGet packages carry native libraries (`libSkiaSharp`,
`libHarfBuzzSharp`) that wrap Google's **Skia** (BSD-3-Clause) and **HarfBuzz** (the "Old MIT"
license). These are redistributed unmodified as shipped in the upstream packages.

---

This list covers what the released artifacts statically include. The base .NET runtime is
distributed under the MIT license by Microsoft (https://github.com/dotnet/runtime). If you spot a
component that should be listed here and isn't, please open an issue.
