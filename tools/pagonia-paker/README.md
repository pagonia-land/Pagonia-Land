# Pagonia Land Paker

This folder hosts the **Pagonia Land Paker**.

The paker is a small C#/.NET command line tool that lists, unpacks, packs, and patches Pioneers of Pagonia `.pak` archives. It supersedes [plpaker](https://github.com/pagonia-land/plpaker) (full feature parity reached); plpaker is being archived.

## Intended Role

The paker is the shared core for archive handling:

```text
.pak  <->  PakReader / PakWriter / PakPatcher  <->  files on disk
```

The shipped `pagonia-manager` links `PagoniaLand.Paker.Core` as a library for Pattern B overlay-pak deploys; any future mod manager integrates the same way. The paker also works fully standalone for inspecting / repacking arbitrary `.pak` archives.

## Download

Grab a Native AOT single-file binary from the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest):

- Windows x64: [pagonia-paker-win-x64.zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-win-x64.zip)
- Linux x64: [pagonia-paker-linux-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-linux-x64.tar.gz)
- macOS Intel: [pagonia-paker-osx-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-x64.tar.gz)
- macOS Apple Silicon: [pagonia-paker-osx-arm64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-paker-osx-arm64.tar.gz)

No .NET runtime needed at the destination. Building from source is also documented in [CLI.md](CLI.md).

## Documentation

- [`CLI.md`](CLI.md) is the user-facing contract: every command, every flag, every exit code, every diagnostic code. It also documents the `.pak` and `.gd.bin` format references.

## How Changes Are Validated

If you modify anything under this folder, run [`scripts/preflight.ps1`](../../scripts/preflight.ps1) from the repository root before committing. It builds both tool solutions, runs every test (currently 78 paker tests, 174 patcher tests), and runs `schema-validate` against every sandbox example. [`.github/workflows/tools.yml`](../../.github/workflows/tools.yml) runs the same steps on every push and PR.

## Current Status

The CLI exposes `--version`, `list`, `unpack`, `pack`, `patch`, `compress`, `decompress`, `classify`, `gdbin info`, and `loca info` with the five plpaker filter flags (`-c`/`-d`/`-s`/`-e`/`-f`), `--json <report>` for the report-emitting commands, and `-j|--jobs=<n>` for parallel encoding. The offline tests pass, `scripts/paker-parity-check.ps1` cross-checks output against `plpaker`, `scripts/paker-benchmark.ps1` measures the parallel speedup (≈3.4× on `pack dlc1.pak`), and `scripts/sandbox-pack.ps1` bundles `sandbox/out/` into a `.pak`. Native AOT publish ships a single-file Windows binary that smoke-tests cleanly across every command. The publishing recipe and verify-a-release-build walk-through are in [`CLI.md`](CLI.md). Feature coverage: full plpaker parity, sandbox-pack loop, `.gd.bin` byte-identical round-trip, auto-register added XMLs on `patch`, Pattern B overlay-pak scaffold, and pak-shape classifier.

Build:

```powershell
dotnet build .\PagoniaLand.Paker.slnx
```

Run the CLI:

```powershell
dotnet run --project .\src\PagoniaLand.Paker.Cli -- --version
dotnet run --project .\src\PagoniaLand.Paker.Cli -- list       <pak> [<dir>]
dotnet run --project .\src\PagoniaLand.Paker.Cli -- unpack     <pak> [<dir>]
dotnet run --project .\src\PagoniaLand.Paker.Cli -- pack       <pakinfo.json> [<out.pak>]
dotnet run --project .\src\PagoniaLand.Paker.Cli -- patch      <input.pak> <output.pak> <file> [<file> ...]
dotnet run --project .\src\PagoniaLand.Paker.Cli -- compress   <input> <output>
dotnet run --project .\src\PagoniaLand.Paker.Cli -- decompress <input> <output>
```

This is a quickstart; for the full surface — `gdbin info`, `loca info`, `classify`, the
`patch` flags `--delete` / `--no-gdbin-register`, and the filter flags — see
[`CLI.md`](CLI.md).

Run the offline test project:

```powershell
dotnet run --project .\tests\PagoniaLand.Paker.Tests
```

Publish a Native AOT binary:

```powershell
dotnet publish .\src\PagoniaLand.Paker.Cli\PagoniaLand.Paker.Cli.csproj -c Release -r win-x64
```

## C# Structure

```text
tools/pagonia-paker/
  PagoniaLand.Paker.slnx
  src/
    PagoniaLand.Paker.Cli/
    PagoniaLand.Paker.Core/
  tests/
    PagoniaLand.Paker.Tests/
  fixtures/
```

## Design Rules

- keep the paker usable without a mod manager
- keep paker core separate from CLI presentation
- never modify the input `.pak`; always write to a separate output
- stream both reading and writing; never buffer a whole archive in memory
- keep diagnostics useful for modders and machine-readable for tools
- keep test fixtures generated at test time, not copied from real game data
