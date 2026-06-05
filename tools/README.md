# Pagonia Land Tools

Three command-line tools plus a static browser. Each addresses a different layer of the modding workflow, but they share one C# solution structure (Core + Cli) and one set of JSON Schemas under [`../schemas/`](../schemas/).

## Which Tool When?

| Tool | What it does | Use when… |
| --- | --- | --- |
| [`pagonia-paker/`](pagonia-paker/) | Lists, unpacks, packs, patches, and classifies `.pak` archives; builds `.gd.bin` indexes for Pattern B overlay mods | You need to inspect a `.pak`, extract its contents, repack edits, or build an overlay pak |
| [`pagonia-patcher/`](pagonia-patcher/) | Applies declarative `mod.yaml` edits to game XML with conflict detection and lockfiles | You're writing a mod by hand and want the patch operations + validation, or you need to plan/apply mods at the XML level |
| [`pagonia-manager/`](pagonia-manager/) | Installs / enables / profiles / plans / deploys / rolls back mods against a real game install. Wraps the patcher and paker cores | You want to install someone else's mod (or your own), manage profiles, deploy safely with rollback. **The default tool for most modders.** |
| [`catalog-browser/`](catalog-browser/) | Static HTML browser for the locally-generated catalog | You want to explore the extracted game database in a UI rather than grepping XML |

Most modders only ever run **`pagonia-manager`**. The patcher and paker are exposed as separate CLIs because they're useful on their own (writing mods, repacking paks) and because the manager is built on top of them.

## Run It

Two ways to get a binary:

- **Download from [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest)** — Native AOT single-file, no .NET runtime needed. Available for win-x64 / linux-x64 / osx-x64 / osx-arm64.
- **Build from source** — each tool's README has the `dotnet publish` recipe.

The **manager** launches in two modes:

- Run `pagonia-manager` with **no arguments** in a terminal → interactive shell with menus and wizards (Spectre.Console)
- Pass any argument (`--help`, `install ...`, `--version`) → scripted CLI surface, exactly as documented in its `CLI.md`

The **paker** and **patcher** are CLI-only (no interactive shell); they're driven entirely through the arguments documented in each tool's `CLI.md`, and are typically invoked by the manager or by scripts.

## Documentation

- Each tool has its own `README.md` (architecture, intended role) and `CLI.md` (every command, flag, exit code, diagnostic code)
- The schemas these tools read and write are in [`../schemas/`](../schemas/)
- A walkthrough that drives every manager command end-to-end against a fixture game tree: [`../sandbox/examples/manager-walkthrough/`](../sandbox/examples/manager-walkthrough/)
- First-time orientation: [`../docs/setup.md`](../docs/setup.md) (start with its **Quick Path** section)

## Source Structure

All three CLI tools follow the same layout:

```text
tools/<tool>/
├── README.md                    — overview, intended role
├── CLI.md                       — user-facing contract
├── PagoniaLand.<Tool>.slnx      — solution
├── src/
│   ├── PagoniaLand.<Tool>.Core/ — library (the actual logic)
│   └── PagoniaLand.<Tool>.Cli/  — CLI front-end (thin wrapper)
└── tests/
    └── PagoniaLand.<Tool>.Tests/
```

Future GUIs sit on top of the same `Core` library — the CLI is just one consumer of that library, not the implementation.

## Verification

`scripts/preflight.ps1` builds + tests all three tools and runs the manager walkthrough end-to-end against a fixture game tree. CI ([`.github/workflows/tools.yml`](../.github/workflows/tools.yml)) runs the same plus an AOT publish smoke. See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.
