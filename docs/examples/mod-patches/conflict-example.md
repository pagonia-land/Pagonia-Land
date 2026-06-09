# Conflict Example

This example shows why patch validation matters.

Two mods can both be valid on their own and still conflict when enabled together.

## Mod A

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.cheaper-sawmill-a
name: Cheaper Sawmill A
version: 0.1.0
gameDatabaseVersion: "1.3.2-11873+194094"
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
```

```yaml
operations:
  - id: cheaper-sawmill-softwood-a
    operation: replaceValue
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "3"
```

## Mod B

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.cheaper-sawmill-b
name: Cheaper Sawmill B
version: 0.1.0
gameDatabaseVersion: "1.3.2-11873+194094"
requiredPackages:
  - core
patches:
  - patches/buildings.yaml
```

```yaml
operations:
  - id: cheaper-sawmill-softwood-b
    operation: replaceValue
    target:
      file: core/gdb/buildings.gd.xml
      entityGuid: c732cb26-7487-4a7b-b1ba-b65e094f9bac
      entityName: Sawmill
      component: AspectBuildup
      path: Costs/Item[Content/Resource='c22b4997-5563-44ab-8aa0-04a7b2c826be']/Content/Amount
    expectedOldValue: "4"
    value: "2"
```

## Conflict Report

A future validator should report:

```text
Blocking conflict:
Both mods write the same target.

Target:
game-gdb/core/gdb/buildings.gd.xml
Sawmill / AspectBuildup / Costs / Softwood Trunk / Amount

Mod A:
expected 4 -> writes 3

Mod B:
expected 4 -> writes 2

Manual decision required.
```

## Safe Chained Version

If Mod B intentionally builds on Mod A, it should say so:

```yaml
id: pagonia-land.example.cheaper-sawmill-b
loadAfter:
  - pagonia-land.example.cheaper-sawmill-a
```

And its expected value should match Mod A's output:

```yaml
expectedOldValue: "3"
value: "2"
```

This is no longer an accidental conflict. It is an explicit dependency.

The validator should still show it in the dry-run report because load-order dependent patches are more fragile than independent patches.
