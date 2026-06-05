# 💡 Worked Examples

This directory contains practical, step-by-step examples for learning the game database safely.

The examples are intentionally small. They are meant to teach tracing, editing discipline, validation, and testing habits rather than to provide complete ready-to-publish mods.

**New here?** Work down the page in order: the **traces** teach you to read the data without changing anything, the **small edits** teach you to change one thing safely, and the **patch / collection / repo** examples show how to package and ship what you built.

## Traces

Read-only walkthroughs — follow a value across files without editing anything. The safest place to start.

| Example | What it traces |
| --- | --- |
| [Trace The Sawmill Production Chain](trace-sawmill-chain.md) | Softwood trunk to firewood through the sawmill: building → production recipe → resources, across files. |
| [Trace A Full Production Chain](trace-full-production-chain.md) | A longer multi-step chain (hammer from iron) end-to-end, following every input and output step. |
| [Trace A Resource From Deposit To Building](trace-resource-deposit-building.md) | A raw resource from its terrain deposit through extraction to the building that consumes it. |
| [Trace A DLC Production Addition](trace-dlc-production-addition.md) | How a `dlc1` package layers a new or extended production recipe on top of the core database. |
| [Trace A Map Objective](trace-map-objective.md) | An objective from its definition through its notifications, completion triggers, and rewards. |
| [Trace An Artifact Through Treasure And Shrine Systems](trace-artifact-treasure-shrine.md) | A single artifact across the treasure-hunting and shrine / sanctuary systems. |
| [Trace Unit Attachments](trace-unit-attachments.md) | How a unit's attachment and equipment references hang together. |
| [Trace A Tech Tree Unlock](trace-tech-tree-unlock.md) | A tech-tree node and what unlocking it gates. |
| [Trace A Tools Editor Terrain Sediment](trace-tools-editor-sediment.md) | A terrain sediment entry in the `tools/` editor data. |

## Small Edits

Make one change, validate, test. Each page changes a single value or adds one variant.

| Example | What you change |
| --- | --- |
| [Change A Building Construction Cost](change-building-cost.md) | One resource amount in a building's construction cost. |
| [Change A Recipe Resource Amount](change-recipe-resource-amount.md) | How much of a resource a production recipe consumes or produces. |
| [Change A Resource Display Name Or Icon](change-resource-display-icon.md) | A resource's UI name or icon, leaving its behaviour untouched. |
| [Change A Unit Recruitment Cost](change-unit-recruitment-cost.md) | What a unit costs to recruit. |
| [Create A Decoration Variant](create-decoration-variant.md) | Add a new decoration by cloning an existing one. |

## Mod Patch Examples

Complete `mod.yaml` patch files showing the declarative format end-to-end. See [Mod Patch Examples](mod-patches/README.md) for a cheaper-sawmill patch, an optional DLC patch set, a resource icon patch, and an intentional conflict.

## Collection Examples

See [Collection Examples](collections/README.md) for `*.collection.yaml` manifests, plus a resolved lockfile, that bundle several mods into one curated set.

## Repo & Catalog Layouts

Reference layouts for packaging and publishing your work — full, schema-validated artifacts you fork as a starting point, not single edits.

| Layout | What it shows |
| --- | --- |
| [Sandbox Templates](../../sandbox/examples/README.md) | Starter mod folders under `sandbox/examples/` to copy into `sandbox/mods/` and edit. |
| [Mod-Repo Layouts](../../examples/README.md) | Overview of the runnable, schema-validated reference layouts shipped under `examples/`. |
| [Multi-Mod Repo Example](../../examples/mod-repo-example/README.md) | A Git-hostable mod repo: two example mods plus a collection that ties them into a ready-to-play preset. |
| [Mod Catalog Example](../../examples/mod-catalog-example/README.md) | A curated catalog of mod-distribution repos, with federated references the manager aggregates. |

## Before Running Any Example

Read:

- [Local Setup](../setup.md)
- [GUID Reference](../guid-reference.md)
- [Common Fields and Patterns](../common-fields.md)
- [Modding Risk Map](../modding-risk-map.md)
- [Declarative Mod Patch Format](../mod-patch-format.md)
- [Mod Patch Conflict Model](../mod-conflicts.md)
- [Troubleshooting](../troubleshooting.md)
- [Map, POI, And Objective Systems](../systems/map-poi-objectives.md)

Run validation before you edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\validate_database.ps1
```

Regenerate the local catalog:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

## General Rule

Make one change, validate, test, and write down what changed. Most database problems become much easier to understand when each experiment has only one moving part.
