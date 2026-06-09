# Pagonia Land Manager — CLI Contract

This page is the user-facing contract for `pagonia-manager.exe`. Every command, flag, exit code, and diagnostic code that the manager promises to downstream tools (scripts, CI, a future GUI) is documented here. Match against the `Code` field rather than human-readable messages — message wording can be tweaked between releases, the codes cannot.

## What This Tool Covers

Store management, the local mod install lifecycle (`install` / `uninstall` / `list`), active-profile mod selection (`enable` / `disable` / `move` / `status`), named profiles (`profile create` / `list` / `use` / `copy` / `export` / `delete` / `show`), local collection install (`collection install` / `list` / `show` / `uninstall`), dry-run planning (`plan`), full deploy + rollback for both XML patches and Pattern B overlay paks (`deploy` / `rollback` / `deploy-status` / `deploy-list`), and schema-validated JSON reports on every state-changing command (`schema-validate`). Native AOT publishes a single-file binary on Windows / Linux / macOS Intel / macOS Apple Silicon.

## CLI Args vs Interactive Mode

`pagonia-manager` with **no arguments + a TTY** launches an [interactive shell](README.md#interactive-mode-default) (Spectre.Console-based, task-grouped main menu + an Advanced submenu that mirrors every CLI verb). With **any CLI arg** or **redirected stdin** it behaves as documented below — the scripted contract. The two modes share 100% of the underlying logic; the shell is just a different front-end onto the same services and emits the same diagnostic codes.

Use scripted mode for CI, automation, deployment scripts, mod-manager-GUI integrations. Use interactive for hand-driven sessions where you'd otherwise be looking up flag names. The choice is per-invocation; nothing persists.

## Implemented Commands

```powershell
pagonia-manager --version                                  # print product name and version
pagonia-manager -v                                         # alias for --version
pagonia-manager --info                                     # print backing core library versions (patcher + paker)

pagonia-manager store init [--store <path>]                # create the mod store layout at the resolved root
pagonia-manager store info [--store <path>]                # print resolved root, store schema version, and counts

pagonia-manager install --from <folder|zip|gh:owner/repo[:base][#ref]/mod-id|https://.../mod.zip|modio:<game>/<mod-id>[#<ver>]> [--store <p>]
                                                           # validate and install a mod into the store
                                                           # :base names a subdirectory holding the repo's index.yaml (e.g. gh:pagonia-land/Pagonia-Land:official-mods/<mod-id>)
pagonia-manager uninstall <mod-id> [--version <v>] [--store <p>]
                                                           # remove a mod version (or the only one if unique)
pagonia-manager list [--store <path>]                      # show installed mods with id+version+sidecar metadata

pagonia-manager enable <mod-id> [--version <v>] [--store <p>]
                                                           # enable a mod in the active profile (--version defaults to most recently installed)
pagonia-manager disable <mod-id> [--store <p>]             # remove a mod from the active profile's enabled list + load order
pagonia-manager move <mod-id> --position <n> [--store <p>] # reorder a mod (1-based position)
pagonia-manager move <mod-id> --before <other-id> [--store <p>]
pagonia-manager move <mod-id> --after  <other-id> [--store <p>]
pagonia-manager status [--store <path>]                    # active profile + enabled mods in load order

pagonia-manager profile create <name> [--store <p>]        # create a new empty profile
pagonia-manager profile list [--store <p>]                 # list all profiles (* marks active)
pagonia-manager profile use <name> [--store <p>]           # switch the active profile
pagonia-manager profile copy <source> <target> [--activate] [--store <p>]   # duplicate a profile (snapshot / experiment branch)
pagonia-manager profile export [<name>] --out <file.collection.yaml> [--id <id>] [--name <n>] [--version <v>] [--store <p>]   # export a profile as a shareable collection
pagonia-manager profile delete <name> [--store <p>]        # delete a non-default, non-active profile
pagonia-manager profile show [<name>] [--store <p>]        # show a profile (defaults to active)

pagonia-manager tweak list <mod-id> [--profile <name>] [--store <p>] [--json <out>]
                                                           # show a mod's tweaks + current values + origin
pagonia-manager tweak set <mod-id> <tweak-id> <value> [--profile <name>] [--store <p>] [--json <out>]
                                                           # store a per-profile tweak override (validated)
pagonia-manager tweak reset <mod-id> [<tweak-id>] [--profile <name>] [--store <p>] [--json <out>]
                                                           # drop one override, or all of a mod's overrides

pagonia-manager collection install --from <file|gh:owner/repo[:base][#ref]/id> [--mods-root <p>] [--as-profile <name>] [--activate] [--overwrite] [--store <p>]
                                                           # resolve + install a local collection manifest
pagonia-manager collection list [--store <p>]              # list installed collections
pagonia-manager collection show <id> [--store <p>]         # show a collection's details
pagonia-manager collection uninstall <id> [--store <p>]    # remove a collection's manifest + lockfile

pagonia-manager catalog list [--store <p>]                 # list subscribed catalogs
pagonia-manager catalog add <gh:owner/repo|file:path> [--store <p>]
                                                           # subscribe to a catalog
pagonia-manager catalog remove <gh:owner/repo|file:path> [--store <p>]
                                                           # unsubscribe from a catalog
pagonia-manager catalog browse [--store <p>]               # show every repo across subscribed catalogs (federated, dedup'd)
pagonia-manager catalog show <gh:owner/repo|file:path> [--store <p>]
                                                           # show one catalog's repos + federation refs
pagonia-manager catalog refresh [<gh:owner/repo|file:path>] [--store <p>]
                                                           # force re-fetch (all subscriptions, or one named source)

pagonia-manager expansions list [--game <path>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>]
                                                           # show each expansion's Present / Owned / Effective state
pagonia-manager expansions set <decorations1|dlc1> <owned|not-owned|unknown> [--game <path>] [--store <p>] [--json <out>]
                                                           # declare whether this install owns a DLC expansion

pagonia-manager plan --game <path> [--profile <name>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>] [--out <md>]
                                                           # dry-run patch plan for the active (or named) profile

pagonia-manager deploy --game <path> [--profile <name>] [--accept-warnings] [--force] [--dry-run] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>]
                                                           # apply the active (or named) profile to the game install (--force overwrites out-of-band changes)
pagonia-manager rollback --game <path> [--force] [--store <p>] [--json <out>]
                                                           # undo the last deploy for this game install (--force restores over out-of-band changes)
pagonia-manager deploy-status --game <path> [--store <p>]  # show detected game version + last deploy timestamp + profile
pagonia-manager deploy-list --game <path> [--store <p>]    # list all retained deploys for the game

pagonia-manager deploys list-orphans [--store <p>]         # list deploy dirs whose game install moved or updated
pagonia-manager deploys clean --keep <N> [--game <p>] [--dry-run] [--store <p>]
                                                           # trim deploy backups to N most recent per fingerprint

pagonia-manager schema-validate --kind <k> --report <path>  # validate a JSON report against its schema
                                                           # kinds: install, uninstall, deploy, rollback,
                                                           #        collectionInstall, status, deployStatus,
                                                           #        tweakList, tweakSet, tweakReset,
                                                           #        expansionsList, expansionsSet
```

`--json <out>` is supported on `install`, `uninstall`, `deploy`, `rollback`, `collection install`, `status`, `deploy-status`, `deploy-list`, `plan`, the three `tweak` verbs, and the two `expansions` verbs. Each writes its report to the given path atomically. (`deploy-list --json` emits a `deployStatus`-kind report — its `deploys[]` array carries every retained deploy — so validate it with `schema-validate --kind deployStatus`.)

`--info` is useful when diagnosing a build to confirm which Patcher.Core / Paker.Core versions a given `pagonia-manager.exe` was linked against.

## Mod Store

The store is the on-disk tree where the manager keeps installed mods, profiles, collection lockfiles, and its own state. Layout (v0.1):

```text
<store-root>/
  mods/<mod-id>/<version>/                # extracted mod content + .manager-install.yaml sidecar
  profiles/<name>.profile.yaml            # per-profile enabled-mod list + load order
  collections/
    <id>/<version>/                       # locally cached collection manifests
    locks/<id>.lock.yaml                  # last resolved lockfile per collection
  state.yaml                              # active profile, last deploy info, store schema version
```

### Store Root Resolution

Resolved in this order (first non-empty source wins):

1. `--store <path>` flag on the command line
2. `PAGONIA_MANAGER_STORE` environment variable
3. Platform default: `Environment.SpecialFolder.LocalApplicationData` + `PagoniaLand/Manager/`
   - Windows: `%LOCALAPPDATA%\PagoniaLand\Manager\`
   - Linux: `$XDG_DATA_HOME/PagoniaLand/Manager` (default `~/.local/share/PagoniaLand/Manager`)
   - macOS: `~/.local/share/PagoniaLand/Manager`

Every command that touches the store prints which source the resolved root came from.

### Default Catalog Subscription

A fresh `store init` seeds the new store with the official catalog as its one default subscription (`gh:pagonia-land/Pagonia-Land/catalog/official.yaml`), so `catalog browse` has something to show on a brand-new install. It is surfaced as a `manager.defaultCatalogSeeded` info line. The seed is an **ordinary entry** in `state.yaml.subscribedCatalogs` — opt out with `catalog remove gh:pagonia-land/Pagonia-Land/catalog/official.yaml` and it stays gone (re-running `store init` against an existing store never re-adds it; only a brand-new store is seeded). The interactive shell's first-run setup seeds it the same way.

### Atomic Writes

Every write the manager makes goes to a sibling `*.tmp` file and is then renamed into place via `File.Move(..., overwrite: true)` (atomic on Windows when source and destination share a volume). Reads tolerate a leftover `*.tmp` from a crashed previous run and ignore it. `AtomicFile.CleanupLeftoverTempFiles(directory)` is available for tools that want to actively prune stale temps.

### Install Sidecar

Each installed mod version directory contains a `.manager-install.yaml` sidecar written by `install` and read by `list`:

```yaml
installedAt: 2026-05-29T12:00:00Z
sourcePath: C:\Users\me\Downloads\cheaper-sawmill-0.1.0.zip
sourceType: zip
manifestName: Fixture Cheaper Sawmill
source: ""
```

For installs originating from a remote source (see [Install Sources](#install-sources) below), the `source` field is populated with the transport-neutral identifier that pins the exact commit fetched, for example `gh:pagonia-land/example-mods#a1b2c3d4e5f6.../pagonia-land.example.cheaper-sawmill`. Local folder / zip installs leave it empty.

The sidecar is not part of the mod itself — it's the manager's own bookkeeping. Deleting it does not break the install; `list` will simply show `(unknown)` for the missing fields.

### Install Sources

`install --from <source>` accepts these source forms:

| Form | Status | Behaviour |
|---|---|---|
| `<folder>` | available | Folder kept in place; pipeline runs against it directly. |
| `<file.zip>` | available | Extracted to a temp directory, then the temp dir feeds the same pipeline as a folder. Cleaned up after install. |
| `gh:<owner>/<repo>[:<base>][#<ref>]/<mod-id-or-path>` | available | Fetched from GitHub via `raw.githubusercontent.com`. The optional `:<base>` scopes lookups to a repo subdirectory (so a repo can host its mod tree under a subpath); see the command synopsis above and the lockfile section below. The `<ref>` (branch, tag, or commit SHA; defaults to `HEAD`) is resolved to a concrete commit SHA up front so the sidecar's `source` field pins exact code even after the branch moves. If the repo ships an `index.yaml` catalog, `<mod-id-or-path>` is looked up as a mod id first, then as a repo-relative folder path; without an `index.yaml`, the value is treated as a folder path verbatim. |
| `https://github.com/<owner>/<repo>/tree/<ref>[/<path>]` | available | Long form of `gh:` — the URL copied from GitHub's web UI. Parsed into the same `GitHubSource` as the short form. |
| `https://example.com/<mod>.zip` (any HTTP host) | available | Direct-URL ZIP source. The URL is the source identity; no auth, no rate limit, no registry lookup. The download is streamed straight into a temp file while being hashed (SHA-256); zip-slip path-traversal entries are refused at extract time; a nested-folder heuristic drills into a single top-level wrapper directory if the ZIP has one. The sidecar records `source: url:<url>#<sha256>` so the installer's drift detection can warn on re-install when the same URL serves different bytes than before. URLs whose path doesn't end in `.zip` are rejected to avoid false positives on repo landing pages and documentation links. |
| `http://example.com/<mod>.zip` | opt-in | Same as above but plain HTTP. Always emits the `manager.directUrlInsecureHttp` warning; install only proceeds when `state.yaml.allowInsecureSources: true` is set explicitly. Useful for locked-down corporate / LAN deployments with known-trusted internal hosts; never on by default. |
| `modio:<game>/<mod-id>[#<version>]` | available (Map-only for PoP today) | mod.io adapter. The fetcher calls `https://api.mod.io/v1/games/<game>/mods/<mod-id>` to resolve the mod's metadata + a pre-signed download URL, then delegates to the direct-URL flow above for the actual ZIP fetch. `<game>` accepts either a numeric mod.io game id (Pioneers of Pagonia is `8242`), the long slug `pioneers-of-pagonia`, or the short alias `pop` — all three resolve to the same numeric id. **For Pioneers of Pagonia today**, the only configured type is "Map" — those mods are managed by the game's in-game UGC subscription list (settings → mods → mod.io), not by `pagonia-manager install`. The adapter detects Map-type mods + aborts cleanly with `manager.modIoMapTypeSkipped`. Authentication uses a read-only API key — set `PAGONIA_MODIO_API_KEY` to inject your own (recommended for isolated rate-limit buckets); the binary also ships with an embedded default key (currently empty until first public release). **No personal OAuth token required** — read-only ops don't need one. |

The `gh:` flow downloads only the files the manifest references (`mod.yaml` plus each entry under `patches:` and `patchSets[*].patches`), into a temp directory laid out exactly like a local mod folder, then hands that temp directory off to the same install pipeline as a local install. Patch files containing `..` traversal are refused before any network request is issued.

The direct-URL ZIP flow downloads the whole archive (it's just a ZIP — no manifest-walking) and extracts it. ZIPs wrapped in a single top-level folder ("`my-mod-v1.0/mod.yaml`") are handled transparently — the fetcher drills in once so `ModInstaller` sees `mod.yaml` at the directory root.

mod.io's `modio:<game>/<mod-id>` source type reuses the direct-URL pipeline: mod.io's REST API returns a pre-signed download URL, which is then fed to `DirectUrlFetcher` like any other URL.

The interactive **Install a Mod** wizard accepts these same source forms (local path, `gh:`, `modio:`, or a `.zip` URL): a remote spec is resolved into a temp dir by the shared `InstallSourceResolver` — the same dispatch the scripted command uses — and then offered for enabling in the active profile. (You can also reach a catalog's mods without typing a `gh:` spec at all via [*Browse Community Catalogs*](#interactive-drill-in).)

### Install Pipeline

`install` runs the mod through the patcher's validation stack before touching the store:

1. Resolve the source — folder kept in place; `.zip` extracted to a temp directory; `gh:` / `https://github.com/` fetched into a temp directory.
2. `ManifestReader.ReadMod(stagingRoot)` — typed YAML parse.
3. `ManifestValidator.ValidateMod(loadedMod)` — field-level checks.
4. `SchemaValidator.ValidateMod(stagingRoot)` — JSON Schema conformance against [`schemas/mod-patches/mod.schema.json`](../../schemas/mod-patches/mod.schema.json).
5. **Conflict-minimising authoring advisor** — for a mod that ships its own GameDatabase overlay (`*.gd.xml`), the manager runs the patcher's `EntityRelationAdvisor` over it and surfaces conflict-risk findings (`usesDestructiveInheritanceMode`, `unloadsReferencedEntity`, `inheritanceConflictRisk`). These are **advisory** — Info notices plus an unload-dangling Warning, never an Error — so they inform without blocking. Install runs the base-free checks; the base-aware ones (cross-database unload, `replaceCouldBeIncremental`) are available interactively under *Advanced → Mods → advise*, which can take a game root.

Any error diagnostic from stages 1–4 aborts the install — nothing is written to `<store>/mods/`. Patcher diagnostics (validation **and** advisor) surface **verbatim** through the manager's report (see the diagnostic-code conventions below). A duplicate install (same `id@version`) is a `manager.modAlreadyInstalled` **warning**, not an error — the existing install is preserved untouched.

## Profiles

The store can carry any number of named profiles, each its own enabled-mod list + load order. One profile is active at a time; its name is recorded in `state.yaml` and every `enable` / `disable` / `move` / `status` command targets it. A fresh `store init` creates and activates the `default` profile.

### Profile File Shape

Each profile lives at `<store>/profiles/<name>.profile.yaml`:

```yaml
profileVersion: "0.1"
name: dlc1-testing
collection: pagonia-land.collections.beginner-qol   # optional; set by `collection install`
enabledMods:
  - id: pagonia-land.example.cheaper-sawmill
    version: "0.1.0"
    tweaks:                                          # optional; per-mod user-supplied tweak overrides
      softwood-cost: "3"
loadOrder:
  - pagonia-land.example.cheaper-sawmill
```

The `collection` field is left null for hand-created profiles; `collection install` sets it when a profile is created from a collection so a future re-resolve can refresh it.

#### `tweaks` (per enabled mod)

Each `enabledMods[]` entry may carry an optional `tweaks` map of `<tweak-id>: <value>` overrides, keyed by the tweak ids the mod declares in its `mod.yaml` (see the patcher's [tweaks block](../../docs/mod-patch-format.md#tweaks-tweaks-block)). Values are stored as raw scalar strings — the mod's declaration owns the type, and the override is validated against it when resolved. Three meanings:

- **absent / null** — no overrides recorded; every tweak resolves to its mod-declared default.
- **`tweaks: {}`** (empty map) — explicitly "no overrides".
- **non-empty** — user-supplied values that override the mod's defaults.

**Compatibility.** The field is optional. `profileVersion` is `"0.1"`; a profile that omits the `tweaks` key keeps loading — every mod reads back as "use defaults". A version change on an enabled mod (`enable <mod> --version <new>`) preserves its stored tweak overrides, since tweak ids are stable across versions (a rename is handled via the mod's `aliases:` field).

### Profile Name Rules

Profile names must satisfy:

- non-empty, no leading/trailing whitespace, at most 64 characters
- not `.` or `..`, no leading `.` (so they never become hidden files on Unix)
- no `/`, `\`, `:`, `*`, `?`, `"`, `<`, `>`, `|` (so they're always valid file basenames on every OS we ship to)

Invalid names are rejected with `manager.profileNameInvalid` before any disk write.

### Lifecycle Guarantees

- **`profile create <name>`** writes an empty `<name>.profile.yaml` with `profileVersion: "0.1"`. Duplicate names are rejected with `manager.profileAlreadyExists`.
- **`profile use <name>`** updates `state.yaml.activeProfile` atomically (read-modify-write via `AtomicFile`). Using the already-active profile is a noop. Using a missing profile is rejected with `manager.profileMissing`.
- **`profile copy <source> <target>`** duplicates an existing profile — enabled mods, load order, tweak overrides, and the collection link — under a new name, for a local snapshot before a risky change or an experiment branch. The copy is a value copy: later edits to either profile leave the other untouched. Rejected if `<target>` already exists (`manager.profileAlreadyExists` — delete it first; there is no implicit overwrite) or `<source>` is missing (`manager.profileMissing`); an info `manager.profileCopied` confirms success. `--activate` switches the active profile to the copy through the same path as `profile use`, leaving the source intact.
- **`profile export [<name>] --out <file.collection.yaml>`** writes the active (or named) profile out as a shareable collection manifest. The profile's enabled mods + load order become the collection's `mods` + `loadOrder`; each mod's per-profile tweak overrides fold into `mods[].tweaks`; each mod's `source:` is recovered from its install provenance (the install sidecar, then the originating collection's lockfile when the profile is collection-pinned), falling back to `source: "local"` with a `manager.profileExportLocalSource` warning when nothing remote is recoverable (that entry won't fetch elsewhere without a matching `--mods-root`). Optional `--id` / `--name` / `--version` set the collection metadata (`--id` defaults to `pagonia-land.profiles.<profile>`). The generated manifest is schema-validated against `collection.schema.json` **before** it is written — a malformed export aborts rather than emitting an invalid file. A profile with no enabled mods is refused with `manager.profileExportEmpty` (a collection requires at least one mod). Success surfaces `manager.profileExported` (info). This is the shareable counterpart to `profile copy`; for a bit-identical reproduction, share the lockfile instead.
- **`profile delete <name>`** removes the file. **Always rejected for `default`** (`manager.profileDefaultDeletion` — the default is the safety net that `store init` re-creates if needed). **Always rejected for the active profile** (`manager.profileActiveDeletion` — switch away first).
- **`profile show [<name>]`** without a name prints the active profile; otherwise the named one. Same renderer as `status`.

### Mod Selection Lives Per-Profile

The `enable` / `disable` / `move` / `status` commands always operate on the **currently active profile**. To enable a mod in a different profile, `profile use` it first, then `enable`. Switching back via `profile use` restores the other profile's state untouched — mod selection is fully profile-scoped.

## Tweaks

> For the feature overview across author / curator / player, see the [Mod Tweaks guide](../../docs/mod-tweaks.md). This section is the player-facing CLI reference.

When a mod author marks values as user-adjustable (a [`tweaks:` block](../../docs/mod-patch-format.md#tweaks-tweaks-block) in `mod.yaml`), you tune them per profile without editing the mod — a cost multiplier, an on/off flag, a difficulty enum. Overrides are stored on the profile's enabled-mod entry (`enabledMods[].tweaks`, `profileVersion: "0.1"`); a `plan` / `deploy` substitutes them into the mod's `{{ tweaks.<id> }}` placeholders before applying.

```powershell
pagonia-manager tweak list <mod-id> [--profile <name>] [--store <p>] [--json <out>]
pagonia-manager tweak set  <mod-id> <tweak-id> <value> [--profile <name>] [--store <p>] [--json <out>]
pagonia-manager tweak reset <mod-id> [<tweak-id>] [--profile <name>] [--store <p>] [--json <out>]
```

All three target the active profile unless `--profile <name>` names another, and the mod must be **enabled** in that profile (the overrides live on its enabled-mod entry).

### `tweak list`

Prints every tweak the mod declares with its current effective value and where that value came from:

- **`default`** — no override stored; the mod author's declared default is in effect.
- **`profile-override`** — a value you stored with `tweak set`.
- **`collection-default`** — a value seeded by a collection install that you haven't changed since. (Edit it with `tweak set` and it becomes `profile-override`.)

For a `number` / `integer` tweak the declared range is shown; for an `enum`, its allowed values. `--json` writes a `tweakList` report (`schemas/manager/tweak-list.schema.json`).

### `tweak set`

Validates the value against the tweak's declaration before storing it:

- type mismatch (non-numeric `number`/`integer`, a `boolean` that isn't `true`/`false`, an `enum` value that isn't one of the declared options) → `manager.tweakValueInvalid` (error).
- a numeric value outside the declared `min..max` → `manager.tweakValueOutOfRange` (error). (The patcher emits the same code as a plan-time *warning*; here it's an error because you supplied the value and can fix it.)
- an undeclared tweak id → `manager.tweakUnknownId`; a mod not enabled in the profile → `manager.tweakUnknownMod`.

On success the override is written to the profile YAML atomically. A value may start with `-` (a negative number) — it's still parsed as the value, not a flag. `--json` writes a `tweakSet` report.

### `tweak reset`

With a `<tweak-id>`, drops that single override; without one, clears the mod's entire override map (back to all-defaults). Resetting something that has no stored override is a successful no-op (the output says so). `--json` writes a `tweakReset` report whose `tweakId` is `null` for a whole-mod reset.

### Renamed tweaks (aliases)

When a mod author renames a tweak between versions, they list the old id under the tweak's `aliases:` array. The manager then migrates your stored override **forward** the next time it reads the profile: a value keyed by the old id is moved to the current id, the profile YAML is rewritten so it catches up, and `manager.tweakMigratedFromAlias` (info) names the mapping — **no silent data loss on a rename**. If a profile somehow carries both the old and new id (a hand-edit), the current id wins and the stale alias is dropped (`manager.tweakAliasConflict`, warning). An override for an id the mod no longer declares at all is left untouched and surfaced as `manager.tweakOrphanedOverride` (info) so you can decide whether to `tweak reset` it.

### Precedence

When the same tweak is set in more than one place, highest wins: **lockfile pin > CLI `--tweak` (patcher-direct) > profile override > collection-supplied default > mod default**. A profile override surfaces in the plan report as origin `external`.

## Expansions (Ownership)

> For the conceptual model — and the `DLCNeedsOwn` runtime-gating background — see [Package Layering → Present vs Owned vs Effective](../../docs/package-layering.md#present-vs-owned-vs-effective).

Pioneers of Pagonia ships **every** pak (`core`, `decorations1`, `dlc1`, `tools`) to every player; the engine gates DLC content at runtime by ownership. So a pak being on disk doesn't mean its content is active *for you*. The manager tracks three states per game install:

- **Present** — the pak exists on disk (detected; usually true for all four).
- **Owned** — you're entitled to the expansion (**user-declared**, default `unknown` — the tool has no store-account access).
- **Effective** — `Present ∧ Owned`: whether the content is actually active for you in **solo** play.

`core` / `tools` are base game + editor data and are **always owned**; only `decorations1` / `dlc1` are declarable. Ownership is stored **per game install** (keyed by the game-root fingerprint, in `state.yaml` → `installs:`), never on a profile — it's a fact about the installation, stable across every profile.

```powershell
pagonia-manager expansions list [--game <path>] [--assume-owned <pkg>] [--assume-not-owned <pkg>] [--store <p>] [--json <out>]
pagonia-manager expansions set <decorations1|dlc1> <owned|not-owned|unknown> [--game <path>] [--store <p>] [--json <out>]
```

`--game` defaults to the active / stored / platform install (same resolution the Plan + Deploy wizard uses). `expansions list` prints each package's Present / Owned / Effective state (`--json` → `expansionsList` report); `expansions set` writes the per-install record (`--json` → `expansionsSet`).

### The plan / deploy gate

`plan` and `deploy` are ownership-aware. **Presence blocks; ownership only warns** — the load-bearing rule, because Envision ships every pak, so a non-owner must still be able to write a present pak's bytes to match an owning co-op host:

- A required expansion that is **not present** → `manager.modExpansionNotPresent` (**error**, blocks).
- Present but **not owned** → `manager.modExpansionNotOwned` (**warning, never blocks** — not even without `--accept-warnings`). Solo-inactive; the bytes still deploy.
- Present but **unknown** → `manager.expansionOwnershipUnknown` (warning, advisory — prompts you to declare).
- An **optional** package that's absent → `manager.modOptionalExpansionSkipped` (info); present-but-not-effective → `manager.modOptionalExpansionInactive` (info, still deploys).

Where a mod declares `multiplayerSafe`, the not-owned / unknown warning carries a one-line co-op note: in co-op only the host needs to *own* the expansion, but every player needs the same deployed mod set — share it via `profile export` → collection.

### Override flags (`--assume-owned` / `--assume-not-owned`)

`plan`, `deploy`, and `expansions list` accept repeatable `--assume-owned <pkg>` / `--assume-not-owned <pkg>` flags (declarable packages only). They simulate entitlement **transiently** — routed through the resolver, never written to the stored record — so you can preview a core-only player's experience (`plan --assume-not-owned dlc1`) or drive a headless / CI run without touching `state.yaml`.

### Interactive surface

The same model is in the interactive shell: a **Game Expansions** screen (under *Settings*, mirrored under *Advanced → Game Ops*) shows the Present / Owned / Effective table with `Owned` a keyboard-flippable tri-state for `decorations1` / `dlc1` (Present is read-only; `core` / `tools` shown as always-owned). The interactive *Plan + deploy* flow renders the ownership diagnostics inline, at parity with the scripted gate. The first time you deploy against an install that has an expansion present but ownership undeclared, a one-time nudge asks whether you own it (owned / not-owned / ask-me-later); "ask me later" leaves it unknown and is recorded so you aren't re-prompted on every deploy.

## Collections

A collection is a curated bundle of mods shipped as a single YAML manifest. `collection install` runs the manifest through the patcher's resolver, installs every referenced mod into the store, snapshots the manifest, writes a lockfile, and creates a profile pinned to the collection.

### Install Pipeline

```text
collection.yaml + --mods-root
   |
   v
SchemaValidator.ValidateCollection  -> aborts on schema errors
   |
   v
CollectionResolver.Resolve          -> patcher matches collection mods to <mods-root>/<id>/
   |                                   builds a CollectionLock with SHA-256 hashes
   v
Profile preflight                   -> name validity + collision check (before any disk write)
   |
   v
ModInstaller.Install (per mod)      -> reuses the regular install validation + sidecar pipeline
   |                                   already-installed mods are noops with the modAlreadyInstalled warning
   v
Snapshot manifest                   -> <store>/collections/<id>/<version>/<id>.collection.yaml
Snapshot lockfile                   -> <store>/collections/locks/<id>.lock.yaml
Create profile                      -> profile.Collection = <id>, enabledMods + loadOrder from resolved mods
```

The profile-collision check runs **before** any disk write so a name clash aborts cleanly — the store stays exactly as it was. Mod installs happen AFTER the preflight; partial install (mod N+1 fails after mod N succeeded) leaves the installed mods in `<store>/mods/` but skips writing the collection manifest, lockfile, and profile. Re-running the install resumes from where it stopped (already-installed mods become noops).

### Source Resolution

Collection mods are matched by `(id, version)` against the local `--mods-root` folder. When `--from` is a `gh:owner/repo[#ref]/<collection-id>` URI, the manager fetches the collection's `.collection.yaml` + every referenced mod from raw.githubusercontent.com into a temp dir, then runs the same local-resolve pipeline against that dir. Per-mod `source: gh:owner/repo[#ref]` fields inside the collection manifest trigger a **cross-repo fetch** through the OTHER repo's `index.yaml`; the lockfile records each mod's resolved commit SHA so a re-install months later reproduces byte-identical.

Non-GitHub URL `source:` fields (e.g. `https://example.invalid/foo.zip`) still trigger the legacy `manager.collectionRemoteSourceUnsupported` warning and require a matching mod under the collection's repo (or local `--mods-root`).

### Profile Pinning

The created profile carries `collection: <id>` in its file so a future re-resolve can refresh it. The profile name defaults to the collection id; override with `--as-profile <name>` (`--profile <name>` is retained as a legacy alias).

By default, a name collision aborts the install (the existing profile is never silently overwritten). Two flags change that:

- **`--overwrite`** replaces an existing profile of the same name in-place. The pre-existing profile's `enabledMods` are dropped; the new one carries the collection's mods + load order. Manifest + lockfile + mod copies are unaffected.
- **`--activate`** sets `state.yaml.activeProfile` to the new profile after install. The next `plan` / `deploy` runs against it. Combined invocation:

```powershell
pagonia-manager collection install \
    --from gh:pagonia-land/example-mods/pagonia-land.example.beginner-qol \
    --as-profile play-with-streamer \
    --activate
```

That's the "one command to try a publisher's setup" path — link from Discord → playing their exact stack — documented in [Mod Distribution Patterns](../../docs/mod-distribution.md#distribution-channels).

### Lockfile Schema (v0.1)

`<store>/collections/locks/<id>.lock.yaml` carries `collectionLockVersion: "0.1"`. The schema is the public contract under [`schemas/mod-patches/collection-lock.schema.json`](../../schemas/mod-patches/collection-lock.schema.json); the per-mod entries carry two optional fields:

- **`source`** — transport-neutral identifier of where each mod came from at install time, e.g. `gh:owner/repo#<commit-sha>/<mod-id>` (or `gh:owner/repo:base#<commit-sha>/<mod-id>` when the repo's `index.yaml` lives in a `:base` subdirectory). Empty for local-only installs. The SHA is pinned at fetch time so a re-install pulls byte-identical content even after the source branch moves on the remote; the `:base` segment round-trips so the subdirectory is re-resolved on re-install / profile export.
- **`resolvedAt`** — ISO-8601 timestamp when `source` was resolved.

A lockfile that omits these fields still reads cleanly. A lockfile declaring any other version is rejected with a structured `lockfileVersionUnsupported` error rather than silently producing an under-validated install.

### Uninstall Scope

`collection uninstall <id>` removes only the collection-level artifacts:

- `<store>/collections/<id>/` (manifest snapshots)
- `<store>/collections/locks/<id>.lock.yaml`

**Installed mods and the linked profile are kept.** This matches the `uninstall <mod-id>` semantics (which also doesn't cascade to profiles). Remove orphaned mods with `uninstall <mod-id>` and orphaned profiles with `profile delete <name>` as needed.

### Idempotent Reinstall

Re-running `collection install` with the same id+version present in the store is a noop with `manager.collectionAlreadyInstalled` (warning, exit `Success`). To upgrade or re-resolve, `collection uninstall <id>` first.

If the auto-created profile was deleted out-of-band (e.g. `profile delete <name>`) but the manifest + lockfile remain in the store, re-running `collection install` **recreates the profile from the resolved mod set** and surfaces a `manager.collectionAlreadyInstalled` info diagnostic naming the recovery. The mods themselves stay where they are — only the profile slot is rebuilt.

### Invalid `--profile` Override

`--profile <name>` with an invalid name (slashes, colons, etc.) is rejected with `manager.profileNameInvalid`. The error message names the override (e.g. `Profile name override 'foo/bar' is invalid`) so it doesn't blame the collection id when the user supplied the bad name themselves.

## Catalogs

A **catalog** is a curated list of mod-distribution repos (and optionally other catalogs). The manager aggregates across every catalog the user is subscribed to with cycle + depth + dedup protection, so discovery scales beyond one central registry. Anyone can publish their own catalog; communities can specialise (localization, hardcore presets, …); the default subscription is one of many.

```text
pagonia-manager catalog list [--store <p>]
pagonia-manager catalog add <gh:owner/repo[#ref][/path]|https://...|file:path> [--store <p>]
pagonia-manager catalog remove <gh:owner/repo[#ref][/path]|https://...|file:path> [--store <p>]
pagonia-manager catalog browse [--store <p>]
pagonia-manager catalog show <gh:owner/repo[#ref][/path]|https://...|file:path> [--store <p>]
pagonia-manager catalog refresh [<gh:owner/repo[#ref][/path]|https://...|file:path>] [--store <p>]
```

### Source Types

| Form | Status | Notes |
|---|---|---|
| `gh:<owner>/<repo>[#<ref>][/<path>]` | available | GitHub-hosted catalog. `<ref>` defaults to HEAD and is resolved to a concrete commit SHA at fetch time so the aggregator's visited-set sees a stable identity. `<path>` defaults to `catalog.yaml`. |
| `file:<absolute-or-relative-path>` | available | Local-file catalog. Relative paths in subscription specs resolve against the working directory at parse time. Federated `file:./...` references inside a catalog resolve against the parent catalog's directory. Primary use cases: tests, offline workshops, LAN-hosted classroom catalogs, the bundled [`examples/mod-catalog-example/`](../../examples/mod-catalog-example/). |
| `https://example.com/path/catalog.yaml` | available | Raw HTTPS-hosted catalog — for publishers who self-host outside GitHub (GitLab Pages, S3, generic web host, corporate intranet over TLS). No commit-SHA pinning is possible; the URL itself is the canonical identity. URL normalisation lowercases scheme + host and strips a trailing slash from non-root paths so `HTTPS://Example.COM/x` and `https://example.com/x` dedup in the aggregator's visited-set. |
| `http://example.com/...` | available, warning | Plain HTTP. Still fetches but surfaces `manager.catalogInsecureHttp` (warning). Set `state.yaml.allowInsecureCatalogSources: true` to silence the warning for LAN / intranet catalogs you've explicitly vetted. |
| `modio:...` | planned | Lands alongside the broader mod.io adapter. |

Bare local paths without a transport prefix (`./catalog.yaml`, `C:\path\catalog.yaml`) are rejected — every subscription spec must name its transport explicitly so the stored subscription file stays grep-able.

### `catalog browse` — federation semantics

`catalog browse` aggregates every repo across every subscribed catalog plus every catalog they federate to (the `catalogs:` field). Aggregation is BFS through the source graph with:

- **Cycle detection** on canonical source string. `A → B → A` bails on the second visit with `manager.catalogCycleDetected` (info, not error — repos from the first visit are still surfaced).
- **Depth cap** default 5 hops, override via `state.yaml.catalogMaxDepth`. At the cap, the catalog is fetched but its descendant catalogs are not enqueued; emits `manager.catalogDepthCapped` (warning).
- **Repo dedup** on `(owner, repo)`. Multiple catalogs vouching for the same repo collapse to one row; the row's `vouched by N catalogs` count is a soft trust signal.
- **Non-fatal fetch failures**: a per-catalog fetch failure adds a diagnostic but doesn't abort the aggregation. Users see every repo they CAN see, plus a clear note about what failed.

Example output:

```text
Aggregated repos (4 unique across 2 fetched catalog(s)):
  TheLavaBlock/example-hardcore-presets
    (example) Hardcore difficulty presets, one collection per playstyle.
  pagonia-land/example-mods [vouched by 2 catalogs]
    Bundled reference repo — two example mods + one preset collection.
  ...
```

### Interactive drill-in

In the interactive shell, **Browse Community Catalogs** (main menu) renders the aggregated repo table, then lets you select a repo to see what it publishes: its `index.yaml` is fetched (ref pinned to a commit) and its **mods + collections** are listed with id, name, version, and description. From there you pick a mod or collection to **install directly** — a mod runs the same fetch + validate + install pipeline as scripted `install --from gh:…` and then offers to enable it in the active profile; a collection runs `collection install --from gh:…` and asks whether to activate the new profile. No drop to scripted mode, no hand-copied ids. A repo that ships no `index.yaml` is reported as having nothing to browse (you can still install a mod by its repo-relative path from the CLI); a fetch / parse failure surfaces its diagnostics inline.

### Subscription Storage

Subscriptions persist in `state.yaml.subscribedCatalogs[]`. The depth cap override lives in `state.yaml.catalogMaxDepth` (null / 0 / negative → use the default 5). Both fields survive every other state write (DeployService, ProfileLifecycleService.Use, GameRootResolver.SetStoredDefault all explicitly preserve them).

The separate `state.yaml.allowInsecureSources` boolean (default false) opts in to plain-HTTP direct-URL installs — see [Install Sources](#install-sources) for the full story.

### On-Disk Cache

`catalog browse` and `catalog show` go through a `CachingCatalogFetcher` that keeps a per-catalog cache under `<store>/cache/catalogs/<sanitised-canonical>_<8hex>/` (the 8-hex SHA1 suffix disambiguates two sources whose 80-char sanitised prefix collides):

- **`catalog.yaml`** — verbatim fetched bytes (no re-serialisation; deterministic across runs).
- **`cache-meta.yaml`** — small sidecar with `canonical`, `fetchedAt` (ISO-8601 UTC), `commitSha` (gh: only), and `sourceType`.

A cache hit serves from disk + emits `manager.catalogStale` info naming the age + threshold. A miss / stale entry / corrupt meta / `forceRefresh` flag re-fetches via the underlying network fetcher, then atomically rewrites both files (`.tmp` + rename) — surfaced as `manager.catalogCacheWritten`.

`file:` sources bypass the cache entirely; the source file IS the canonical bytes, no caching gain.

`state.yaml.catalogCacheStalenessHours` (default 24, null / 0 / negative → use the default) controls how long a cache entry is considered fresh. Lower it for aggressive freshness at the cost of more network round-trips; raise it for stable subscription sets.

### `catalog refresh` — force re-fetch

```text
pagonia-manager catalog refresh                 # refresh every subscribed catalog
pagonia-manager catalog refresh <gh:.../file:.> # refresh just the named one
```

`catalog refresh` skips the cache-hit check, hits the network for every targeted source, and overwrites the cached YAML + meta on success. Use it when you know upstream changed but the cache hasn't expired yet, or when troubleshooting "the manager seems to see an old view of a catalog I just updated".

### Validating Your Own Catalog

```powershell
pagonia-patcher schema-validate --catalog .\catalog.yaml
```

The schema lives at [`schemas/mod-patches/catalog.schema.json`](../../schemas/mod-patches/catalog.schema.json) — the public contract for any tool that wants to produce, consume, or audit Pagonia Land catalogs.

## Doctor

`pagonia-manager doctor [--store <path>] [--game <path>]` is a read-only health roll-up — one first-stop command that bundles checks the manager already implements, so a confused user doesn't have to know which verb to reach for. It writes nothing; it's the scriptable form of the interactive Status dashboard.

It runs, in order:

| Check | Needs game root | What it reports |
| --- | --- | --- |
| Store | no | initialised? (a hard gate — fails the rest if not) |
| Active profile | no | active profile name + enabled-mod count (warns when empty) |
| Enabled mods installed | no | every enabled mod has an install on disk (errors on a missing one) |
| Cross-mod overlay conflicts | no | reuses the [cross-mod detector](#plan) — two enabled mods destructively targeting the same entity |
| Orphaned deploys | no | deploy records whose game install moved or updated |
| Deploy-backup storage | no | total `<store>/deploys/` size (warns past ~15 GB) |
| Expansion ownership | **yes** | present / owned / effective expansions (skipped when no game root resolves) |

The game root is resolved the usual way (`--game` > stored default > platform default), so on a configured machine `doctor` needs no arguments. Each check prints `[OK]` / `[WARN]` / `[FAIL]` / `[SKIP]` plus any diagnostics, then a summary line. Exit code is non-zero only when a check is an **error** (warnings don't fail it) — so it's safe to drop into CI or a pre-deploy script.

## Plan

`pagonia-manager plan --game <path>` produces a dry-run patch plan for the active profile (or `--profile <name>`) against a real game install. Nothing is written to the game; the report shows what *would* be applied. This is the read-only inspection step before `deploy`.

The plan also runs a **cross-mod overlay-conflict check** across the enabled mods *in load order*: when two enabled mods both `Replace` or `Unload` the same inherited GameDatabase entity, the engine resolves them by load order (last-loaded wins), so the earlier mod is silently overridden. The plan surfaces this as a `manager.crossModOverlayConflict` **warning** naming the winner and the overridden mods — advisory, so it never blocks the plan. This is the cross-mod companion to the patcher's per-mod [authoring advisor](../../docs/mod-conflicts.md#gamedatabase-overlay-conflicts-authoring-advisor): that lints one mod in isolation; this catches the collision only an installed set in load order can have.

### Pipeline

```text
profile state (state.yaml) -> active profile
    |
    v
profile.enabledMods + loadOrder -> ordered (id, version) list
    |
    v
per-mod install dir lookup -> ManifestReader.ReadMod -> LoadedMod[]
    |
    v
cross-mod gameDatabaseVersion check (manager warning if mods disagree)
    |
    v
cross-mod overlay-conflict check (manager.crossModOverlayConflict warning when
    two enabled mods Replace/Unload the same inherited entity — load order wins)
    |
    v
PatchPlanner.Plan(gameRoot, mods)
    |
    v
manager envelope + patcher report
```

The mod load order in the patcher plan matches the profile's `loadOrder` exactly — `move --before/--after/--position` directly drives apply order. Conflicts that the patcher surfaces (two mods writing the same target) come back as `Conflicts` in the patcher report and the CLI exits with `Conflict` (exit code 2).

### JSON Report Shape

`--json <path>` writes a wrapped report:

```json
{
  "manager": {
    "profile": "default",
    "gameRoot": "C:/Games/PoP",
    "success": true,
    "diagnostics": [
      { "severity": "Info", "code": "manager.profileEmpty", "message": "...", "path": null }
    ]
  },
  "patcher": {
    "PlanSource": "managerProfile",
    "Success": true,
    "ModCount": 1,
    "WriteCount": 1,
    "ConflictCount": 0,
    "Mods": [ ... ],
    "Writes": [ ... ],
    "Conflicts": [ ... ],
    "Diagnostics": [ ... ]
  }
}
```

The manager envelope (`manager`) uses camelCase keys; the embedded patcher report (`patcher`) uses the patcher's existing PascalCase shape — passed through verbatim from `PatchPlanReporter.ToJson(plan, planSource: "managerProfile")` so downstream tools can match patcher reports from any source.

`--out <path>` writes a Markdown render of the same data.

### Exit Codes

| Outcome | Exit code |
|---|---|
| Plan clean (or only info/warning diagnostics) | `Success` (0) |
| Patcher conflicts present | `Conflict` (2) |
| Manager-level error (missing game, profile missing, mod install missing) or patcher-level error other than conflicts | `Error` (1) |
| CLI args malformed (no `--game`) | `Usage` (64) |

### What `plan` Does Not Do

- Does not write to `<game-root>/` — that's `deploy`.
- Does not detect the game's actual `gameDatabaseVersion` — the cross-mod version check is internal to the profile. Mismatch between the profile and the game itself is a deploy-time check.
- Does not fetch remote mods — every enabled mod must be installed in the store first (`install` or `collection install`).

## Deploy and Rollback

`pagonia-manager deploy --game <path>` actually writes patched files into the game install. Every deploy backs up the originals first so `rollback` can restore them byte-identically.

### Two Deploy Modes

`<path>` can be either:

| Layout | Detected by | Deploy writes |
| --- | --- | --- |
| **Live install** | `<path>/pak/*.pak` present | Rebuilt paks back into `<path>/pak/` (originals backed up under `<store>/deploys/<fp>/<ts>/backup/pak/<name>.pak`) |
| **Extracted layout** | `<path>/core/gdb/*.gd.xml` populated | Patched loose XMLs into `<path>/<package>/gdb/` (originals backed up under `<store>/deploys/<fp>/<ts>/backup/<rel>`) |

Both modes share the same store, profile, plan, and history. Detection happens automatically at deploy + rollback time via `GameLayoutDetector`; unrecognised paths surface `manager.gameLayoutUnrecognised` instead of the cryptic per-mod `targetFileMissing` chain.

The live-install mode additionally:

- Maintains a per-install extract cache at `<store>/cache/extract-v5-<fp>/` that mirrors the extracted layout — the patcher / planner reads from this cache as if it were the extracted folder. Cache schema is `v5`; older `v1`–`v4` cache directories get pruned automatically on next ensure.
- **Selective extract**: only paks the active profile actually touches are extracted into the cache. A `core/gdb/buildings.gd.xml`-only mod warms `core.pak` only; `decorations1.pak` / `dlc1.pak` / `tools.pak` stay un-extracted until something needs them. Per-pak completion is tracked in `<cache>/.extract-status.yaml`; subsequent ensure calls incrementally add missing paks rather than restarting from scratch.
- **Selective rebuild**: only paks whose contents the active profile actually touches get repacked + written back. Untouched canonical paks stay untouched on disk.
- **Sparse patch apply**: pure Pattern A mods (no `pak:` block, no `entries:` ops) take a fast path that skips `PatchApplier.CopyGameRoot` entirely — patched XML bytes pipe straight from memory into `PakRebuilder`, no temp staging directory created or walked. Mods that need the disk-staging semantics (Pattern B paks / entry-level operations) fall back automatically. Path taken is surfaced as `manager.deployUsedSparsePath` / `manager.deployFellBackToFullApply`.
- Streams pak I/O end-to-end (read original, write new, hash) so multi-GB paks don't hit the .NET 2 GB single-array ceiling.

### Game Fingerprint

Two related-but-distinct fingerprints are in play:

- **`GameFingerprint` (16 hex chars)** — identifies an install for deploy/rollback bookkeeping. Hashes the absolute gameRoot path + `system.json` content if present. Tests work with the path-only fingerprint; real game installs include `system.json` (which Steam updates touch) so a game version bump shifts the fingerprint and the next deploy lands under a fresh `<store>/deploys/<new-fp>/` dir. The previous fingerprint's deploys become **orphans** — surface them via `pagonia-manager deploys list-orphans` (see [Deploys Maintenance](#deploys-maintenance) below).
- **Pak cache fingerprint (`v5-<hash>`)** — used by `PakCacheService` to identify "which install version is this cache for". Hashes the sorted list of `pak/*.pak` filenames (DLC install/uninstall shifts it) + `system.json` content (Steam update shifts it). Deliberately omits per-pak file size and mtime — those drift on every manager-deploy and previously invalidated the cache after every round-trip. Manager-deploy + rollback keep the cache warm; only Steam updates and DLC changes shift the fingerprint and force a fresh extract.
  - **External-change detection (`pakSha256` sidecar, v5).** The cache status sidecar (`.extract-status.yaml`) now records, per extracted pak, the SHA-256 of the source pak captured at extract time. On the next ensure, each warm pak the active profile needs is re-hashed and compared. An exact match is the fast path. A difference is cross-referenced against the `rebuiltPaks[].newSha256` values recorded in this install's deploy manifests: if the live pak's hash is one the manager wrote, it's an expected deploy — silent. If it matches nothing the manager wrote, another tool or a manual repack changed the canonical data — the manager warns `manager.canonicalPakChangedExternally` and re-extracts that pak so the cache is correct (self-healing). This is a per-pak content check layered *on top of* the fingerprint, not folded into it — folding pak content back in would resurrect the post-deploy self-invalidation v4 removed. The schema tag bumped v4 → v5 so older (hash-less) sidecars are transparently re-extracted on first run. **Cost:** this re-hashes each warm pak the profile needs (bounded to that working set, typically just `core.pak`) on every ensure, so a warm-cache plan/deploy is no longer free — but the manifest cross-reference is read lazily, only when a hash actually differs.

### Deploy Pipeline (extracted layout)

```text
1. plan                                           abort on errors / conflicts
2. warnings? --accept-warnings required           else deployBlockedByWarnings
2b. live-state drift preflight (vs previous deploy) else deployBlockedByDrift; --force overrides
3. PatchApplier into a temp staging directory     full patcher behaviour
4. diff staging vs <game-root> -> modified file list
5. backup originals to <store>/deploys/<fp>/<ts>/backup/<rel>
6. atomic per-file copy: staging -> <game-root>/<rel>     XML patches land
7. build + copy Pattern B paks: -> <game-root>/mods/<name>.pak
8. write manifest.yaml (atomic) — rollback needs it to find backups + added paks
9. append entry to history.yaml — THE commit point (status/deploy-status see it)
10. update state.yaml.lastDeploy with timestamp + gameRoot + profile
```

### Deploy Pipeline (live install)

```text
1. detect layout, compute pak-cache fingerprint
2. PakCacheService.Ensure(required-paks) -> <store>/cache/extract-v5-<fp>/
   (selective: extract only paks the active profile touches; incremental
    if some are already in .extract-status.yaml)
3. plan against cache root                        abort on errors / conflicts
4. warnings? --accept-warnings required           else deployBlockedByWarnings
5. preflight: any prior deploy at this gameRoot under a DIFFERENT fingerprint?
   -> warn manager.gameUpdatedSinceLastDeploy (informational, doesn't block)
5b. preflight: hash each live target the PREVIOUS deploy (same fingerprint) wrote
    and compare to its recorded post-deploy SHA-256 -> any mismatch warns
    manager.liveStateDrift and blocks with manager.deployBlockedByDrift unless
    --force / acceptDrift (dry-run surfaces it without blocking)
6. apply patches — two paths:
   - SPARSE (pure Pattern A): PatchApplier.ApplySparse(cacheRoot, plan)
     returns Dictionary<relativePath, byte[]> in memory; no staging tree,
     no CopyGameRoot, no post-apply diff. Surface manager.deployUsedSparsePath.
   - SLOW (Pattern B pak / entries: ops present): PatchApplier.Apply(cacheRoot,
     stagingRoot, plan) mirrors the cache + applies. Surface
     manager.deployFellBackToFullApply with the reason.
7. group modified files by owning canonical pak (via each pak's index)
8. for each affected pak (selective rebuild):
     a. backup original -> <store>/deploys/<fp>/<ts>/backup/pak/<name>.pak (CopyAtomic, streaming)
     b. PakRebuilder.Rebuild pak with replacement bytes -> atomic write into
        <gameRoot>/pak/<name>.pak
9. build + copy Pattern B overlay paks (slow path only): -> <gameRoot>/mods/<name>.pak
10. write manifest.yaml v0.1 (with rebuiltPaks list incl. SHA-256 + byte sizes)
11. append entry to history.yaml — THE commit point
12. update state.yaml.lastDeploy with timestamp + gameRoot + profile
```

The order is important in both modes. Backups land BEFORE game files (XML mode: step 5 before 6; live mode: step 7a before 7b) so `rollback` can always restore byte-identically. History is the **commit point** at step 9/10 — appended only after files are on disk and the manifest is written. A crash anywhere before history-append leaves the deploy as "partially applied, no history record", which `status` and `deploy-status` surface as the previous deploy still being latest — never a silent lie about a phantom successful deploy. Per-file atomicity uses `AtomicFile.WriteAllBytes` / `WriteStreamed` / `CopyAtomic` (write `*.tmp` + `File.Move(..., overwrite: true)`).

`--dry-run` runs through the planning + grouping stages and reports what would change. Nothing is written to the store or the game.

#### Live-state drift detection (`--force`)

A deploy is a destructive write into the player's real install. Before overwriting a pak/file it previously wrote, `deploy` re-hashes that live target and compares it to the post-deploy SHA-256 the **previous** deploy recorded in its manifest (`rebuiltPaks[].newSha256`, `addedFiles[].deployedSha256`, `modifiedFiles[].deployedSha256`). If they differ, something changed it out-of-band — the user by hand, a second mod tool, a partial Steam patch. Each drifted file surfaces `manager.liveStateDrift` (warning) and the deploy refuses with `manager.deployBlockedByDrift` (error) so the foreign change isn't silently clobbered.

- Pass **`--force`** (Core: `acceptDrift`) to overwrite anyway — the drift is still reported, but the deploy proceeds.
- A **dry-run** reports the drift without blocking and without writing, so you can see it before committing.
- `rollback` enforces the mirror-image check: before restoring a backup over a live target, it verifies the live file is still what we deployed. If not, it refuses (so a later hand-edit you might want to keep isn't discarded) unless `--force` is passed. This is independent of the existing backup-side `manager.rollbackHashMismatch` (which verifies the *backup* is intact) — the two guard different ends of the restore.
- The Pattern B existence-refusal (a `<game>/mods/<name>.pak` that already exists blocks the deploy without a backup entry) is a special case of the same "don't clobber unmanaged bytes" principle; its behaviour is unchanged.

What it protects you from: you deploy a mod set, later run another tool (or hand-edit a pak), then re-run the manager. Instead of silently erasing that change, the manager stops and tells you exactly which files changed — overwrite with `--force`, or `rollback` first to inspect.

#### Game-vs-mod version compatibility

When the manager can read the install's real version (the `Pioneers of Pagonia.exe` ProductVersion — a live install with an exe), `plan` and `deploy` compare each enabled mod's declared `gameDatabaseVersion` against it. Both are the same `major.minor.patch-build+revision` shape, so the comparison is exact:

- **Exact match** → silent.
- **Same `major.minor.patch`, different build/revision** (e.g. mod `1.3.0-11727+193140`, game `1.3.0-11768+193445`) → `manager.modGameVersionDrift` (info). The mod targets a different snapshot of the same patch line; almost always still applies.
- **Different `major.minor.patch`** (e.g. mod `1.2.5-…`, game `1.3.0-…`) → `manager.modGameVersionMismatch` (warning). A real gap; the mod may not apply cleanly.

It is **advisory, never a hard block** — even a major-version mismatch is a warning gated by the normal `--accept-warnings` path. Rationale: GameDatabase content is frequently forward/backward compatible across patches, the version string is the author's *tested-against* marker not a strict contract, and the patcher already fails loudly at apply time (`patcher.expectedValueMismatch`) if a patched value genuinely moved. When the install version is unknown (extracted layout, no exe), the check degrades silently to the intra-profile-only `manager.profileGameVersionMismatch` (mods vs each other) — the two axes are orthogonal and fire independently.

### Layout

```text
<store>/deploys/
  <game-fingerprint>/
    history.yaml                         # ordered, newest first
    <timestamp>/
      manifest.yaml                      # mods, files, hashes
      backup/<relative-path>             # originals to restore
```

`history.yaml` is the index. `<timestamp>` uses `yyyyMMddTHHmmssfffZ` for filesystem-safe sortable names.

### Rollback

`pagonia-manager rollback --game <path>` dispatches on whichever list the manifest populated:

1. Compute fingerprint, find `history.yaml`, read latest entry, read its `manifest.yaml`.
1b. **Live-state drift preflight**: hash each live target the manifest recorded writing and compare to its recorded post-deploy SHA-256. Any mismatch means a change landed after our deploy — surface `manager.liveStateDrift` and refuse the whole rollback (nothing restored, history left intact) unless `--force` is passed, so a hand-edit you might want to keep isn't silently discarded. Independent of the backup-side check in step 2.
2. **Live-install path** (when `manifest.rebuiltPaks` is non-empty): for each pak entry, hash the backup with streaming SHA-256, compare against `originalSha256` recorded at deploy time. Mismatch surfaces `manager.rollbackHashMismatch` and refuses the restore (overwriting the live install with corrupt-bytes backup would defeat the point of rollback). On match, `CopyAtomic` the backup back into `<gameRoot>/pak/<name>.pak`. The extract cache under `<store>/cache/` is **kept** — fingerprint hasn't changed, the cache stays valid for the next plan.
3. **Extracted-layout path** (when `manifest.modifiedFiles` is non-empty): restore each `relativePath` from `backup/<rel>` atomically per file.
4. Pattern B overlay paks under `<gameRoot>/mods/` are deleted in both paths.
5. Pop the entry from history, delete the rolled-back deploy's `<timestamp>/` directory (manifest + backup).

A second rollback rolls back the next entry. No prior deploy → `manager.rollbackNothingToRollback` (info, exit `Success`).

### Pattern B Overlay Paks

Mods that declare a `pak:` block in their `mod.yaml` ship a Pattern B overlay pak alongside (or instead of) XML patches. The deploy pipeline handles them automatically:

1. `PatchApplier` writes the loose-file scaffold (`<staging>/<pak.name>/{manifest.json, files.json, <pak.name>.gd.bin, memory.bin}` plus any `*.gd.xml` from `entries.add`).
2. The manager's `PakBuilder` wraps the paker's `PakPacker`: walks the scaffold dir, synthesises a transient `pakinfo.json` listing every file (in-pak path = `<pak.name>/<rel>`, gzip-compressed), and calls `PakPacker.Pack(pakinfo, outputPakPath)`.
3. The resulting `.pak` is staged in the temp directory, then atomically copied to `<game>/mods/<pak.name>.pak`.

Deploy refuses to overwrite an existing same-named pak in `<game>/mods/` (`manager.deployBlockedByErrors` with the hint to run `rollback` first or move the file aside) — silent overwrite without a backup entry would risk data loss.

Rollback **deletes** Pattern B paks (no backup needed — they didn't exist before deploy). Missing-at-rollback is a `manager.rollbackAddedFileMissing` info diagnostic (the user may have cleaned up out-of-band).

### Deploys Maintenance

Two subcommands under the `deploys` group manage the store's deploy history beyond the single-step rollback flow.

#### `deploys list-orphans`

```powershell
pagonia-manager deploys list-orphans [--store <path>]
```

Enumerates every `<store>/deploys/<fingerprint>/` directory whose recorded `gameRoot` either no longer exists on disk or has drifted to a different fingerprint (typically a Steam game update touched `system.json`). Per-row output: fingerprint, recorded gameRoot path, total deploys recorded under that fingerprint, latest timestamp + profile + mod/file counts, and the reason it's stale (`GameRootGone` / `GameUpdated`).

Orphaned deploys still take disk space (their backup paks are full-size) but can no longer roll back cleanly — the install they applied to is either gone or has been updated past the version the backup was for. The interactive status dashboard also surfaces an **Orphaned deploys** warning panel when at least one exists.

Listing is read-only; use `deploys clean` to actually remove orphans.

#### `deploys clean`

```powershell
pagonia-manager deploys clean --keep <N> [--game <path>] [--dry-run] [--store <path>]
```

Per-fingerprint, keeps the N most recent timestamp directories and removes older ones (manifests + backups). `--keep 0` prunes all but the **newest** deploy per fingerprint — the newest is always retained as that install's rollback anchor (`rollback` reverts it), so a clean can never leave an install with no undo path. The lastDeploy guard below applies on top of that.

Flags:

- `--keep <N>` — required. Number of most-recent timestamps to keep per fingerprint.
- `--game <path>` — scope to one install's fingerprint. Without it, every fingerprint directory under the store is processed, each with its own keep-N rule applied independently.
- `--dry-run` — report what would be removed without writing. Counts are still produced; `history.yaml` and on-disk directories are not touched.

**Last-deploy guard.** The timestamp `state.yaml.lastDeploy` currently references is **never** removed, even when `--keep 0` is in effect. Removing it would orphan what `status` calls the user's "latest deploy" with no rollback path. The protection surfaces as `manager.deployCleanRefusedLatest` and the entry stays in `history.yaml` even when other keep-window-protected entries surround it.

After clean, `history.yaml` is rewritten with the trimmed deploy list. One-step rollback always operates on `Deploys[0]` (newest-first), so trimming the tail doesn't affect current-and-recent rollback semantics; it only takes away the option to roll back further than N steps.

The interactive status dashboard surfaces a **Deploy backups storage** warning panel when `<store>/deploys/` exceeds ~15 GB, with a hint to run this command (`manager.deploysStorageHigh`). Below the threshold the panel stays hidden.

Default behaviour up to this release was "keep all backups forever"; this command is the user-opt-in to reclaim space without the manager auto-deleting anything behind the user's back.

### Exit Codes

| Outcome | Exit code |
|---|---|
| Deploy completed | `Success` (0) |
| Dry run completed | `Success` (0) |
| Rollback reverted | `Success` (0) |
| Rollback noop (no history) | `Success` (0) |
| Deploy blocked (errors / conflicts / unaccepted warnings) | `Error` (1) |
| `--game` missing | `Usage` (64) |

## JSON Reports + Schemas

Every command listed above accepts `--json <out>` and writes a stable, schema-validated JSON report. Reports are the integration surface for downstream tools (a future GUI, CI scripts, mod-installer hooks) — match against the `code` field for diagnostics and the `reportKind` field for the report shape, not human-readable text.

### Stable Contract

- `schemaVersion`: `"0.1"` on every report. Each schema versions independently; a future bump signals a shape change to that one report.
- `reportKind`: discriminator, one of `install`, `uninstall`, `deploy`, `rollback`, `collectionInstall`, `status`, `deployStatus`, `tweakList`, `tweakSet`, `tweakReset`, `expansionsList`, `expansionsSet`.
- `gameProductVersion` (on `deployStatus`): the live install's game version, read from the executable's Win32 **ProductVersion** — byte-for-byte the same string mods declare as `gameDatabaseVersion` (e.g. `1.3.2-11873+194094`). `null` when no readable exe (extracted layout / fixture); the human-readable output renders that as `(unknown)`. The same version drives the game-vs-mod compatibility check below.
- `diagnostics`: always an array of `{ severity, code, message, path? }`.
- The plan report (`plan --json`) uses its own `{ manager, patcher }` envelope shape — its embedded patcher payload uses the patcher's PascalCase property names.

The canonical schemas live under [`schemas/manager/`](../../schemas/manager/) and ship embedded inside the AOT-published `pagonia-manager.exe`, so `schema-validate` works without needing the repo's `schemas/` folder on disk.

### `schema-validate`

```powershell
pagonia-manager schema-validate --kind install --report .\install.json
pagonia-manager schema-validate --kind deploy  --report .\deploy.json
```

Exits `Success` (0) on conformance, `Error` (1) on any schema violation. The tool is the reference implementation of the public contract — adding a new diagnostic code or report field that escapes the schema is caught here.

### Example: Install Report

```json
{
  "schemaVersion": "0.1",
  "reportKind": "install",
  "outcome": "Installed",
  "modId": "pagonia-land.smoke.s9",
  "version": "0.1.0",
  "manifestName": "Smoke S9",
  "installPath": "C:\\...\\store\\mods\\pagonia-land.smoke.s9\\0.1.0",
  "diagnostics": [
    { "severity": "Info", "code": "fileRead", "message": "...", "path": "..." },
    { "severity": "Info", "code": "modManifestValid", "message": "...", "path": null }
  ]
}
```

`fileRead`, `modManifestValid` etc. are patcher codes passing through verbatim — the convention is documented in the Diagnostic Codes section below.

A profile holds two lists:

- `enabledMods` — `(id, version)` pairs describing which version of each enabled mod the manager will use
- `loadOrder` — list of mod ids in the order they should be applied

Both lists are mutated together under normal use: `enable` adds to both, `disable` removes from both, `move` only re-orders `loadOrder`. If a profile drifts (an entry in `loadOrder` without a matching `enabledMods` row), `disable` actively cleans the orphan — see the two-case `disable` semantics below.

`enable` without `--version` resolves the **most recently installed** version: each installed version's `.manager-install.yaml` sidecar carries an `installedAt` timestamp; the latest wins. If the sidecar is missing or malformed, the directory's `CreationTimeUtc` is the fallback. Enabling a mod that's already enabled at the same version is a `manager.modAlreadyEnabled` warning (noop). Enabling at a different version replaces the version reference without duplicating the load-order entry.

`disable` has two cases:

- Mod is absent from BOTH `enabledMods` and `loadOrder` — `manager.modNotEnabled` warning (noop, exit `Success`).
- Mod is present in `loadOrder` only (drift) — orphan is stripped from `loadOrder`, `manager.profileDriftCleaned` info diagnostic surfaced, exit `Success`. Catches the case where an interactive surface (or external edit) left a load-order entry without a matching enabled-mod row.

`move` accepts exactly one of `--position` (1-based, must be in `[1, count]`), `--before <other-id>`, or `--after <other-id>`. The anchor (`--before`/`--after` target) must be in the load order. A no-op move (e.g. position equals current) is silently accepted.

## Exit Codes

| Code | Constant | Meaning |
| ---: | --- | --- |
| 0 | `Success` | Command completed without error. Also the result for duplicate installs (warning, not error). |
| 1 | `Error` | Generic failure (validation, IO, unexpected exception). Also the result for `uninstall` against a missing mod / version. |
| 2 | `Conflict` | A blocking conflict was detected (mod-vs-mod, profile-vs-game, etc.). |
| 64 | `Usage` | The CLI was invoked with an unknown command or a missing/malformed required argument. |

Exit codes are stable across releases — downstream automation may rely on them.

## Diagnostic Codes

The manager's own diagnostic codes follow the `manager.<area><Detail>` pattern. This is a deliberate departure from the patcher/paker convention (which uses bare codes like `modManifestReadFailed` or `pakFooterTruncated`): the manager's reports **surface downstream codes verbatim**, so namespacing the manager's own codes keeps the layers distinguishable. A downstream consumer matching on the `Code` field can tell at a glance whether an entry came from the manager itself (`manager.*`) or was passed through from the patcher / paker (bare).

| Code | Severity | Meaning |
| --- | --- | --- |
| `manager.storeNotInitialised` | error | A store operation found no `state.yaml` at the resolved root. Run `pagonia-manager store init` first. |
| `manager.storeStateUnreadable` | error | `state.yaml` exists but is empty or not valid YAML. |
| `manager.storeSchemaVersionUnsupported` | error | Reserved — emitted when the store's `storeVersion` is newer or older than the running manager supports. Not currently raised; will land alongside a forward-/backward-compat check. |
| `manager.modSourceNotFound` | error | The `--from` path does not exist (or was empty). |
| `manager.modSourceNotAFolderOrZip` | error | The `--from` path exists but is neither a directory nor a readable `.zip` archive. |
| `manager.modManifestMissing` | error | The source root contains no `mod.yaml`. |
| `manager.modAlreadyInstalled` | **warning** | A mod with the same `id@version` is already in the store; the install is a noop and the existing files are preserved. Exit code stays at `Success`. |
| `manager.modNotInstalled` | error | `uninstall` was called against a mod id that the store doesn't have. |
| `manager.modVersionAmbiguous` | error | `uninstall` without `--version` against a mod that has multiple installed versions. Pass `--version` to choose. |
| `manager.modVersionNotInstalled` | error | `uninstall --version <v>` against a mod where that specific version is not installed, or `enable --version <v>` against the same. |
| `manager.profileMissing` | error | The active profile name in `state.yaml` has no matching `<name>.profile.yaml` on disk. |
| `manager.modAlreadyEnabled` | **warning** | `enable` against a mod already enabled at the same version. The command is a noop and exit code stays at `Success`. |
| `manager.modNotEnabled` | **warning** | `disable` against a mod that isn't in the active profile's enabled list (and isn't in its load order either). Noop, exit `Success`. |
| `manager.profileDriftCleaned` | info | `disable` against an id present in the active profile's `loadOrder` but not in `enabledMods` (drift). The orphan is stripped from load order; mutated=true, exit `Success`. |
| `manager.moveTargetNotInLoadOrder` | error | The mod-id passed to `move` is not in the active profile's load order. |
| `manager.moveAnchorNotInLoadOrder` | error | The `--before`/`--after` anchor id is not in the load order. |
| `manager.movePositionOutOfRange` | error | The `--position` value is outside `[1, loadOrderCount]`. |
| `manager.profileAlreadyExists` | error | `profile create` (or `profile copy` onto an existing target) against a name that already has a profile file. |
| `manager.profileCopied` | info | `profile copy` duplicated a profile under a new name. |
| `manager.profileExported` | info | `profile export` wrote a profile out as a collection manifest. |
| `manager.profileExportLocalSource` | **warning** | A mod in an exported profile had no recoverable remote source; written as `source: "local"`. It won't fetch on another machine without a matching `--mods-root`. |
| `manager.profileExportEmpty` | error | `profile export` against a profile with no enabled mods. A collection requires at least one mod. |
| `manager.profileNameInvalid` | error | A profile name failed the name rules (see Profile Name Rules above). |
| `manager.profileActiveDeletion` | error | `profile delete` against the active profile. Switch to a different profile first. |
| `manager.profileDefaultDeletion` | error | `profile delete` against `default`. The default profile is never deletable; it's the safety net that `store init` re-creates if needed. |
| `manager.collectionAlreadyInstalled` | **warning** | `collection install` against a collection (id + version) already present in the store. Noop, exit `Success`. |
| `manager.collectionNotInstalled` | error | `collection uninstall` or `collection show` against a collection not in the store. |
| `manager.collectionRemoteSourceUnsupported` | **warning** | A `mods[].source` URL in the collection manifest isn't resolved in this install mode; the matching local mod from `--mods-root` is used instead. (Cross-repo `gh:` sources are resolved when the collection itself is installed with `collection install --from gh:...`.) |
| `manager.collectionInstallAborted` | error | A per-mod install failed mid-collection; the collection-level manifest + lockfile + profile are not written. Already-installed mods remain in the store and resume on re-run. |
| `manager.profileEmpty` | info | `plan` ran against a profile with zero enabled mods. The patcher plan is empty; not an error — useful for "what would deploy look like right now?" sanity checks. |
| `manager.profileGameVersionMismatch` | warning | Two or more mods in the same profile declare different `gameDatabaseVersion` values. Apply will still run, but the mods may have been authored against different game versions. (Intra-profile axis: mods vs each other.) |
| `manager.modGameVersionDrift` | info | A mod's declared `gameDatabaseVersion` shares the same `major.minor.patch` line as the install's actual version (read from the exe) but a different build/revision — e.g. mod `1.3.0-11727+193140` vs game `1.3.0-11768+193445`. Almost always still applies; never blocks. (Game-vs-mod axis.) |
| `manager.modGameVersionMismatch` | warning | A mod's declared `gameDatabaseVersion` is on a different `major.minor.patch` line from the install's actual version — e.g. mod `1.2.5-…` vs game `1.3.0-…`. A real version gap; the mod may not apply cleanly. Advisory only — gated by the normal `--accept-warnings` path, never a hard block. Fires only when the install version is readable (live install with an exe); degrades silently otherwise. (Game-vs-mod axis.) |
| `manager.modInstallMissing` | error | A mod is enabled in the profile but its install directory is missing from `<store>/mods/<id>/<version>/`. Usually the mod was uninstalled or moved out-of-band. Re-install or `disable` it. |
| `manager.gameRootMissing` | error | The `--game <path>` passed to `plan` / `deploy` / `rollback` is empty or does not exist. |
| `manager.deployBlockedByErrors` | error | Plan errors or patcher conflicts prevented the deploy. Run `plan` to see the details, then fix or override. |
| `manager.deployBlockedByWarnings` | error | Plan has warnings and `--accept-warnings` was not passed. Pass it to override. |
| `manager.liveStateDrift` | warning | A live file the last deploy wrote was changed out-of-band (a hand-edit, a second mod tool, a partial Steam patch) since then — its on-disk SHA-256 no longer matches the post-deploy hash recorded in the manifest. On `deploy` it precedes a `deployBlockedByDrift` block; on `rollback` it gates the restore. Overridable with `--force`. |
| `manager.deployBlockedByDrift` | error | Deploy refused: one or more live targets changed out-of-band since the last deploy (each named by a preceding `liveStateDrift` warning). Re-run with `--force` to overwrite the foreign change(s), or `rollback` first to inspect. A dry-run surfaces the drift without blocking. |
| `manager.rollbackBlockedByDrift` | error | Rollback refused: one or more live files changed out-of-band since the deploy being reverted (each named by a preceding `liveStateDrift` warning). Re-run with `--force` to restore over the foreign change(s); nothing was changed. |
| `manager.deployEmpty` | info | Profile has no writes against the game — deploy is a noop, no manifest written. Exit `Success`. |
| `manager.deployCompleted` | info | Deploy wrote N files; backup + manifest + history updated. |
| `manager.deployDryRun` | info | `--dry-run` mode: report what would change. Nothing written. |
| `manager.rollbackNothingToRollback` | info | No prior deploy recorded for this game's fingerprint. Exit `Success`. |
| `manager.rollbackCompleted` | info | Rollback restored N files from the latest deploy. |
| `manager.rollbackBackupMissing` | error | A backup file referenced by the manifest is missing; rollback can't restore. |
| `manager.deployHistoryUnreadable` | error | `history.yaml` exists but is empty / invalid YAML (surfaced via `DeployHistoryStore.TryRead` from `status` / `deploy-status` / `rollback` / `deploy` — never an unhandled stack trace), or a referenced manifest is missing during rollback. |
| `manager.schemaValidationOk` | info | `schema-validate` confirmed the report conforms to its schema. |
| `manager.schemaValidationFailed` | error | `schema-validate` found a schema violation, or could not load / parse the report. The diagnostic message carries the JSON pointer (`/path/to/field`) and the failed keyword (`required`, `enum`, etc.). |
| `manager.pakBuildSucceeded` | info | Pattern B overlay pak built from a scaffold directory during deploy. |
| `manager.pakBuildFailed` | error | `PakBuilder` invocation failed (paker reported errors). The deploy aborts before any pak is written. |
| `manager.pakScaffoldMissing` | error | Mod declared `pak.name` but `PatchApplier` produced no scaffold directory at `<staging>/<pak.name>/`. Usually means a `pak.name` typo or a patcher apply that aborted earlier. |
| `manager.rollbackAddedFileMissing` | info | Rollback's expected `addedFiles` entry (e.g. a Pattern B pak in `<game>/mods/`) was already gone. Not an error — the user may have removed it out-of-band. |
| `manager.gameLayoutUnrecognised` | error | The `--game <path>` argument exists on disk but doesn't look like a Pioneers of Pagonia folder (neither a live install with `pak/*.pak` nor an extracted layout with `core/gdb/*.gd.xml`). |
| `manager.pakCacheRefreshed` | info | `PakCacheService.Ensure` extracted N paks into the cache (cold miss, or partial-hit's "add missing" branch). |
| `manager.pakCacheReused` | info | Cache was warm for every requested pak — no extraction needed this call. |
| `manager.pakCachePartialHit` | info | Subset of requested paks was already on disk; the others got extracted on this call. |
| `manager.pakCacheSelective` | info | Cache only extracted `requested` of `discovered` paks because the active profile's mods only touch a subset (selective extract — saves time on first plan against installs where DLC paks aren't touched). |
| `manager.pakCacheExtractFailed` | error | One pak failed to extract mid-batch. Paks that succeeded earlier in the same call stay in the cache + are recorded in `.extract-status.yaml`; the next ensure resumes from where it left off rather than re-extracting from scratch. |
| `manager.canonicalPakChangedExternally` | warning | At extract time, a canonical source pak (`<game>/pak/*.pak`) was found changed since it was cached, and the new hash matches no `rebuiltPaks[].newSha256` the manager recorded — so another tool or a manual repack edited it, not a manager deploy. The pak is re-extracted (self-healing) so the cache is correct; a far clearer signal than the downstream `patcher.expectedValueMismatch` it would otherwise surface as. Advisory, never blocks. |
| `manager.pakRebuilt` | info | `PakRebuilder` rewrote one canonical pak with patched XML payload. Message names the pak + entry counts + before/after byte sizes. |
| `manager.pakRebuildFailed` | error | Pak rebuild ran into an I/O or format error. Backup is intact at the recorded path; deploy aborts before the live pak is overwritten. |
| `manager.modifiedFileMissingOwningPak` | error | A patched file's relative path doesn't appear in any of the discovered paks' indexes. Usually means the cache is stale — re-run plan after refreshing the cache. |
| `manager.pakRollbackRestored` | info | Rollback restored one pak from backup. Emitted once per rebuilt-pak entry in the manifest. |
| `manager.rollbackHashMismatch` | error | A backup pak's SHA-256 differs from the value recorded at deploy time. Rollback refuses to overwrite the live pak — restoring potentially-corrupt bytes would defeat the entire point of rollback. |
| `manager.deployUsedSparsePath` | info | Live-install deploy took the sparse fast-path: patched XML bytes piped from memory into `PakRebuilder`, no temp staging tree created. |
| `manager.deployFellBackToFullApply` | info | Live-install deploy fell back to the disk-staging Apply path. Message names the reason (Pattern B pak block detected / entry-level operations present). |
| `manager.gameUpdatedSinceLastDeploy` | warning | Deploy preflight detected a prior deploy for the same `gameRoot` path but a different fingerprint — likely a Pioneers of Pagonia update touched `system.json` between the two. When both the prior deploy manifest and the current install expose a ProductVersion, the message names them (*"updated from v1.4.2 to v1.5.0"*); otherwise it falls back to the fingerprint-hash wording. Older backups under that older fingerprint won't restore cleanly over the current install; run `pagonia-manager deploys list-orphans` to inspect. |
| `manager.orphanedDeploysPresent` | warning | The status dashboard observed at least one orphaned deploy directory under `<store>/deploys/`. Reserved for the JSON status report; today the dashboard surfaces orphans via a side panel rather than this code. |
| `manager.orphanedDeployCleaned` | info | Reserved for a future `deploys clean --orphans` option that explicitly targets orphan dirs. Today `deploys clean` operates per-fingerprint via `--keep N`; orphans are removed by the same per-fingerprint rule when their fingerprint dir is processed. |
| `manager.deployCleanRemoved` | info | `deploys clean` removed (or would have removed on `--dry-run`) one timestamp directory. Message names the fingerprint + timestamp. |
| `manager.deployCleanKept` | info | `deploys clean` kept one timestamp within the `--keep N` window. |
| `manager.deployCleanRefusedLatest` | warning | `deploys clean` refused to remove a timestamp because `state.yaml.lastDeploy` currently references it. Removing it would orphan the user's "current deploy" with no rollback path. |
| `manager.deploysStorageHigh` | info | Status dashboard surfaced the deploy-backups-storage panel because `<store>/deploys/` exceeded the soft ~15 GB threshold. Hint pointed the user at `deploys clean`. |
| `manager.defaultGameRootStored` | info | Interactive wizard persisted a new value to `state.yaml.defaultGameRoot`. Subsequent wizard launches will suggest this path by default. |
| `manager.defaultGameRootCleared` | info | The "Change default game folder" wizard cleared `state.yaml.defaultGameRoot`. Next wizard launch falls back to the platform default (Windows Steam path if it exists) or the bare text prompt. |
| `manager.remoteResolvedToCommit` | info | Remote install resolved the user-supplied ref (branch/tag/SHA) to a concrete commit SHA. The SHA — not the ref — is what the sidecar's `source` field records, so the install trail stays stable when the branch moves later. |
| `manager.remoteFetchFailed` | error | An HTTP request the remote-fetch flow needed failed (unknown ref, missing `mod.yaml`, missing patch file, traversal path, or a transport error). Message names the URL and the failure mode; nothing is written to the store. |
| `manager.remoteIndexMalformed` | error | The remote repo's `index.yaml` parsed as invalid YAML or as a structurally-broken `RepoIndex`. Install aborts; the repo author needs to fix it. Validate locally with `pagonia-patcher schema-validate --repo-index <yaml>` before publishing. |
| `manager.modNotInRepoIndex` | error | The `<mod-id-or-path>` segment of the source spec was not found in the remote repo's `index.yaml` (neither as a mod id nor as a path). Message lists every id the index actually offers so the user can fix the typo. |
| `manager.crossRepoSourceResolved` | info | A `collection install --from gh:...` followed a per-mod `source: gh:other/other-repo[#ref]` field across repository boundaries; the other repo's ref was resolved to a concrete commit SHA before fetching that mod's files. Surfaces once per cross-repo hop. |
| `manager.profileCreatedFromCollection` | info | A profile was created (or replaced under `--overwrite`) from a collection's resolved mod list + load order. Names the profile + the collection + mod count. |
| `manager.profileActivatedFromCollection` | info | After `collection install --activate`, the new profile was set as `state.yaml.activeProfile`; the next `plan` / `deploy` targets it. |
| `manager.profileAlreadyExists` | error | A `collection install` would have overwritten an existing profile with the same name. Message points the user at `--overwrite` (to replace it) or `--as-profile <name>` (to pick a different name). |
| `manager.catalogFetchFailed` | error | A catalog could not be fetched / parsed at the source location. Non-fatal for `catalog browse` aggregation — the user still sees repos from other subscribed catalogs that resolved fine. |
| `manager.catalogMalformed` | error | The fetched catalog YAML did not match the `catalog.yaml` shape (wrong type for a field, missing required keys). |
| `manager.catalogSubscribed` | info | `catalog add` persisted a new subscription, or surfaced a duplicate-add no-op. |
| `manager.catalogUnsubscribed` | info | `catalog remove` cleared a subscription, or surfaced a no-op when nothing matched. |
| `manager.catalogCycleDetected` | info | The federation walk hit a catalog it had already visited (A → B → A). Repos from the first visit are still surfaced; the second occurrence is skipped. |
| `manager.catalogDepthCapped` | warning | The federation depth cap (`state.yaml.catalogMaxDepth`, default 5) was reached. The catalog at the cap was fetched + its repos collected; its further descendants were not enqueued. |
| `manager.catalogStale` | info | The on-disk catalog cache served the response. Message names the age + the freshness threshold. Run `catalog refresh` to force a re-fetch. |
| `manager.catalogCacheWritten` | info | A fresh fetch landed and the cache was updated atomically. Names the cache directory. |
| `manager.catalogCacheCorrupt` | warning | The cache meta sidecar or the cached YAML was unreadable; the fetcher fell through to a fresh network fetch + cache rewrite. Non-blocking. |
| `manager.catalogInsecureHttp` | warning | A subscribed catalog uses plain `http://` (no TLS). The fetch still proceeds; flip `state.yaml.allowInsecureCatalogSources: true` to silence the warning for LAN / intranet hosts you've vetted. |
| `manager.catalogRepoIndexPathConflict` | warning | Two aggregated catalogs vouch for the same `owner/repo` but disagree on its `indexPath` (the subdirectory holding the repo's `index.yaml`). The first catalog visited wins; the message names both paths. Resolve by aligning the catalogs, or trust the first. |
| `manager.defaultCatalogSeeded` | info | `store init` (CLI or interactive first-run) seeded a brand-new store with the official catalog as its default subscription. Opt out with `catalog remove`; it's never re-added to an existing store. |
| `manager.directUrlFetched` | info | A direct-URL ZIP install succeeded. Message names the URL, the archive size, and the SHA-256 prefix that was hashed during streaming. |
| `manager.directUrlFetchFailed` | error | The direct-URL fetch failed (404, network error, extraction failure). Message names the URL + the failure mode. |
| `manager.directUrlInsecureHttp` | warning | Install source uses plain `http://` instead of `https://`. The warning fires regardless of `state.yaml.allowInsecureSources`; the install only PROCEEDS when that flag is true. Without the opt-in, the install aborts with the same warning + a refusal message. |
| `manager.directUrlTraversalRefused` | error | A ZIP entry's path resolved to a location outside the extraction root (zip-slip / `..` traversal). Refused before any file was written. |
| `manager.directUrlArchiveDrift` | info | Re-installing from a URL that previously installed different bytes. The previous SHA-prefix + current SHA-prefix are named in the message. Non-blocking — the install proceeds with the new bytes. |
| `manager.modIoApiError` | error | mod.io API call failed: missing API key, network error, 404 on the game/mod ids, malformed JSON response. Message names the failure mode + the relevant URL or ids. |
| `manager.modIoRateLimited` | warning | mod.io returned 429 (rate-limited). Message points the user at `PAGONIA_MODIO_API_KEY` for an isolated rate-limit bucket. |
| `manager.modIoMapTypeSkipped` | info | The fetched mod is tagged as `Map`. Maps are handled by the game's in-game UGC subscription list, not by `pagonia-manager install`. No files were downloaded; nothing was written to the store. |
| `manager.modIoCollectionsUnsupported` | info | `collection install --from modio:...` was attempted. mod.io's collection model is server-curated and not portable as `*.collection.yaml`; the message points users at `collection install --from gh:...` for portable collections. |
| `manager.modIoUnknownGameAlias` | error | The `<game>` segment of `modio:<game>/<mod-id>` is neither a numeric id nor one of the recognised slugs (`pioneers-of-pagonia`, `pop`). Message names what IS accepted so the user can fix a typo. |
| `manager.modIoVersionPinNotImplemented` | info | The install was pinned to `modio:<game>/<mod-id>#<version>` but mod.io's current modfile has a different version. Pinning a mod.io install to a specific `#<version>` isn't supported; the install proceeds with mod.io's current modfile bytes. |
| `manager.tweakUnknownMod` | error | A tweak read/set/reset targeted a mod that is not enabled in the resolved profile (overrides live on a profile's enabled-mod entry). |
| `manager.tweakUnknownId` | error | The mod is enabled but declares no tweak with the given id (`tweak set`/`reset`). Run `tweak list <mod-id>` to see the ids it exposes. |
| `manager.tweakValueOutOfRange` | error | A `tweak set` value for a `number`/`integer` tweak fell outside the declared `min..max`. (The patcher uses the same code as a plan-time *warning*; the manager raises it at error severity because the user supplied the value and can fix it.) |
| `manager.tweakValueInvalid` | error | A `tweak set` value did not match the tweak's declared type — non-numeric for `number`/`integer`, not `true`/`false` for `boolean`, or not one of the declared options for `enum`. |
| `manager.tweakOverridesResetByReinstall` | info | A `collection install --overwrite` reseeded the profile's tweak map from the collection, discarding tweak overrides the user had stored since the last install. Without `--overwrite`, an already-installed collection is a no-op and the user's overrides survive. |
| `manager.tweakMigratedFromAlias` | info | The author renamed a tweak and listed the old id under `aliases:`; a stored override keyed by the old id was migrated forward to the current id, and the profile YAML rewritten so it catches up. Fires once per migrated key — no silent data loss on a rename. |
| `manager.tweakAliasConflict` | warning | A profile stored overrides under *both* a tweak's current id and one of its legacy aliases (a hand-edit / race). The current id wins; the stale alias entry is dropped. |
| `manager.tweakOrphanedOverride` | info | A profile carries an override for an id the mod no longer declares (and not as an alias) — the tweak was removed or the alias dropped. The value is kept untouched (nothing silently lost) and surfaced so the user can clean it up. |
| `manager.modExpansionNotPresent` | error | A mod needs an expansion (its `requiredPackages`, or a non-optional `patchSets[].requiresPackages`) whose pak is **not present on disk** — there is nothing to patch. Presence is the hard constraint. Envision ships every pak to every player, so a missing one is unusual; check the install. |
| `manager.modExpansionNotOwned` | warning (non-blocking) | A required expansion is present but **not owned** on this install. Solo, the content stays inactive (the engine gates it at runtime); the bytes still deploy — needed for co-op parity with an owning host. Ownership never blocks deployment, so this warning does **not** require `--accept-warnings`. |
| `manager.expansionOwnershipUnknown` | warning (non-blocking) | A required expansion is present but you haven't declared whether you own it (default `unknown`). Solo, it only takes effect if you own it. Declare with `expansions set <pkg> <owned\|not-owned>`. Advisory, never blocks. |
| `manager.modOptionalExpansionSkipped` | info | A mod's **optional** content targets an expansion that is **absent** on disk, so that content is skipped — reported with its real reason (absent, not "not owned"). |
| `manager.modOptionalExpansionInactive` | info | A mod's optional content targets an expansion that is **present but not effective** for you (not owned / unknown). It still deploys for co-op parity but stays inactive in solo play. |
| `manager.expansionOwnershipSet` | info | `expansions set` wrote a per-install ownership record (keyed by the game-root fingerprint). |
| `manager.expansionPackageNotDeclarable` | error | `expansions set` was given `core` / `tools` (always owned — base game + editor data) or an unknown package. Only `decorations1` / `dlc1` are declarable. |

## Publishing A Standalone Binary

The manager publishes as a Native AOT single-file executable so end users don't need a .NET runtime — drop the binary somewhere on `PATH` and run.

```powershell
# Windows x64
dotnet publish .\src\PagoniaLand.Manager.Cli -c Release -r win-x64

# Linux x64
dotnet publish ./src/PagoniaLand.Manager.Cli -c Release -r linux-x64

# macOS (Intel / Apple Silicon)
dotnet publish ./src/PagoniaLand.Manager.Cli -c Release -r osx-x64
dotnet publish ./src/PagoniaLand.Manager.Cli -c Release -r osx-arm64
```

Output lands at `src/PagoniaLand.Manager.Cli/bin/Release/net8.0/<rid>/publish/`. On Windows that's `pagonia-manager.exe`; the binary bundles YamlDotNet + JsonSchema.Net + the linked Patcher.Core + Paker.Core into a single Native AOT image.

### Verify The Build

Smoke-test the published binary against a temporary store + game fixture before shipping:

```powershell
$EXE = ".\src\PagoniaLand.Manager.Cli\bin\Release\net8.0\win-x64\publish\pagonia-manager.exe"
$STORE = "$env:TEMP\pagonia-mgr-aot-smoke"

# Should print version + linked Patcher.Core / Paker.Core versions
& $EXE --info

# Init a throwaway store
& $EXE store init --store $STORE

# Full lifecycle round trip against a fixture mod + fixture game tree,
# with JSON reports schema-validated by the binary itself
& $EXE install --from .\fixtures\my-mod --store $STORE --json $env:TEMP\install.json
& $EXE schema-validate --kind install --report $env:TEMP\install.json
& $EXE enable my.mod.id --store $STORE
& $EXE plan --game .\fixtures\game-gdb --store $STORE
& $EXE deploy --game .\fixtures\game-gdb --store $STORE --json $env:TEMP\deploy.json
& $EXE schema-validate --kind deploy --report $env:TEMP\deploy.json
& $EXE rollback --game .\fixtures\game-gdb --store $STORE
```

If every command exits `Success` and `schema-validate` returns no errors, the binary is shippable.

### Build-Time Warnings

The publish surfaces summary warnings `IL2104` / `IL3053` from `PagoniaLand.Manager.Core`, `PagoniaLand.Patcher.Core`, and `YamlDotNet`. These come from YamlDotNet's reflection-based deserialization. They're mitigated by:

- `ILLink.Descriptors.xml` (project-level type roots)
- `[DynamicDependency]` attributes on every reader-class constructor that calls `Deserialize<T>` (ProfileStore, StoreStateReader, ModLister, DeployHistoryStore, RollbackService)

The warnings are summary indicators — the underlying types are preserved correctly, verified by the full-lifecycle smoke test passing against the AOT binary.

Patcher and paker codes (e.g. `modManifestReadFailed`, `missingId`, `schemaValidationFailed`) appear in manager reports without re-namespacing whenever the manager invokes those libraries. Their meaning is documented in the [patcher CLI](../pagonia-patcher/CLI.md) and [paker CLI](../pagonia-paker/CLI.md) pages.
