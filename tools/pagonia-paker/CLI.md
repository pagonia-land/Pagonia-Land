# Pagonia Land Paker CLI

`pagonia-paker` is the command line front-end of [Pagonia Land Paker](README.md). It lists, unpacks, packs, patches, and gzip-(de)compresses Pioneers of Pagonia `.pak` archives.

The CLI is the canonical user-facing surface; everything it does is also available as plain library calls under the `PagoniaLand.Paker` namespace.

## Commands

```text
pagonia-paker --version
pagonia-paker list       [--json <report>] <pak> [<output-dir>]
pagonia-paker unpack     [filters] [-j <n>] [--json <report>] <pak> [<output-dir>]
pagonia-paker pack       [filters] [-j <n>] [--json <report>] <pakinfo.json> [<output.pak>]
pagonia-paker patch      [-j <n>] [--json <report>] [--delete <path> ...] [--no-gdbin-register] <input.pak> <output.pak> [<file> ...]
pagonia-paker compress   <input> <output>
pagonia-paker decompress <input> <output>
pagonia-paker gdbin info [--json <report>] <gdbin>
pagonia-paker loca info  [--json <report>] <loca>
pagonia-paker classify   [--json <report>] <pak>
```

### `--version`

Prints `Pagonia Land Paker <version>` and exits with `0`. The version string includes the Git hash for published builds.

### `list <pak> [<output-dir>]`

Reads the index of `<pak>` and writes a `pakinfo.json` sidecar describing every entry into `<output-dir>` (defaults to `<pak>` minus the `.pak` suffix, in the same directory). No data blobs are extracted.

