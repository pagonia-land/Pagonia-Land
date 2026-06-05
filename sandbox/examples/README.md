# Example Mods

These are starter templates. Copy a folder out into `sandbox/mods/`, rename it, and edit the contents:

```powershell
Copy-Item -Recurse .\sandbox\examples\lower-sawmill-cost .\sandbox\mods\my-first-mod
```

Then open `sandbox/mods/my-first-mod/mod.yaml` and the files under `patches/` and adjust.

## What Each Example Demonstrates

| Example | Operation | What it teaches |
| --- | --- | --- |
| [`lower-sawmill-cost/`](lower-sawmill-cost/) | `replaceValue` | The simplest case: change one number on a known entity. |
| [`remove-stone-cost/`](remove-stone-cost/) | `removeListItem` | Remove one item from a list (here: the Stone Block cost from the Sawmill's construction). |
| [`sanctuary-tweak-abilities/`](sanctuary-tweak-abilities/) | `replaceValue` + `addListItem` + `removeListItem` | All three list-modifying operations in one mod, targeting the Sanctuary (in-game "Cornucopia") ability list from the Meadowsong DLC. **Requires the `dlc1` package**, so this is the first example that won't load against a core-only `game-gdb/`. The patches target real Meadowsong GUIDs from the public release. |
| [`sanctuary-add-custom-ability/`](sanctuary-add-custom-ability/) | `pak:` + `entries.add` + `addListItem` (cross-pak) | **End-to-end "ship a brand-new Sanctuary ability"** worked example. Combines a Pattern B overlay pak (the new ability is defined as an entity in the mod's own `*.gd.xml`) with a Pattern A patch (the new GUID is added to the shipped Sanctuary's `AspectShrine/Abilities` list). Demonstrates a cross-pak reference: the mod's entity is referenced by a dlc1 entity at engine merge time. Requires the `dlc1` package. |
| [`add-extra-asset/`](add-extra-asset/) | `entries.add` | Ship a brand-new binary pak entry (icon, texture, audio, prefab, …) alongside any XML patches. Also has commented-out `entries.replace` and `entries.delete` lines as templates. |
| [`standalone-overlay/`](standalone-overlay/) | `pak:` + `entries.add` | Pattern B worked example: a standalone overlay pak. The `pak:` block triggers the patcher's scaffold step (writes `manifest.json` + `files.json` + `<name>.gd.bin` + `memory.bin` under `<name>/`), so `sandbox-pack` without `-BasePak` produces an engine-loadable pak. See [docs/mod-distribution.md](../../docs/mod-distribution.md) for the conceptual map. |
| [`example.collection.yaml`](example.collection.yaml) | — | Bundles the two simple Sawmill mods above into a collection that the patcher can plan and apply together. |

## GUIDs In These Examples

All examples target **real game GUIDs** from the Meadowsong public release (`1.3.0-11768+193445`). Specifically the Sawmill-based ones use:

- `c732cb26-7487-4a7b-b1ba-b65e094f9bac` — real Sawmill entity GUID.
- `c22b4997-5563-44ab-8aa0-04a7b2c826be` — real Softwood Trunk resource GUID.
- `d8dd765a-ac73-49cc-a9b9-f6102f6f8e07` — real Stone Block resource GUID.

So the Sawmill examples apply directly to an extracted `game-gdb/` of that build — copy `lower-sawmill-cost/` into `sandbox/mods/`, run apply, done. (The patcher's `game-gdb-mini` test fixture mirrors the same GUIDs, so the examples also work against it for a no-real-install smoke test.)

If your install is on a different version where the entity GUIDs or current `expectedOldValue` payloads have changed, the patcher surfaces a clear `targetEntityMissing` or `expectedValueMismatch`. Look up current GUIDs in `generated/entities.json` (regenerate with `scripts/analyze_database.ps1`) and adjust.

**One caveat — `sanctuary-tweak-abilities/`:** the Sanctuary entity GUID and the Ability GUIDs being modified are real Meadowsong GUIDs. But the `value` / `xml` payloads of its `replaceValue` and `addListItem` patches carry placeholder `aaaaaaaa-…` / `bbbbbbbb-…` GUIDs — those you swap for whatever real Ability GUIDs you actually want to point at. The `removeListItem` patch is fully real and applies as-is.

```powershell
.\scripts\sandbox-apply.ps1 -Game .\tools\pagonia-patcher\fixtures\game-gdb-mini
```
