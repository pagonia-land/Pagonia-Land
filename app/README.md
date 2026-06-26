# Pagonia Land — Desktop App

The **"Pagonia Land"** desktop app: a native cross-platform (Avalonia) GUI that generates a
rich GameDatabase catalog on the fly from your own game install and shows it interactively.
It is also the foundation the mod-management GUI will grow on.

> **Early preview.** A catalog viewer: an **Overview** page (counts + how the domains connect +
> the paks it read) and **Resources / Buildings / Recipes / Units / Objectives** tabs with
> **real icons** decoded straight from your install's `.pak` files. Each tab is a slim list plus
> a **detail pane** with the entity's full fields, and every reference is a **clickable link**
> that jumps to the related entity in both directions across all domains. A global "find
> anything" search, a per-tab filter, Back/Forward history, and a warm-restart cache round it out.

## Run it

From the repository root:

```bash
dotnet run --project app/src/PagoniaLand.App
```

Or open `app/PagoniaLand.slnx` in your IDE and run **PagoniaLand.App**.

## Build a standalone binary

To produce a single self-contained executable (the `.NET` runtime + Avalonia bundled in — no
SDK needed to run it), the same way the [release workflow](../.github/workflows/release.yml) does.
The app is **not** Native-AOT/trimmed (Avalonia's reflection-based XAML/bindings don't trim
cleanly), so it's published self-contained single-file instead:

```bash
# RID is win-x64 or linux-x64 (a macOS .app bundle is a follow-up).
dotnet publish app/src/PagoniaLand.App/PagoniaLand.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded
```

The binary lands in `app/src/PagoniaLand.App/bin/Release/net10.0/<rid>/publish/` as
`Pagonia Land` (`.exe` on Windows). Or just grab a prebuilt archive from the
[releases page](https://github.com/pagonia-land/Pagonia-Land/releases/latest).

In the window: click **Auto-detect** (finds a Steam install) or **Browse…** to point at your
**game install** (the folder with `pak/`), a folder of `.pak` files, or an extracted
`game-gdb` — then **Generate**. The resources table fills in, read straight from your install's
paks and projected locally by the `Catalog.Core` engine.

## Layout

```text
app/
├── PagoniaLand.slnx
├── src/
│   ├── PagoniaLand.Catalog.Core/   # the catalog engine (reads the GameDatabase into a model)
│   └── PagoniaLand.App/            # the Avalonia GUI
└── tests/
    └── PagoniaLand.Catalog.Tests/  # engine tests
```

## Notes

- The app is **local-only** — it reads your own install on your machine and never publishes
  anything.
- `Catalog.Core` is the same engine intended to back a `pagonia-catalog` CLI and, over time,
  to replace the repository's PowerShell analysis/catalog scripts with one tested source.
