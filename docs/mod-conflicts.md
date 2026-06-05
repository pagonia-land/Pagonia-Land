# 💥 Mod Patch Conflict Model

This page describes how patch-based mods are checked before they are applied.

The goal is to catch the most common ways mods break each other:

- two mods changing the same value
- one mod removing data another mod needs
- a patch written for an older game database version
- a DLC/package patch being applied without the required package
- duplicate GUIDs in newly added entities
- risky changes that need manual review

The repository ships the Pagonia Land Patcher, which implements the v0.1 of this conflict model: it detects duplicate write targets, mismatched expected values, missing packages, and unsupported operations, and it blocks `apply` until the combined plan is clean.

## Conflict Levels

| Level | Meaning | Suggested Result |
| --- | --- | --- |
| Info | The patch is independent and expected values match | Apply allowed |
| Warning | The patch is probably valid, but touches related data | Apply only after review |
| Conflict | Two or more mods write incompatible changes | Do not apply automatically |
| Error | Target missing, old value mismatch, invalid XML, duplicate GUID, or broken reference | Do not apply |

## Target Conflicts

A target conflict happens when two operations write the same target:

```text
file + entityGuid + component + path
```

Example:

```text
Mod A:
Sawmill construction Softwood Trunk amount: 4 -> 3

Mod B:
Sawmill construction Softwood Trunk amount: 4 -> 2
```

This should be a blocking conflict unless one mod explicitly declares it should load after the other and expects the changed value.

Safe chained example:

```yaml
Mod A:
  expectedOldValue: "4"
  value: "3"

Mod B:
  loadAfter:
    - mod-a
  expectedOldValue: "3"
  value: "2"
```

That is still worth reporting, but it is no longer accidental.

## Expected Value Mismatch

An expected value mismatch is the most important safety check.

Example:

```yaml
expectedOldValue: "4"
value: "3"
```

If the current database value is already `5`, the patch should stop.

Possible reasons:

- the game updated
- another mod already changed the value
- the mod was written against a different snapshot
- the patch target is too broad or wrong

The safe behavior is to fail closed and ask for review.

## Remove Conflicts

Remove operations are dangerous because other mods may depend on the removed node.

Example:

```text
Mod A removes a recipe from a building.
Mod B changes the same recipe's output.
```

This should be a blocking conflict if both mods are enabled.

For this reason, first-generation mod patches should avoid `removeEntity` and use `removeListItem` only with very specific targets.

## Add Conflicts

Adding data can conflict too.

Common add conflicts:

| Conflict | Example |
| --- | --- |
| Duplicate GUID | Two mods add different entities with the same GUID |
| Duplicate technical name | Two mods add `MyNewDecoration` |
| Duplicate recipe identifier | Two mods add the same `RecipeIdentifier` |
| Duplicate build menu slot or sort order | Two mods insert content into the same UI location |
| Missing dependency | A mod adds a recipe that references a resource from another disabled mod |

GUID conflicts must be blocking errors.

Name or sort-order conflicts may be warnings unless the game requires uniqueness.

## Semantic Conflicts

Some conflicts are not direct writes to the same XML path.

Examples:

- Mod A changes `WineRecipe` output.
- Mod B changes `Winery` production links.
- Mod C changes `Wine` resource category or carry type.

These mods do not necessarily conflict, but they touch the same gameplay chain.

A future validator can detect this through generated catalogs:

```text
resource-flow
building-production
production-chains
unit-equipment-matrix
objective-flow
```

Suggested result: warning, not automatic failure.

## Version Drift

Every mod should declare the snapshot it was authored against:

```yaml
gameDatabaseVersion: "1.3.1-11826+193733"
```

The validator should compare this with the local current snapshot. Prefer exact program versions such as `1.3.1-11826+193733` over descriptive labels.

If the snapshot differs:

- continue only in dry-run mode
- require all `expectedOldValue` checks to pass
- show a clear warning that the mod has not been reviewed for this database version

## Package And DLC Conflicts

Mods should declare required and optional packages:

```yaml
requiredPackages:
  - core
optionalPackages:
  - dlc1
  - decorations1
```

Patch sets can also have package requirements:

```yaml
patchSets:
  - id: dlc1-support
    optional: true
    requiresPackages:
      - dlc1
    patches:
      - patches/dlc1.yaml
```

Suggested behavior:

| Situation | Result |
| --- | --- |
| Required package missing | Error, do not apply |
| Optional package missing | Skip optional patch set and report info |
| Patch file targets missing package folder | Error unless it belongs to skipped optional patch set |
| Core patch references DLC entity but `dlc1` is not required | Warning or error depending on operation |
| DLC patch writes core entities | Allowed, but report as cross-package patch |

This matters because `dlc1` extends the base database with new resources, buildings, recipes, objectives, units, NPC data, map data, and cross-package references. A mod can be valid for a DLC-enabled setup and invalid for a core-only setup.

## Load Order

Load order should be explicit when needed and irrelevant when possible.

Good mod design:

- independent patches
- exact targets
- expected old values
- minimal writes

Load order fields:

```yaml
loadAfter:
  - other.mod.id
loadBefore:
  - another.mod.id
```

If two mods require incompatible ordering, the validator should report a load-order cycle.

## Risk Score

Each operation can declare a risk level:

```yaml
risk: low
```

Suggested defaults (the values shown are the literal schema enum — note the
hyphen in `very-high`):

| Operation | Default `risk:` |
| --- | --- |
| `replaceValue` | `low` |
| `replaceAttribute` | `medium` |
| `addListItem` | `medium` |
| `mergeComponent` | `medium` |
| `removeListItem` | `high` |
| `replaceNode` | `high` |
| `addEntity` | `high` |
| `removeEntity` | `very-high` |

The `risk:` field accepts only `low`, `medium`, `high`, or `very-high`. Risk is not validation. It is a review hint for humans.

## Recommended First Rule Set

For early community experiments:

1. Allow `replaceValue`.
2. Require `expectedOldValue`.
3. Require `entityGuid`.
4. Require dry-run output.
5. Require exact `gameDatabaseVersion`.
6. Check required packages.
7. Skip optional patch sets when their package is missing.
8. Block duplicate write targets.
9. Block duplicate added GUIDs.
10. Warn on snapshot mismatch.
11. Warn on semantic chain overlap.
12. Run `scripts\validate_database.ps1` after applying.
13. Keep backups or work from a copied test game directory.

This keeps the first patch format practical without making it too clever too early.
