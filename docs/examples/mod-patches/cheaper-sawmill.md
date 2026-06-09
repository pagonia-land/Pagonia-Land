# Cheaper Sawmill Patch

This example describes a small mod patch that lowers one Sawmill construction cost.

It is based on the worked example [Change A Building Construction Cost](../change-building-cost.md).

## Mod Metadata

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.cheaper-sawmill
name: Cheaper Sawmill
version: 0.1.0
author: TheLavaBlock
gameDatabaseVersion: "1.3.2-11873+194094"
description: Lowers the Sawmill Softwood Trunk construction cost for local testing.
requiredPackages:
  - core
optionalPackages: []
requiresNewGame: false
safeToRemove: unknown
multiplayerSafe: unknown
campaignSafe: unknown
loadAfter: []
loadBefore: []
incompatibleWith: []
patches:
  - patches/buildings.yaml
```

## Patch Operation

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
      resourceGuid: c22b4997-5563-44ab-8aa0-04a7b2c826be
      resourceName: Softwood Trunk
    expectedOldValue: "4"
    value: "3"
```

## Intended Dry-Run Result

```text
Operation: cheaper-sawmill-softwood-cost
Target: Sawmill / AspectBuildup / Softwood Trunk construction amount
Expected old value: 4
New value: 3
Risk: low
Result: safe if target and expected value match
```

## Why This Is A Good First Patch

- it changes one scalar value
- it keeps the resource GUID unchanged
- it keeps the building GUID unchanged
- it does not edit recipe flow, workers, objectives, or placement
- it has a clear expected old value

## What Would Be Unsafe

Avoid turning this into:

```yaml
operation: replaceNode
```

for the whole `AspectBuildup` component. That would hide too much structure inside one large write and make conflicts harder to detect.
