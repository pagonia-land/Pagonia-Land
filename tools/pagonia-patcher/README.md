# Pagonia Land Patcher

This folder hosts the **Pagonia Land Patcher**.

The patcher is a small C#/.NET command line tool that validates and applies declarative GameDatabase patch mods.

## Intended Role

The patcher is the shared core:

```text
mod.yaml + patch files + game XML -> dry-run plan -> optional patched output
```

The shipped `pagonia-manager` links `PagoniaLand.Patcher.Core` as a library; any future mod manager (e.g. a GUI on top of `Manager.Core`) integrates the same way. The patcher also works fully standalone — modders, CI scripts, and third-party tools can drive it via the CLI without going through the manager.

## Download

Grab a Native AOT single-file binary from the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest):

- Windows x64: [pagonia-patcher-win-x64.zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-win-x64.zip)
- Linux x64: [pagonia-patcher-linux-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-linux-x64.tar.gz)
- macOS Intel: [pagonia-patcher-osx-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-x64.tar.gz)
- macOS Apple Silicon: [pagonia-patcher-osx-arm64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-patcher-osx-arm64.tar.gz)

No .NET runtime needed at the destination. Building from source is also documented in [CLI.md](CLI.md).

## Documentation

- [CLI contract](CLI.md) documents commands, exit codes, JSON reports, and mod manager integration.

## How Changes Are Validated

If you modify anything under this folder, run [`scripts/preflight.ps1`](../../scripts/preflight.ps1) from the repository root before committing. It builds both tool solutions, runs every test (currently 64 patcher tests, 58 paker tests), and runs `schema-validate` against every sandbox example. [`.github/workflows/tools.yml`](../../.github/workflows/tools.yml) runs the same steps on every push and PR. See [CONTRIBUTING.md → When You Touch Tools Or Schemas](../../CONTRIBUTING.md#when-you-touch-tools-or-schemas) for the contract between this codebase and the schemas under [`schemas/mod-patches/`](../../schemas/mod-patches/).

## Implemented Commands

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- --version
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-mod --mod .\fixtures\mods\cheaper-sawmill
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- validate-mod --mod .\fixtures\mods\cheaper-sawmill
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-collection --collection ..\..\docs\examples\collections\beginner-qol.collection.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-lock --lock ..\..\docs\examples\collections\beginner-qol.collection-lock.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- resolve-collection --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods --lock .\fixtures\out\collection-lock.yaml
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- export-collection --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill --out .\fixtures\out\exported.collection.yaml --id pagonia-land.local.exported --name "Exported Local Set"
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --collection .\fixtures\collections\local-duplicate.collection.yaml --mods-root .\fixtures\mods
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out\plan.md --json .\fixtures\out\plan.json
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- index-check ..\..\official-mods
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- index build ..\..\official-mods --check
```

`--version`, `inspect-mod`, `validate-mod`, `schema-validate`, `inspect-collection`, `resolve-collection`, `inspect-lock`, `export-collection`, `plan`, and `apply` for one or more mod directories are implemented right now, along with `index-check` and `index build` for keeping a repo's `index.yaml` in sync with its mods.

## C# Structure

```text
tools/pagonia-patcher/
  PagoniaLand.Patcher.slnx
  src/
    PagoniaLand.Patcher.Cli/
    PagoniaLand.Patcher.Core/
  tests/
    PagoniaLand.Patcher.Tests/
  fixtures/
```

## Design Rules

- keep the patcher usable without a mod manager
- keep patcher core separate from CLI presentation
- write patched XML to an output folder by default
- never silently overwrite official game files
- keep diagnostics useful for new modders
- keep test fixtures artificial and small

## Current Status

The patcher can currently read and validate YAML manifests, plan XML value replacements, detect duplicate write conflicts, write Markdown/JSON plan reports, apply clean plans to an output folder, and resolve local collection manifests into lockfiles.

Build:

```powershell
dotnet build .\PagoniaLand.Patcher.slnx
```

Run the CLI:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- --version
```

Run the current offline test project:

```powershell
dotnet run --project .\tests\PagoniaLand.Patcher.Tests
```

Inspect the fixture mod:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- inspect-mod --mod .\fixtures\mods\cheaper-sawmill
```

Validate the fixture mod:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- validate-mod --mod .\fixtures\mods\cheaper-sawmill
```

Plan the fixture mod against the artificial mini XML database:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill
```

Plan a local collection against the artificial mini XML database:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods
```

Plan multiple local collections:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --collection .\fixtures\collections\local-beginner.collection.yaml --collection .\fixtures\collections\local-duplicate.collection.yaml --mods-root .\fixtures\mods
```

Write Markdown and JSON plan reports:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out\plan.md --json .\fixtures\out\plan.json
```

Resolve a local collection and write a lockfile:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- resolve-collection --collection .\fixtures\collections\local-beginner.collection.yaml --mods-root .\fixtures\mods --lock .\fixtures\out\collection-lock.yaml
```

Export direct mod arguments as a collection:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- export-collection --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill --out .\fixtures\out\exported.collection.yaml --id pagonia-land.local.exported --name "Exported Local Set"
```

Plan two conflicting fixture mods:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- plan --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill .\fixtures\mods\conflicting-sawmill
```

Apply the fixture mod to a separate output folder:

```powershell
dotnet run --project .\src\PagoniaLand.Patcher.Cli -- apply --game .\fixtures\game-gdb-mini --mods .\fixtures\mods\cheaper-sawmill --out .\fixtures\out
```

The source `fixtures/game-gdb-mini` files are not modified.
