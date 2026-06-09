# Mod Patch Schemas

This folder contains the v0.1 JSON Schemas for Pagonia Land patch-style mods. These schemas are the **public contract** — anything that consumes Pagonia Land mod manifests (the patcher, `pagonia-manager`, a future GUI on top of `Manager.Core`, IDE plugins, EE's own tooling, web validators) reads against them.

The schemas are intentionally small and conservative. They support:

- editor validation for `mod.yaml` and patch files
- the `pagonia-patcher` command line tool
- mod managers (`pagonia-manager` does this today; future GUIs do the same) that validate archives before installation
- automated checks in CI

## Reference Implementation And Drift Protection

`pagonia-patcher schema-validate` is the reference implementation of this contract. It embeds the schemas in this folder into the patcher binary and validates `mod.yaml`, patch files, collections, and lockfiles against them.

Two safeguards keep the schemas, the patcher's C# code, and the sandbox examples in sync:

- **[`scripts/preflight.ps1`](../../scripts/preflight.ps1)** runs `schema-validate` against every sandbox example before any commit. If a schema change breaks an example, preflight catches it locally.
- **[`.github/workflows/tools.yml`](../../.github/workflows/tools.yml)** runs the same preflight on every push and PR. Schemas, code, and examples cannot drift past CI undetected.

When you add a new field to a model in `tools/pagonia-patcher/src/PagoniaLand.Patcher.Core/Models/`, add the matching property to the schema here in the same PR. The CI's `schema-validate` step will fail loudly on the first sandbox example that uses the new field if the schema doesn't know it yet — that's how we caught the missing `pak:` block when `schema-validate` first ran. See [CONTRIBUTING.md → When You Touch Tools Or Schemas](../../CONTRIBUTING.md#when-you-touch-tools-or-schemas) for the rule.

## Files

| Schema | Purpose |
| --- | --- |
| [mod.schema.json](mod.schema.json) | Validates the root `mod.yaml` manifest |
| [patch-file.schema.json](patch-file.schema.json) | Validates YAML files listed under `patches` or `patchSets[].patches` |
| [collection.schema.json](collection.schema.json) | Validates curated collection manifests |
| [collection-lock.schema.json](collection-lock.schema.json) | Validates resolved collection lockfiles |
| [repo-index.schema.json](repo-index.schema.json) | Validates a Git-distribution repo's top-level `index.yaml` catalog (mods + collections offered by the repo) |
| [catalog.schema.json](catalog.schema.json) | Validates a `catalog.yaml` — a curated list of mod-distribution repos plus optional federated references to other catalogs. The manager aggregates across every catalog the user is subscribed to. |

JSON Schema can validate YAML after the YAML file has been parsed into normal JSON-like data.

## Current Scope

The first version focuses on data shape:

- required mod identity fields
- exact `gameDatabaseVersion`
- required and optional package declarations
- patch file paths
- optional patch sets
- basic mod manager metadata
- patch operations and target fields
- curated collection manifests
- resolved collection lockfiles

It does not prove that a target exists in the XML. That belongs in the patcher itself, which already resolves targets, applies clean plans, and reports conflicts.

## Example Manifest

```yaml
patchFormatVersion: 0.1
id: pagonia-land.example.cheaper-sawmill
name: Cheaper Sawmill
version: 0.1.0
author: Example Modder
gameDatabaseVersion: "1.3.2-11873+194094"
description: Lowers one Sawmill construction cost for local testing.
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

## Example Patch File

```yaml
operations:
  - id: cheaper-sawmill-softwood
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

## Notes For Tool Authors

A patcher should validate in layers:

1. Parse YAML.
2. Validate against these schemas.
3. Check local package folders.
4. Check `gameDatabaseVersion`.
5. Resolve targets in XML.
6. Check expected old values.
7. Build a combined patch plan.
8. Detect conflicts across all enabled mods.
9. Apply only after a clean dry run.

The schema is not a replacement for the patcher. It is the first gate.

For collection design, see [Mod Collections](../../docs/mod-collections.md).

For small example manifests, see [Collection Examples](../../docs/examples/collections/README.md).
