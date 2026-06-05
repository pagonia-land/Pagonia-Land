# 🧭 System Guides

These guides explain cross-cutting systems that touch several XML files at once. They are useful after you understand the basic entity, resource, building, production, unit, and objective pages.

## Guides

- [Artifacts](artifacts.md)
  Artifact resources, combat-boost artifacts, treasure hunter links, and safer tracing assumptions.

- [Treasure Hunting](treasure-hunting.md)
  Treasure Hunter building behavior, `AspectTreasureHunter`, treasure hunter recipes, target resources, and treasure areas.

- [Map, POI, And Objective Systems](map-poi-objectives.md)
  Map-specific POIs, objectives, treasure areas, shrines, sanctuary content, artifacts, and campaign flow.

- [Shrines and Sanctuary](shrines.md)
  `AspectShrine`, shrine abilities, shrine recipes, mana production, and DLC sanctuary data.

- [Combat Boosts](combat-boosts.md)
  Combat-boost artifact resources, tags, objectives, difficulty links, and scenario balance notes.

- [Terrain Props, Deposits, And MapGen](world-mapgen.md)
  World props, harvestable deposits, generated deposit hints, map generation templates, and map economy placement.

- [NPC, Encounter, And Combat Systems](npc-encounter-combat.md)
  NPC units, NPC bases, factions, encounter components, raids, bosses, drops, infection rows, and combat-focused generated filters.

- [Tech Tree, Unlocks, And Seasonal Gates](progression-unlocks-seasonal.md)
  Tech tree groups, `NeedsUnlock` gates, objective unlock rewards, DLC gates, seasonal rows, and progression-focused filters.

- [Visual, Audio, And Asset References](visual-audio-assets.md)
  Icons, prefabs, meshes, textures, character kits, VFX, audio events, ambience rows, unit attachments, and visual state components.

## Generated Local Catalogs

Run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\generate_catalog.ps1
```

Then inspect:

```text
generated/catalog/systems/
```

Those generated files are local and ignored by Git.
