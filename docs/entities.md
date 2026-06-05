# 🧩 Entity Model

The Pioneers of Pagonia game database is built around GUID-addressed entities. Every major game concept appears as an `Entity`: resources, buildings, units, recipes, objectives, notifications, terrain props, DLC unlocks, editor data, and campaign-specific objects.

This page explains the structure that appears across the extracted `*.gd.xml` files.

## Basic Shape

Most files use the same outer structure:

```xml
<EntityGroup>
  <Entities>
    <Entity Name="..." Guid="...">
      <Children />
      <Values>...</Values>
    </Entity>
  </Entities>
  <Groups>
    <EntityGroup Name="...">
      ...
    </EntityGroup>
  </Groups>
</EntityGroup>
```

The root `EntityGroup` is a container. The actual database entries are the nested `Entity` nodes.

## Important Fields

| Field | Meaning |
| --- | --- |
| `Name` | Human/editor-readable name. Useful for searching, but not usually the stable reference key. |
| `Guid` | Stable ID and primary reference target. Most cross-links point to this value. |
| `IsAbstract` | Marks template/base entities. These often exist as authoring structures rather than directly playable content. |
| `Children` | Nested entities. This appears to represent editor-side hierarchy or inheritance/template organization. |
| `Values` | Component container. The child elements inside `Values` define what the entity actually does. |

## Components Define Meaning

An entity's meaning comes from the component-like elements inside `Values`.

For example, a building-like entity may combine:

- `Building`
- `Buildable`
- `AspectBuildup`
- `TerrainBlocking`
- `AspectProduction`
- `AspectStorage`
- `VisAudioAmbience`
- `UiBuildingStatesOverwrite`

This means "building" is not a single hardcoded XML type. It is a composition of simulation, UI, placement, visual, and production components.

## Common Component Families

| Prefix or pattern | Typical role |
| --- | --- |
| `Aspect...` | Simulation behavior, such as production, storage, housing, gathering, recruitment, NPC behavior |
| `Vis...` | Visual or audio presentation |
| `Ui...` | UI display, traits, hints, minimap visibility, HUD entries |
| `Objective...` | Objective conditions, rewards, notifications, campaign progression |
| `Unit...` | Unit behavior, animation, attachments, traits, combat/encounter data |
| `MapGen...` | Map generation rules and scenario setup |
| `NeedsUnlock` | Unlock/DLC/progression gating |
| `Tagged...`, `...Tag` | Flexible grouping/filtering for logic and objectives |

The locally generated `generated/entities.json` index stores the component list for every entity in `valueTypes`. See [../generated/README.md](../generated/README.md) for how to generate it.

## Abstract Entities and Children

Many files contain abstract parent entities with concrete child entities below them:

```xml
<Entity Name="Buildable" Guid="..." IsAbstract="true">
  <Children>
    <Entity Name="Construction Camp" Guid="...">
      ...
    </Entity>
  </Children>
</Entity>
```

This structure is important for understanding how the internal editor likely organized the data. It does not mean that all behavior is inherited in a simple XML-only way, but it does show authoring relationships and reusable templates.

The generated entity index captures:

- `parentEntityGuid`
- `parentEntityName`
- `childEntityCount`
- `groupPath`

## Groups

`Groups` are mainly organizational. They arrange entities into authoring sections such as:

- resource categories
- construction menu areas
- campaign objective chains
- dialogue groups
- NPC camps
- tech tree tiers
- editor terrain groups

Runtime links appear to depend primarily on GUIDs and components, not on group names. Still, group paths are very useful for navigation and documentation.

## GUID References

Most relationships are GUID-based. Examples:

- a recipe step references a `Resource`
- a building cost references resources
- a building employment block references worker units
- an objective references a unit, building, resource, tag, or notification
- DLC entities reference core categories and systems

The locally generated `generated/references.json` index records:

- source file/package
- source entity
- source XML element and path
- referenced GUID
- resolved target entity, if found
- whether the GUID is the null/default GUID

## Null GUIDs

The database contains many references to:

```text
00000000-0000-0000-0000-000000000000
```

These should generally be treated as explicit empty/default references, not as broken links.

## Modding Notes

Low-risk areas are usually values that do not affect identity:

- names and descriptions
- icons and UI ordering
- some numeric tuning
- construction costs or recipe amounts, if references remain valid

High-risk areas include:

- changing GUIDs
- deleting entities that other entities reference
- removing components without understanding their role
- changing abstract parent/child structures
- editing campaign objective chains without tracing notifications, rewards, and triggers

For safe modding, keep GUIDs stable and use the local `generated/references.json` file to check what points to an entity before changing or removing it.
