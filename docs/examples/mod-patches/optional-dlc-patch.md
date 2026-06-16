# Optional DLC Patch Set

This example shows how a mod can have a core patch and an optional DLC patch.

The important idea is:

```text
Core patch applies when core is present.
DLC patch applies only when dlc1 is present.
```

## Mod Metadata

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.production-tuning
name: Production Tuning With Optional DLC Support
version: 0.1.0
author: Pagonia Land
gameDatabaseVersion: "1.4.0-11944+194631"
description: Demonstrates package-aware patch sets.

requiredPackages:
  - core
optionalPackages:
  - dlc1

requiresNewGame: unknown
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown

loadAfter: []
loadBefore: []
incompatibleWith: []

patchSets:
  - id: base
    optional: false
    requiresPackages:
      - core
    patches:
      - patches/core.yaml

  - id: dlc1-support
    optional: true
    requiresPackages:
      - dlc1
    patches:
      - patches/dlc1.yaml
```

## Core Patch

```yaml
operations:
  - id: cheaper-sawmill-softwood-cost
    operation: replaceValue
    risk: low
    reason: Lower early Sawmill construction cost for testing.
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
```

## Optional DLC Patch

```yaml
operations:
  - id: wine-icon-test
    operation: replaceValue
    risk: low
    reason: Demonstrates a dlc1-only patch target.
    target:
      file: dlc1/gdb/resources.gd.xml
      entityGuid: ddb8f6f6-40ed-46d8-9751-76014d51ec5c
      entityName: Wine
      component: ResourceDescription
      path: Icon
    expectedOldValue: dlc1/gui/icons/commodities/icon_com_wine_001.image
    value: dlc1/gui/icons/commodities/icon_com_grapes_001.image
```

## Expected Validator Behavior

Core-only setup:

```text
Apply patch set: base
Skip optional patch set: dlc1-support
Reason: package dlc1 not present
```

DLC-enabled setup:

```text
Apply patch set: base
Apply patch set: dlc1-support
```

Missing required package:

```text
Error:
Required package core is missing.
Do not apply mod.
```

## Why This Matters

Without package-aware patch sets, a DLC patch can fail on a core-only setup because `game-gdb/dlc1/gdb/` does not exist.

With package-aware patch sets, one mod can support several user setups without forcing everyone to own the same DLC.

> **Note on a live install (deploying with `pagonia-manager`).** Pioneers of Pagonia ships every pak — including `dlc1` — to *every* player, so `dlc1/` is usually present on disk even for someone who didn't buy the DLC. The manager therefore refines the "skip" picture: the optional `dlc1-support` set is **skipped only when `dlc1` is genuinely absent**, but on a present-but-not-owned install it **still deploys** (so a co-op participant matches the host) while staying inactive in solo play — surfaced as an info diagnostic rather than a silent no-op. See [Present vs Owned vs Effective](../../package-layering.md#present-vs-owned-vs-effective) and the [manager's expansion gate](../../../tools/pagonia-manager/CLI.md#expansions-ownership).