The JSON shape is byte-compatible with [plpaker](https://github.com/pagonia-land/plpaker): snake_case fields, `version`, `count`, and an `entries` array with `index`, `pos`, `compressed`, `filename`, `begin`, `end`, `size`, `size_compressed`.

### `unpack [filters] <pak> [<output-dir>]`

Extracts every entry of `<pak>` under `<output-dir>` (same default as `list`). Compressed entries are decompressed on the fly. Filenames are validated: rooted paths, drive-letter paths, and `..` segments are refused.

Filter flags (see below) limit which entries are extracted. Skipped entries are counted in the final summary.

### `pack [filters] <pakinfo.json> [<output.pak>]`

Builds a fresh `.pak` from the entries listed in `<pakinfo.json>`. Source files are resolved relative to the directory that contains the pakinfo, so the typical workflow is:

```powershell
pagonia-paker unpack input.pak   ./work
# edit files under ./work as needed
pagonia-paker pack   ./work/pakinfo.json ./work.pak
```

The default output path is `<pakinfodir>.pak` as a sibling of the pakinfo directory, matching the plpaker convention.

Filter flags limit which entries from the pakinfo are packed. Entries outside the filter are skipped; the on-disk layout is rewritten so begin offsets stay monotonic in the new archive. The pakinfo file itself is never modified.

### `patch [--delete <path> ...] [--no-gdbin-register] <input.pak> <output.pak> [<file> ...]`

Builds a new pak from `<input.pak>` by mixing three kinds of edits:

| Operation | How it's signalled | Behaviour |
| --- | --- | --- |
| **Replace** | Positional path that matches an existing entry name in the base pak | The bytes are read from disk and re-encoded into the entry's slot. The entry's existing `compressed` flag is preserved. |
| **Add** | Positional path that does NOT match any existing entry name | A brand-new entry is appended to the output. The new entry is gzip-packed if its extension is `.xml` / `.yaml` / `.yml` / `.txt`; otherwise it's stored verbatim. |
| **Delete** | `--delete <path>` (can repeat) | The named entry is omitted from the output. The path must match an entry in the input pak. |

Every replacement/add file's path-as-given is BOTH the on-disk source AND the entry name, so the typical workflow is to run the command from a directory laid out like an unpacked archive:

```powershell
cd ./work
pagonia-paker patch ../input.pak ../patched.pak `
  tools/manifest.json `
  tools/extras/new-readme.txt `
  --delete tools/memory.bin
```

The same entry cannot appear twice (e.g. as both a positional path and a `--delete`). Every replacement source must exist on disk; every `--delete` path must exist in the input pak. Pre-flight failures prevent the output file from being created.

Entries that are not in any operation are copied verbatim from the input archive (no re-compression), so unchanged blobs stay byte-identical. The input archive is never modified.

**Auto-registration of added `*.gd.xml`.** When an Add target ends in `.gd.xml` and its path lives under a module namespace that already owns a `<m>/<m>.gd.bin` in the input pak, the patcher implicitly updates that index: reads the existing one, appends the newly-added paths (sorted ordinally so the build is deterministic), recomputes byte[3] of the header to match the new entry count, and writes the result back in place of the original index. The XML alone is not enough for the engine to load it — without the auto-update the new file is invisible. Pass `--no-gdbin-register` to opt out. The behaviour also stands down when the user has already provided their own replacement for the `.gd.bin` (or deleted it), and when the module owns no `.gd.bin` in the first place. Each updated index emits one info-level `pakPatchGdbinUpdated` diagnostic and a `GdbinUpdates` entry in the JSON report.

### `compress <input> <output>`

Pipes `<input>` through gzip (`windowBits = 31`, `CompressionLevel.Optimal`) and writes the result to `<output>`. This is the same compressor `pack` uses for individual entries.

### `decompress <input> <output>`

Pipes `<input>` through gzip decompression and writes the result to `<output>`.

### `classify [--json <report>] <pak>`

Inspect a `.pak` and report **what it contributes** — independent signals, not a single label:

- **`GdbScopes`** — does the pak change the GameDatabase, and *where*? The key signal for understanding (and filtering) a mod at a glance. **Empty means no GameDatabase content** — an empty module-level `<m>.gd.bin` (the editor emits one even for a map-only mod) does *not* count, since it lists no `*.gd.xml` resources. Otherwise:
    - **`global`** — a module-level `<m>.gd.bin` that lists at least one `*.gd.xml` → active in **all** game modes (your normal playthroughs). Shipped paks (`core` / `dlc1`) and globally-active editor mods land here.
    - **`map-scoped`** — a `<m>/usermaps/*.gd.bin` (or raw `*.gd.xml`) → the per-map "hosted game database", active **only when playing that map**.
    - A mod can be **both** (`global, map-scoped`) or neither (empty → no GameDatabase content).
- **`PopmapCount`** — how many maps it bundles (`<m>/usermaps/*.popmap`).
- **`OverridesAtRoot`** — root-level files it overlays that aren't standard pak metadata (the Pattern B config-override pattern, e.g. `system.json` from mod.io's camera-zoom mod).
- **`Name` / `ModuleFolder` / `Dependencies`** — the module's identity from `<m>/manifest.json`.

```text
Pak: Example Mod.pak          # a published editor map: map-only, no global changes
Module: example mod
Name: Example Mod
Dependencies: core, decorations1
GdbScopes: map-scoped
Popmaps: 1
```

> **Why no single "kind" label?** A pak isn't *one of* {module, user-map, overlay} — that was a lossy projection. The engine treats a pak as a **bag of contributions** (its `files.json` declares a GameDatabase, a Localization folder, …; `usermaps/` surfaces maps; same-path files overlay), and since 1.4.0 one pak routinely does several at once — a published editor map ships a **GameDatabase *and* a map**, and that GameDatabase may be global, map-scoped, or both. So `classify` reports the signals directly; collapsing them into one mutually-exclusive bucket would hide the rest. (Earlier builds emitted a single `Kind`; it was dropped as misleading.)

With `--json <path>` the same information is written as a `PakClassifyReport` (module folder, manifest Name, declared Dependencies, `GdbScopes`, `PopmapCount`, `OverridesAtRoot`, diagnostics). Exit code is 0 whenever the pak parses — even if no module folder is found (`Name` / `ModuleFolder` are then null and a diagnostic says why); only a hard pak-parse failure trips a non-zero exit.

Filter flags are rejected on this subcommand.

### `gdbin info [--json <report>] <gdbin>`

Read a `<modulename>.gd.bin` index file (the small index that lists which `*.gd.xml` files a Pioneers of Pagonia module contributes to the GameDatabase) and print its contents:

```text
Gdbin: game-paks/core/core.gd.bin
Entries: 44
Header: 0x03 0x00 0x02 0x2B 0x00 0x00 0x00
EditorTerminator: no
  [0] core/gdb/abilities.gd.xml
  [1] core/gdb/audio.gd.xml
  ...
```

`EditorTerminator` reports whether the index ended with the 1.4.0 Pagonia Editor's zero-length terminator record (`yes` for editor-emitted paks, `no` for the shipped `core`/`dlc1`/… indexes) — see the format section below.

With `--json <path>` the same information is written to disk as a `GdBinInfoReport` (entries list, hex-formatted header, `HasTrailingTerminator`, diagnostics). The CLI rejects filter flags on this subcommand.

For the format itself, see the `.gd.bin Index Format` section below.

### `loca info [--json <report>] <loca>`

Read a compiled `loca_<lang>.bin` localization blob (the file an editor mod ships under `<modulename>/localization/`) and print the strings it carries, in on-disk order:

```text
Loca: package map with dlc1 and gdb/localization/loca_en_us.bin
Strings: 6
  [0] My Animal Farm
  [1] Wookieetreibers Animal farm
  [2] My Animal Farm Plural
  [3] A Farms
  [4] My Animal Farm detail view description
  [5] Map exclusive
```

With `--json <path>` the same information is written to disk as a `LocaInfoReport` (string list, count, diagnostics). The CLI rejects filter flags on this subcommand.

The blob has no header, version, or key table — it is a bare sequence of strings read until end-of-stream, so the decode is best-effort and there is no magic number to confirm a candidate file actually is a loca blob. For the byte layout, see the `loca_<lang>.bin Localization Format` section below.

## Filter Flags (for `unpack` and `pack`)

Filters are AND-composed. Entry indices are 0-based and refer to the positions in the source pak's index (for `unpack`) or in the pakinfo's `entries` array (for `pack`).

| Short | Long | Meaning |
| --- | --- | --- |
| `-c` | `--compress` | Only entries where `compressed = true`. |
| `-d` | `--decompress` | Only entries where `compressed = false`. |
| `-s <n>` | `--start=<n>` | Only entries with index `>= n`. |
| `-e <n>` | `--end=<n>` | Only entries with index `<= n`. |
| `-f <substr>` | `--filter=<substr>` | Only entries whose filename contains `<substr>` (case-sensitive). |

`-c` and `-d` are mutually exclusive — passing both is a usage error. The flags can appear before or after the positional arguments and in any order; both `--name value` and `--name=value` are accepted.

## JSON Reports (for `list`, `unpack`, `pack`, `patch`, `classify`, `gdbin info`, `loca info`)

| Flag | Form | Effect |
| --- | --- | --- |
| `--json <path>` | `--json path/to/report.json` or `--json=path/to/report.json` | Write a machine-readable report describing the command outcome. Always emitted, including on failure — the `Success` field reflects the outcome. |

The report shapes are documented in the [`schemas/paker/`](../../schemas/paker/) folder and validated to live with the CLI:

| Command | Schema |
| --- | --- |
| `list` | [pak-list-report.schema.json](../../schemas/paker/pak-list-report.schema.json) |
| `unpack` | [pak-unpack-report.schema.json](../../schemas/paker/pak-unpack-report.schema.json) |
| `pack` | [pak-pack-report.schema.json](../../schemas/paker/pak-pack-report.schema.json) |
| `patch` | [pak-patch-report.schema.json](../../schemas/paker/pak-patch-report.schema.json) |
| `classify` | [pak-classify-report.schema.json](../../schemas/paker/pak-classify-report.schema.json) |
| `gdbin info` | [gdbin-info-report.schema.json](../../schemas/paker/gdbin-info-report.schema.json) |
| `loca info` | [loca-info-report.schema.json](../../schemas/paker/loca-info-report.schema.json) |

Every report carries a `Diagnostics` array with the same `{Severity, Code, Message, Path}` shape the patcher uses. A mod manager that already reads patcher diagnostics needs no extra code to read paker ones.

### plpaker wiring difference

plpaker's `main.cpp` swaps `-c` and `-d` when forwarding them to its internal filter, so on plpaker `-c` actually keeps the uncompressed entries and `-d` keeps the compressed ones. `pagonia-paker` wires them to what the flag names actually say: `-c`/`--compress` keeps compressed entries, `-d`/`--decompress` keeps uncompressed entries. This is intentional — scripts that relied on the plpaker behaviour need their flags flipped.

## Parallelism (for `unpack`, `pack`, `patch`)

| Flag | Form | Effect |
| --- | --- | --- |
| `-j <n>` | `-j 4` | Cap worker threads at `n`. |
| `--jobs=<n>` | `--jobs=4` or `--jobs 4` | Long form of the same flag. |

`unpack`, `pack`, and `patch` all run their per-entry work (gzip compression for `pack`/`patch` replacements, decompression for `unpack`, verbatim byte copies for `patch`) across a small thread pool. The default is `Environment.ProcessorCount` and the floor is 1. Passing `-j 1` falls back to a fully sequential code path that is byte-identical to the pre-parallel implementation — useful for debugging or for spinning-disk hosts where multiple concurrent reads hurt more than they help.

### Performance

Parallel encoding (via `-j|--jobs=<n>`) combined with 1 MB-buffered FileStreams. Wall-clock numbers on `game-paks/decorations1.pak` (44 MB, 416 entries) and `game-paks/dlc1.pak` (436 MB, ~thousands of entries) on a 32-core host:

| Command | Pak | `-j 1` (sequential) | Default (32 cores) | Speedup |
| --- | --- | ---: | ---: | ---: |
| `unpack` | decorations1 | 0.44 s | 0.24 s | 1.8× |
| `unpack` | dlc1 | 2.46 s | 0.97 s | 2.5× |
| `pack` | decorations1 | 1.49 s | 0.78 s | 1.9× |
| `pack` | dlc1 | 11.97 s | 3.57 s | 3.4× |

(The full series including `-j 4` is in [`scripts/paker-benchmark.ps1`](../../scripts/paker-benchmark.ps1).)

Memory peak for parallel `pack`/`patch` is bounded by the sum of compressed bytes for in-flight entries; with `-j 32` and an average compressed entry size of a few MB this stays well under 100 MB even for the dlc1 pak. If you ever pack the 5 GB `core.pak`, consider `-j 4` or `-j 1` to keep memory predictable.

## Exit Codes

| Code | Meaning |
| ---: | --- |
| 0 | Success. |
| 1 | Error — at least one diagnostic with severity `Error` was emitted, or a runtime IO/format error blocked the command. |
| 64 | Usage error — unknown subcommand, missing required positional argument, or invalid filter flag. |

## Diagnostics

Every command prints diagnostics to stdout (`Info`) or stderr (`Error`) as `<Severity>: <code>: <message>`. Stable codes are listed in `PagoniaLand.Paker.DiagnosticCodes` and are safe to match against from automation. The current set:

```text
# Format-level read errors
pakFooterTruncated
pakIndexOffsetInvalid
pakIndexTruncated
pakIndexCrcMismatch
pakEntryTruncated
pakEntryLongFilenameMarkerMissing
pakEntryFilenameInvalidUtf8

# Pack-time errors
pakInfoJsonInvalid
pakInfoEmpty
packSourceMissing
packSourceUnreadable
packEntryFilenameEmpty

# Patch-time errors
patchInputMissing
patchSourceMissing
patchEntryNotFound
patchDuplicateSource
patchDeleteTargetMissing
patchAddConflictsWithExisting
patchGdbinUpdateFailed

# gd.bin index errors
gdbinHeaderInvalid
gdbinEntryTruncated
gdbinPathDecodingFailed

# loca blob errors
locaEntryTruncated
locaStringDecodingFailed

# Classify warnings
classifyMultipleModules
classifyManifestUnreadable

# Info
pakIndexRead
pakIndexWrite
pakPackWritten
pakPatchWritten
pakEntryAdded
pakEntryDeleted
gdbinRead
locaRead
pakPatchGdbinUpdated
pakClassified
```

## `.gd.bin` Index Format

A `<modulename>.gd.bin` file is the small index sitting inside every Pioneers of Pagonia module pak that tells the engine which `*.gd.xml` files (and occasionally other resources like `*.guids`) contribute to the GameDatabase. The format was reverse-engineered empirically from the four shipped indexes (`core/core.gd.bin`, `dlc1/dlc1.gd.bin`, `decorations1/decorations1.gd.bin`, `tools/tools.gd.bin`).

Layout:

```text
[byte 0]      0x03               magic (constant)
[byte 1]      0x00 or 0x01       semantics unknown — possibly a minor version
[byte 2]      0x02               magic (constant)
[byte 3]      (record count - 1) low byte — see below (the editor terminator counts)
[bytes 4..6]  0x00 0x00 0x00     padding/reserved (constant)
[entries]     repeated:
                uint32 LE  length in UTF-16 code units
                length*2   bytes UTF-16 LE path inside the pak
[terminator]  uint32 LE 0x00000000  — present only on 1.4.0-editor-emitted indexes
```

Entry paths are in-pak paths (not on-disk paths), forward slashes throughout, and may point outside the owning module's namespace (e.g. `tools/tools.gd.bin` carries `core/audio/core.guids` alongside its own `tools/gdb/magmaview.gd.xml`).

**Two producers, two tails.** The shipped paks (`core`/`dlc1`/`decorations1`/`tools`) have no entry count and no terminator — the reader consumes entries until the stream is exhausted. The **1.4.0 Pagonia Editor** instead closes every index it writes with a zero-length terminator record (`00 00 00 00`): after the last entry, or alone after the header for an empty index (a map-only mod's module-level `<m>.gd.bin` is exactly the 7-byte header + this terminator). `GdBinReader` tolerates both — a zero-length record is treated as end-of-list — and records which shape it saw on `GdBinIndex.HasTrailingTerminator` (surfaced as `EditorTerminator` / `HasTrailingTerminator`), so `GdBinWriter` re-serialises either byte-identically.

The seven-byte header is preserved verbatim by `GdBinReader` and `GdBinWriter`. When scaffolding a new `.gd.bin` from scratch, callers can either start from `GdBinIndex.CreateEmpty()` (which uses a default header matching `decorations1.gd.bin`'s shape) or use `GdBinFormatConstants.ComputeHeaderByte3(int)` to derive byte[3] from the record count.

| Index | Entries | Terminator | Header bytes (hex) |
| --- | ---: | --- | --- |
| `core/core.gd.bin` (1.4.0) | 44 | no | `03 00 02 2B 00 00 00` |
| `dlc1/dlc1.gd.bin` | 15 | no | `03 00 02 0E 00 00 00` |
| `decorations1/decorations1.gd.bin` | 2 | no | `03 00 02 01 00 00 00` |
| `tools/tools.gd.bin` | 2 | no | `03 01 02 01 00 00 00` |
| editor single-entry mod (e.g. the 1.4.0-editor test paks) | 1 | yes | `03 00 02 01 00 00 00` |
| editor map-only module index | 0 | yes | `03 00 02 00 00 00 00` |

Byte[3] is consistent with `(record count) - 1` across every observed index, where the **editor's terminator counts as a record**: shipped `core` reads `0x2B`=43 for 44 entries, while the editor's single-entry index reads `0x01` (1 entry + terminator − 1) and its empty index reads `0x00` (0 + terminator − 1). Whether the engine actually reads byte[3] as a count, ignores it, or treats it as something else is unverified — the conservative move when editing an index is to keep byte[3] in sync via `WithComputedHeader()` (which counts the terminator when present). Byte[1] varies between `0x00` (core/dlc1/decorations1) and `0x01` (tools), with no observed correlation; preserving it from the source file is the safe choice. (Shipped entry counts track the game version — `core` was 43 before the 1.4.0 update and is 44 after.)

## `loca_<lang>.bin` Localization Format

A `loca_<lang>.bin` file (e.g. `loca_en_us.bin`) is the compiled-localization blob an editor mod ships under `<modulename>/localization/`. The format was inferred empirically from two test paks produced by the 1.4.0 Pagonia Editor ("package gdb with dependency" and "package map with dlc1 and gdb"), so treat it as a best-effort decode rather than a guaranteed spec.

Layout:

```text
[no header, no count field]
[strings]   repeated until EOF:
              7-bit-encoded int   byte length of the UTF-8 payload (LEB128-style)
              length              bytes UTF-8 string
```

Each string is exactly what `System.IO.BinaryWriter.Write(string)` emits: a length-prefixed UTF-8 string where the prefix is a 7-bit-encoded integer holding the **byte** length, not the character count. In both sample files every prefix fit in a single byte (e.g. `0x12`=18 for `MY Festival Ground`, `0x26`=38 for `My Animal Farm detail view description`); strings ≥ 128 bytes use the multi-byte continuation form (`GdBinReader`'s sibling `LocaReader` decodes both, but the multi-byte case has not been observed in the wild).

There is no magic number, version, or string count — like `.gd.bin`, the file is a bare sequence read until end-of-stream, and a 0-byte file is a valid empty blob. The strings appear to be **value-only and positionally ordered**; no embedded keys were seen, so the engine presumably resolves them by index from the GameDatabase side. Because there is no magic, `loca info` cannot prove a candidate file *is* a loca blob beyond "every length prefix lands inside the stream and every payload decodes as UTF-8".

| Sample blob | Strings | Bytes | First string (prefix → value) |
| --- | ---: | ---: | --- |
| `package gdb with dependency` `loca_en_us.bin` | 2 | 49 | `0x12` → `MY Festival Ground` |
| `package map with dlc1 and gdb` `loca_en_us.bin` | 6 | 126 | `0x0E` → `My Animal Farm` |

## Publishing A Standalone Binary

The paker publishes as a Native AOT single-file executable so end users do not need a .NET runtime. From the paker solution folder, run one of:

```powershell
# Windows x64
dotnet publish .\src\PagoniaLand.Paker.Cli -c Release -r win-x64

# Linux x64
dotnet publish ./src/PagoniaLand.Paker.Cli -c Release -r linux-x64

# macOS (Intel / Apple Silicon)
dotnet publish ./src/PagoniaLand.Paker.Cli -c Release -r osx-x64
dotnet publish ./src/PagoniaLand.Paker.Cli -c Release -r osx-arm64
```

The output ends up in `src/PagoniaLand.Paker.Cli/bin/Release/net10.0/<rid>/publish/`. On Windows that is `pagonia-paker.exe`. The binary can be copied somewhere on `PATH` and used directly:

```powershell
pagonia-paker --version
pagonia-paker list   .\game-paks\tools.pak
pagonia-paker unpack .\game-paks\tools.pak .\work
```

AOT cross-compilation needs the target's native toolchain, so producing all platforms from one machine is not currently supported. Build each binary on its own platform; trying `dotnet publish -r linux-x64` from Windows ends with `Cross-OS native compilation is not supported`.

To verify a release build, after publishing run the same command set the test runner exercises:

```powershell
$exe = ".\src\PagoniaLand.Paker.Cli\bin\Release\net10.0\win-x64\publish\pagonia-paker.exe"
& $exe --version
& $exe list   <pak> <out-dir>
& $exe unpack <pak> <out-dir>
& $exe pack   <out-dir>\pakinfo.json <repacked.pak>
& $exe patch  <pak> <patched.pak> <file>
& $exe compress   <input> <output>
& $exe decompress <input> <output>
```

Implementation notes:

- `PublishAot=true` plus `InvariantGlobalization=true` in the CLI project trigger Native AOT.
- All JSON output uses source-generated `JsonSerializerContext`s (`PakInfoJsonContext`, `PakListJsonContext`, `PakUnpackJsonContext`, `PakPackJsonContext`, `PakPatchJsonContext`), which are AOT-clean by construction.
- The Core library has no YAML or reflection-heavy dependencies, so the AOT graph is small — meaningfully lighter than the patcher's.
- `System.IO.Hashing.Crc32` and `System.IO.Compression.GZipStream` are AOT-compatible.
- `dotnet publish` reports `0 Warning(s)` for the supported RIDs.

## Comparing against plpaker

`scripts/paker-parity-check.ps1` (in the repository root) runs the same arguments through both `plpaker` and `pagonia-paker` and reports any output drift. The script needs both binaries on `PATH` (or via explicit paths). plpaker is kept around as the reference implementation while `pagonia-paker` reaches and ratifies parity; once parity is fully ratified it can be archived.
