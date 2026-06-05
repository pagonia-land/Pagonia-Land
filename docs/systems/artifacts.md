# 🏺 Artifacts

Artifacts are modeled mostly as resources. This is useful for modding because they can reuse the normal commodity infrastructure: icons, meshes, carry behavior, storage/pile logic, objective checks, and treasure hunter rewards.

The current database contains several artifact-like groups:

- named generic artifacts such as `Artifact 1 Witchwood Harp`
- campaign-specific artifacts such as tutorial or final-map artifacts
- combat-boost artifacts with an additional `CombatBoostArtifact` component
- objective-facing artifact entries used by objective logic

## Main Files

| File | Role |
| --- | --- |
| `game-gdb/core/gdb/resources.gd.xml` | Main artifact resource definitions and combat-boost artifacts |
| `game-gdb/core/gdb/treasurehunterrecipes.gd.xml` | Treasure hunter reward categories and target resource lists |
| `game-gdb/core/gdb/objectives.gd.xml` | Generic artifact-related objective definitions |
| `game-gdb/core/gdb/campaign map *.gd.xml` | Campaign-specific artifacts, objectives, and treasure locations |
| `game-gdb/dlc1/gdb/*.gd.xml` | DLC map, shrine, and objective data that may reference similar systems |

## Resource Model

Most artifacts use `ResourceDescription`.

Important fields:

| Field | Meaning |
| --- | --- |
| `ResourceCategory` | Usually points to the `Artifacts` resource category for normal artifact rewards. |
| `Name` / `NamePlural` | Localization keys, not final translated text. |
| `Description` | Localization key for the resource description. |
| `Icon` | UI icon path. |
| `Mesh` / `PileMesh` | Visual representation for carried or piled resources. |
| `CarryType` | How a unit carries the artifact. |
| `CarryAttachmentLocator` | Locator used for the carried visual attachment. |
| `WealthValue` / `StealValue` | Value-like fields used by some valuable resources. |
| `TreasureHunterAudioDiscoverEvent` | Audio/event category for treasure discovery. |
| `TreasureHunterNotificationTier` | Notification tier used when found by treasure hunters. |

## Combat-Boost Artifacts

Combat-boost artifacts add a `CombatBoostArtifact` component.

Important fields:

| Field | Meaning |
| --- | --- |
| `Boost` | Combat boost value. |
| `Weight` | Likely selection/weighting value for reward logic. |

Some map-specific objectives also check combat boost totals, so combat-boost artifacts are not just visual loot. They can affect scenario balance.

## Treasure Hunter Link

Artifacts are commonly connected to treasure hunting through `TreasureHunterRecipe` entities. The recipe does not produce a fixed output like normal production. Instead, it lists target resources:

```text
Treasure Hunter building
-> AspectTreasureHunter
-> treasure hunter recipe category
-> TargetResources
-> artifact or valuable resource
```

The generated catalog expands this into:

- `generated/catalog/systems/artifacts.md`
- `generated/catalog/systems/treasure-hunter-recipes.md`
- `generated/catalog/systems/treasure-hunter-targets.md`
- `generated/catalog/systems/artifact-treasure-shrine-graph.md`

These generated files are local and ignored by Git.

## Modding Notes

Safer first edits:

- change display localization keys only when you also know how localization is resolved
- adjust icon or mesh paths only if the replacement asset exists
- inspect combat boost values for balance research without editing them yet
- trace treasure hunter target lists before changing any reward category

Riskier edits:

- delete artifacts referenced by objectives or treasure hunter recipes
- change artifact GUIDs
- move an artifact into another resource category without checking storage and UI behavior
- change combat boost values without checking objectives and difficulty settings
- add a new artifact without adding all required UI, mesh, carry, pile, and reward references

## Good First Trace

Start with `Artifact 1 Witchwood Harp`:

1. Find it in `game-gdb/core/gdb/resources.gd.xml`.
2. Note its `ResourceCategory`, icon, carry type, and GUID.
3. Search for the GUID in `game-gdb/core/gdb/treasurehunterrecipes.gd.xml`.
4. Check whether objectives or notifications refer to the same artifact.
5. Regenerate the catalog and inspect `generated/catalog/systems/treasure-hunter-targets.md`.

