# ⛏️ Sandbox

This folder is the **modding workshop** for Pagonia Land. It's where you write your own mods, optionally bundle them into collections, and produce a patched copy of the game database — without touching the original game files.

## Layout

| Folder | Purpose | Tracked by Git |
| --- | --- | :---: |
| `sandbox/examples/` | Read-only templates you can copy as a starting point | yes |
| `sandbox/mods/` | **Your own mods.** Each subfolder is one mod (`mod.yaml` + `patches/`). | no |
| `sandbox/collections/` | **Your own collection manifests** (`*.collection.yaml`) | no |
| `sandbox/out/` | The patched game database after `sandbox-apply` runs. Wiped and re-created on every apply, so this folder does not exist in a fresh clone. | no |

Only the example folder and the per-folder `README.md` files are committed. Everything you put under `sandbox/mods/`, `sandbox/collections/`, and `sandbox/out/` stays on your machine.

## The Three-Step Workflow

1. **Extract the game database** into [`game-gdb/`](../game-gdb/README.md). The sandbox patches against that copy and never modifies it.
2. **Write or copy a mod into `sandbox/mods/`**. The fastest start is `cp -r sandbox/examples/lower-sawmill-cost sandbox/mods/my-first-mod` and then edit the new copy. Look up real entity GUIDs in `generated/entities.json` (regenerate it with `scripts/analyze_database.ps1` after refreshing `game-gdb/`).
3. **Run the apply script.** From the repository root:

   ```powershell
   .\scripts\sandbox-apply.ps1
   ```

   The script collects every subfolder under `sandbox/mods/`, hands them to `pagonia-patcher apply --game ./game-gdb …`, and writes the patched game database to `sandbox/out/`. It also writes an `apply.md` (human-readable) and `apply.json` (machine-readable) report there.

To apply through a collection instead of a flat mods list:

```powershell
.\scripts\sandbox-apply.ps1 -Collection .\sandbox\collections\my-bundle.collection.yaml
```

The script reuses the same `pagonia-patcher` CLI documented in [tools/pagonia-patcher/CLI.md](../tools/pagonia-patcher/CLI.md); the sandbox is just a conventional layout and a one-line wrapper.

## Bundling The Patched Output Into A .pak

When the patched XML under `sandbox/out/` is ready, `scripts/sandbox-pack.ps1` bundles it into a `.pak`:

```powershell
.\scripts\sandbox-pack.ps1
# -> sandbox\out\sandbox.pak
```

To produce a real mod pak that replaces only a few entries inside an existing Pioneers of Pagonia pak, point the script at the base pak:

```powershell
.\scripts\sandbox-pack.ps1 -BasePak .\game-paks\core.pak -Output .\my-mod.pak
```

In patch mode, every file under `sandbox/out/` is forwarded to `pagonia-paker patch` as a positional path:

- if the entry name matches an existing entry in the base pak, the paker substitutes it (Replace)
- if the entry name does not match, the paker appends it to the pak as a new entry (Add)

`sandbox/out/.entries-deleted.txt` is the deletion sidecar that `pagonia-patcher apply` writes whenever a mod's `entries.delete:` list fires. Each line in that file becomes a `--delete <path>` flag on the paker invocation, so deleted entries are omitted from the resulting pak.

You can reproduce the full apply-and-pack loop end-to-end against the patcher's built-in artificial fixture, without an installed game:

```powershell
# Apply against the artificial mini game database.
.\scripts\sandbox-apply.ps1 -Game .\tools\pagonia-patcher\fixtures\game-gdb-mini

# Pack the patched output. The pak roundtrips byte-identically.
.\scripts\sandbox-pack.ps1

# Verify by listing and unpacking.
dotnet run --project .\tools\pagonia-paker\src\PagoniaLand.Paker.Cli -- list   .\sandbox\out\sandbox.pak .\sandbox\out\verify
dotnet run --project .\tools\pagonia-paker\src\PagoniaLand.Paker.Cli -- unpack .\sandbox\out\sandbox.pak .\sandbox\out\verify
```

The pack script wraps the `pagonia-paker` CLI documented in [tools/pagonia-paker/CLI.md](../tools/pagonia-paker/CLI.md); the mod-manifest `entries:` block is documented in [docs/mod-patch-format.md](../docs/mod-patch-format.md).

## A Note On The Examples

The examples target **real Pioneers of Pagonia GUIDs** from the Meadowsong public release (`1.3.0-11768+193445`). The Sawmill examples work as-is against an extracted `game-gdb/` of that build — both the entity GUID (`c732cb26-…`) and the resource GUIDs (`c22b4997-…` Softwood Trunk, `d8dd765a-…` Stone Block) match what the shipped game uses.

If your install is on a different version where those GUIDs or current costs have changed, the patcher surfaces a clear diagnostic (`targetEntityMissing` or `expectedValueMismatch`). Look up current GUIDs in `generated/entities.json` and adjust. Regenerate the index with `scripts/analyze_database.ps1` after refreshing `game-gdb/`.

## Related Documents

- [Mod Distribution Patterns](../docs/mod-distribution.md) explains where the sandbox workflow fits among the three Pioneers of Pagonia mod shapes (patched canonical pak, side-by-side overlay pak, user maps).
- [First Modding Experiments](../docs/first-mods.md) introduces the data with small, read-and-edit experiments against `game-gdb/`, then points here once you want to keep a change as a real mod.
- [Declarative Mod Patch Format](../docs/mod-patch-format.md) is the reference for the YAML shapes the sandbox consumes.
- [Patcher CLI](../tools/pagonia-patcher/CLI.md) documents the commands `sandbox-apply.ps1` wraps.
- [Paker CLI](../tools/pagonia-paker/CLI.md) documents the commands `sandbox-pack.ps1` wraps.
