# Local Game Database Files

This directory is the local input area for extracted Pioneers of Pagonia game database XML files.

The XML files in this directory are ignored by Git and should not be published in this repository. Keep them local unless you have permission from the rights holder.

## Expected Layout

The layout mirrors the canonical structure inside each `.pak` archive. A standard
install ships **all** of these paks to every player — Envision Entertainment
includes `core`, `decorations1`, `dlc1`, and `tools` regardless of which DLC the
player owns, and gates the actual *content* at runtime (`DLCNeedsOwn` /
`NeedsUnlock`), not by leaving a pak off disk (see
[Package Layering](../docs/package-layering.md)). So each subfolder below is
present once you extract its pak:

```text
game-gdb/
├── core/                 (base game database)
│   └── gdb/
├── decorations1/         (small decorations expansion)
│   └── gdb/
├── dlc1/                 (Meadowsong DLC; its content is runtime-gated by ownership)
│   ├── gdb/
│   └── maps/             (map-specific XMLs shipped by the pak)
└── tools/                (editor / tools data)
    └── gdb/
```

(A subfolder can still be absent across *game versions* — e.g. a pre-Meadowsong
release predates `dlc1.pak` entirely — which is why a clean re-extract matters
below.)

Every `*.gd.xml` lives under `<pak>/gdb/<file>.gd.xml`, with the additional known case of `<pak>/maps/<...>.gd.xml` for paks that ship map-specific GameDatabase content. Mod patches written against this layout also work against a paker-unpacked archive.

## Always Refresh Cleanly

> **Rule.** When updating to a new game version (DLC install, DLC uninstall, hotfix, any upgrade or downgrade), **wipe the entire `game-gdb/` tree before re-extracting**. Otherwise a subfolder shipped by the previous version but not the new one (e.g. `dlc1/` after a roll-back to a pre-DLC release) silently lingers and downstream tools mix two versions' XMLs together.

The extract script handles this when called with `-Clean`:

```powershell
pwsh ./scripts/extract-xmls-from-paks.ps1 -Clean
```

That wipes everything under `game-gdb/` except this README, then runs [`pagonia-paker`](../tools/pagonia-paker/CLI.md) with the `-f .gd.xml` filter against every pak under [`../game-paks/`](../game-paks/README.md). Only XML entries (not textures or binaries) land here.

Running without `-Clean` is additive — it works for the "no version change" case (re-extracting the same paks) but is the wrong default when refreshing for a new release. Treat `-Clean` as the default.

For a full update (snapshot + clean extract + validate + analyze + catalog), see [One-Command Refresh](../UPDATE_PLAYBOOK.md#one-command-refresh) in the playbook — it already routes through `-Clean`.

## Regenerate Local Analysis Only

If the XMLs are already current and you just want to refresh the derived indexes:

```powershell
pwsh ./scripts/analyze_database.ps1
```

This regenerates the local ignored JSON files in `generated/`.
