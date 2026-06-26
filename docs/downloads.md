---
title: Download
hide:
  - navigation
---

# ⬇ Download

Pagonia Land ships the **Pagonia Land app** (a desktop catalog viewer) and the **CLI tools** (manager / patcher / paker). No .NET runtime needed at the destination — each archive contains one folder with the executable inside.

[![Latest release](https://img.shields.io/github/v/release/pagonia-land/Pagonia-Land?include_prereleases&label=latest%20release)](https://github.com/pagonia-land/Pagonia-Land/releases/latest)

## Pagonia Land App

The native desktop GUI that generates an interactive GameDatabase catalog from your own Pioneers of Pagonia install — real icons, bidirectional cross-navigation, and search. Self-contained single-file.

| Platform | Download |
|---|---|
| Windows x64 | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-land-app-win-x64.zip) |
| Linux x64 | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-land-app-linux-x64.tar.gz) |

macOS builds are planned. See the [app overview](_imported/app/index.md) for what it does and how to build from source.

## Command-Line Tools

The `manager` / `patcher` / `paker` CLIs ship as **Native AOT single-file binaries**.

| Platform | Manager | Patcher | Paker |
|---|---|---|---|
| Windows x64 | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-win-x64.zip) | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-win-x64.zip) | [⬇ zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-win-x64.zip) |
| Linux x64 | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-linux-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-linux-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-linux-x64.tar.gz) |
| macOS Intel | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-x64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-x64.tar.gz) |
| macOS Apple Silicon | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-arm64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-arm64.tar.gz) | [⬇ tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-arm64.tar.gz) |

The download URLs above always point at the latest published release. The filename inside each archive carries the version (e.g. `pagonia-manager-0.4.0-win-x64/`).

!!! tip "Most modders only need the manager"
    `pagonia-manager` wraps the patcher and paker cores. Unless you're writing mods by hand or scripting `.pak` operations, the manager is the only download you need.

## Schemas Only

For mod-manager developers, IDE plugins, and web validators that want to integrate against the contracts without running the tools:

- [⬇ pagonia-schemas.zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-schemas.zip)

The bundled JSON Schemas mirror the [Schemas](_imported/schemas/index.md) section on this site.

## Verify Your Download

Every release ships a [`SHA256SUMS.txt`](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/SHA256SUMS.txt) covering every archive.

=== "Linux / macOS"

    ```bash
    # In the directory containing your downloads + SHA256SUMS.txt
    sha256sum -c SHA256SUMS.txt
    ```

=== "Windows (PowerShell)"

    ```powershell
    # Per-file check
    Get-FileHash pagonia-manager-win-x64.zip -Algorithm SHA256
    # Compare the resulting Hash against the matching line in SHA256SUMS.txt
    ```

## First Launch Notes

### macOS Gatekeeper

The macOS binaries (both Apple Silicon and Intel) are **ad-hoc signed**, so they launch at all — which a completely unsigned binary would not. On first launch, Gatekeeper may still complain because the signature isn't from a recognised developer:

1. Right-click the binary in Finder → **Open**
2. Confirm in the dialog
3. macOS remembers the choice for subsequent launches

We don't have an Apple Developer ID for full notarisation yet — the project is small enough that the right-click-Open bypass is the practical workflow.

### Windows SmartScreen

First launch may show a SmartScreen warning for unsigned binaries. Click **More info → Run anyway** to proceed. Same caveat as macOS — no code-signing cert yet.

### Linux

No first-launch friction; the tar.gz extracts to a self-contained folder and runs directly.

## Building From Source

If you'd rather build locally — for a custom platform target (incl. macOS), a development branch, or to inspect the build — see the overviews:

- [Pagonia Land App](_imported/app/index.md)
- [Pagonia Manager](_imported/tools/pagonia-manager/index.md)
- [Pagonia Patcher](_imported/tools/pagonia-patcher/index.md)
- [Pagonia Paker](_imported/tools/pagonia-paker/index.md)

You need .NET 10 SDK installed locally for source builds.

## What's Next

- **First time?** Read [Local Setup](setup.md) (its **Quick Path** is the 10-minute speedrun), then run `pagonia-manager` with no arguments for the interactive shell.
- **Already familiar?** The [Manager CLI Reference](_imported/tools/pagonia-manager/cli.md) covers every command, flag, and exit code.
- **Browsing the game database?** Use the [Catalog Browser](https://pagonia-land.github.io/Pagonia-Land/catalog/){:target="_blank"} — bring your own locally-generated `search-index.json`.
