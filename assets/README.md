# Assets

Shared branding assets for the Pagonia Land repository, the three Pagonia Land CLIs (`pagonia-manager`, `pagonia-patcher`, `pagonia-paker`), and the local catalog browser.

> Two other locations keep their **own** copies of these icons, not the files here — if you change the icon, update all three:
> - [`docs/assets/`](../docs/assets/) (`favicon.ico`, `icon-128.png`, `icon-32.png`) — used by the MkDocs documentation site.
> - [`tools/catalog-browser/assets/`](../tools/catalog-browser/assets/) (`icon-32.png`, `icon-64.png`, `icon-128.png`) — bundled with the catalog browser so it stays self-contained. On GitHub Pages the browser ships at the `/catalog/` subpath, where a `../../assets/` traversal would escape the site base path and 404; a same-directory `assets/` resolves both there and when the page is opened straight from disk.

## Files

| File | Size | Purpose |
| --- | ---: | --- |
| `icon.ico` | 256×256 (single image) | Windows application icon embedded in all three published CLI executables (`<ApplicationIcon>`) |
| `icon-256.png` | 256×256 | High-resolution master. Not referenced directly today; kept as the source for regenerating the smaller PNGs and as a candidate for the GitHub social-preview image |
| `icon-128.png` | 128×128 | Root `README.md` header, the catalog browser's brand logo, and its 128×128 favicon |
| `icon-64.png` | 64×64 | Catalog browser 64×64 favicon |
| `icon-32.png` | 32×32 | Catalog browser 32×32 favicon |

The PNGs are downscaled from the `.ico` content with high-quality bicubic resampling. Regenerate them after replacing `icon.ico` so all sizes stay in sync.

## Where The Assets Are Used

- **All three CLI projects** reference `icon.ico` through `<ApplicationIcon>`, so each published Windows executable carries the icon:
  - [`tools/pagonia-manager/.../PagoniaLand.Manager.Cli.csproj`](../tools/pagonia-manager/src/PagoniaLand.Manager.Cli/PagoniaLand.Manager.Cli.csproj)
  - [`tools/pagonia-patcher/.../PagoniaLand.Patcher.Cli.csproj`](../tools/pagonia-patcher/src/PagoniaLand.Patcher.Cli/PagoniaLand.Patcher.Cli.csproj)
  - [`tools/pagonia-paker/.../PagoniaLand.Paker.Cli.csproj`](../tools/pagonia-paker/src/PagoniaLand.Paker.Cli/PagoniaLand.Paker.Cli.csproj)
- [`tools/catalog-browser/index.html`](../tools/catalog-browser/index.html) links `icon-32.png`, `icon-64.png`, and `icon-128.png` as favicons and uses `icon-128.png` as the in-page brand logo — from its **own** bundled [`tools/catalog-browser/assets/`](../tools/catalog-browser/assets/) copies (see the note above), not the files here.
- [`README.md`](../README.md) shows `icon-128.png` (96×96) next to the project title.

## Regenerating The PNGs

PowerShell, Windows-only, after replacing `icon.ico`:

```powershell
Add-Type -AssemblyName System.Drawing
$icon = New-Object System.Drawing.Icon('.\assets\icon.ico', 256, 256)
$bmp256 = $icon.ToBitmap()
$bmp256.Save('.\assets\icon-256.png', [System.Drawing.Imaging.ImageFormat]::Png)
foreach ($size in 128, 64, 32) {
    $resized = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($bmp256, 0, 0, $size, $size)
    $g.Dispose()
    $resized.Save(".\assets\icon-$size.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
}
$bmp256.Dispose(); $icon.Dispose()
```
