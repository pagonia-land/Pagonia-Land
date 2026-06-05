# Resource Icon Patch

This example walks through the shipped [`apple-icon-test`](https://github.com/pagonia-land/Pagonia-Land/tree/main/examples/mod-repo-example/mods/apple-icon-test) example mod: a cosmetic resource-icon change that only runs when an optional DLC package is present. It builds on the worked example [Change A Resource Display Name Or Icon](../change-resource-display-icon.md), and demonstrates the **optional patch set** mechanism for DLC-gated content.

## Mod Metadata

The mod requires only `core`, but declares `dlc1` as *optional*. The icon swap itself lives in an **optional patch set** that is skipped entirely on a core-only install:

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.apple-icon-test
name: Apple Icon Test
version: 0.1.0
author: TheLavaBlock
gameDatabaseVersion: "1.3.1-11826+193733"
description: Cosmetic DLC pack patch — swaps the Wine icon for the Grapes icon. Demonstrates how to ship a patch that only runs when the dlc1 package is installed.
requiredPackages:
  - core
optionalPackages:
  - dlc1
requiresNewGame: false
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown
loadAfter: []
loadBefore: []
incompatibleWith: []
patchSets:
  - id: dlc1-icon
    optional: true
    requiresPackages:
      - dlc1
    patches:
      - patches/dlc1-icon.yaml
```

Because `dlc1-icon` is `optional: true` and gated behind `requiresPackages: [dlc1]`, the set is applied only when `dlc1` is installed. On a core-only install it is silently skipped — no error, no missing-target failure.

## Patch Operation

The single operation in `patches/dlc1-icon.yaml` swaps the **Wine** commodity icon (a `dlc1` resource) for the Grapes icon:

```yaml
operations:
  - id: wine-icon-swap
    operation: replaceValue
    risk: low
    reason: Cosmetic-only DLC icon swap; gated behind requiresPackages -> dlc1 so it never fires on a core-only install.
    target:
      file: dlc1/gdb/resources.gd.xml
      entityGuid: ddb8f6f6-40ed-46d8-9751-76014d51ec5c
      entityName: Wine
      component: ResourceDescription
      path: Icon
    expectedOldValue: dlc1/gui/icons/commodities/icon_com_wine_001.png
    value: dlc1/gui/icons/commodities/icon_com_grapes_001.png
```

## Intended Dry-Run Result

```text
Operation: wine-icon-swap
Target: Wine / ResourceDescription / Icon
Expected old value: dlc1/gui/icons/commodities/icon_com_wine_001.png
New value: dlc1/gui/icons/commodities/icon_com_grapes_001.png
Risk: low
Result: applied if dlc1 is installed and the expected old value matches; skipped if dlc1 is absent
```

## Why This Is Low Risk

- it changes a display asset path only
- it does not change the resource GUID
- it does not change production, storage, deposits, recipes, or objectives
- the new path is already known in the current database
- the whole set is gated on `dlc1`, so a core-only player is unaffected

## Validation Ideas

A future validator could check:

- target entity exists (when the gating package is present)
- `ResourceDescription/Icon` exists
- old value matches
- new value appears somewhere in `generated/catalog/asset-references.csv`

That last check would not prove the asset exists in the pak, but it would catch many typos.
