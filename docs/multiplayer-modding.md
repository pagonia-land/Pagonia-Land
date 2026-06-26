# Multiplayer and Modded Saves

The 1.4.0 "Quality of Life & Pagonia Editor Update" changed how the game treats enabled mods at save and multiplayer time. These changes are runtime behaviour owned by the engine — this page explains what they mean for modders and mod-set managers, and what Pagonia Land's tools do and don't do about them.

> **Source:** the 1.4.0 official patch notes (Core Game → Mod Changes) and the new localization keys backing the co-op join warnings. Some details below (exact load-order determination, hosted-database co-op distribution) are still open questions — flagged where they are.

## What changed in 1.4.0

- **Active mods are remembered with a save.** The mods enabled when a map is started or saved are recorded; the save can only be loaded again with those mods present. You can still *add* new mods to an existing save, and the UI shows which mods are missing for a load.
- **Achievements are gated.** Achievements can no longer be earned or progressed on modded maps or with mods that contain GameDatabase changes. Starting or loading such a map shows a confirmation dialog.
- **Multiplayer loads only the host's mods.** When you join a friend's session, only mods also in use by the host are loaded. A join-time warning appears if any of the host's mods need subscribing or enabling on your side, and there is a "**GDB hash mismatch**" co-op error path when the merged GameDatabase doesn't line up.
- **Mod DLC dependencies are enforced.** DLC dependencies are shown in the browser, collection, and UGC map selection. A user map cannot be started without the required DLC, and a mod with an unowned-DLC dependency won't load — though you can still subscribe/enable it to play multiplayer with a DLC-owning host.
- **Mods can ship single-language localization** (e.g. a new building's name).

## What this means for modders

### Save-binding: a save needs its mod set

A save now carries the identity of the mods it was created with. Practically:

- **Removing or disabling a mod can block loading a save that used it.** Treat a deployed mod set as part of the save's requirements, not a disposable layer.
- **Adding mods to an existing save is fine** — the binding is "these must be present", not "exactly these and no others".
- If you manage installs with [`pagonia-manager`](../tools/pagonia-manager/CLI.md), a **profile** is the natural unit here: the set of enabled mods + their load order that a given save was built on. Keep the profile that a save depends on intact (the manager's `profile export` captures it as a reproducible [collection](mod-collections.md)).

### Multiplayer: everyone needs the same mods

Co-op only loads mods the host also uses, and a mismatch in the merged GameDatabase surfaces as a "GDB hash mismatch". The reliable way to avoid it is **set parity** — every player enables the same mods.

- Distribute the exact set as a [collection + lockfile](mod-collections.md) so each player installs byte-identical mods. `profile export` → a collection others `collection install` is the one-command path.
- **Load order may matter, not just the set.** The engine merges mods by load order (last-loaded `Replace`/`Unload` wins; `Incremental` stacks — see [the conflict model](mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor)). Whether two players with the *same set* but a *different order* can still diverge is **not yet verified** (the exact rule that makes a mod "last" is not yet confirmed). Until it's confirmed, prefer sharing the load order too (a lockfile pins it), not only the set.
- **Hosted/global mods likely require parity rather than auto-distributing from the host.** Early evidence (a 1.4.0 known issue: a crash when joining without the same mods) leans toward "joiners must have the mods", with map-scoped database travelling with the map. This is still being confirmed.

### DLC dependencies

The game now enforces what the manager already models as **Present / Owned / Effective** ([package layering](package-layering.md)): a mod's required DLC must be present to load, and unowned-DLC mods don't load solo — but a non-owner can still enable such a mod to play co-op with a DLC-owning host. The manager's deploy gate is *advisory* (it warns on ownership, blocks only on absence) because the bytes ship regardless; the game's *runtime* enforcement is the stricter layer on top. If a deployed mod silently won't load solo, an unowned DLC dependency is the first thing to check.

### Achievements

Any mod that changes the GameDatabase (globally or per-map) disables achievement progress on the affected maps. A `classify` of a mod pak tells you in advance whether it carries `global` or `map-scoped` GameDatabase content (`GdbScopes`) — which is exactly the signal that predicts achievement-gating. If achievements matter to you, prefer cosmetic/asset-only mods, or accept the trade-off knowingly.

## What Pagonia Land's tools do (and don't) here

| Concern | What the tools do | What they don't do |
| --- | --- | --- |
| **Byte-identical mod sets across players** | Collections + lockfiles pin exact versions (and content hashes); `profile export` produces a shareable collection | — |
| **Load order** | Lockfiles record and reproduce a profile's load order | Do not (yet) verify two players' orders match — share the lockfile |
| **The game's GDB hash** | — | Do **not** compute or compare the engine's merged-GDB hash; the "GDB hash mismatch" check is the game's, at runtime |
| **DLC dependencies** | Present/Owned/Effective model + co-op carve-out in deploy gating | Don't override the game's runtime enforcement |
| **Cross-mod conflicts** | `plan`/`deploy` warn when two managed mods destructively target the same entity | Can't see inside an editor-authored mod's intent (only `classify` its scope) |
| **Achievement impact** | `classify` exposes `GdbScopes` (the gating signal) | A dedicated "this profile disables achievements" advisory is **planned**, not yet emitted |
| **Co-op parity check** | — | A profile-parity / "what this save needs" command is **planned**, now justified by the real GDB-hash path |

## See also

- [Mod Collections](mod-collections.md) — pinning a reproducible mod set + load order with lockfiles.
- [Mod Patch Conflict Model](mod-conflicts.md) — how overlapping mods resolve and the authoring advisor.
- [Editor Mods and Pagonia Land](editor-interop.md) — how editor-authored mods fit alongside hand-authored ones.
- [Mod Distribution Patterns → Distribution Channels](mod-distribution.md#distribution-channels) — how a shared set actually reaches every player.
