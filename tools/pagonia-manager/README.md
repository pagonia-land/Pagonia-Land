# Pagonia Land Manager

This folder hosts the **Pagonia Land Manager**.

The manager is a small C#/.NET command line tool that owns the stateful side of the modding workflow: install / enable / profile / plan / deploy / rollback against a real Pioneers of Pagonia game install. It wraps the [Pagonia Land Patcher](../pagonia-patcher/) and [Pagonia Land Paker](../pagonia-paker/) core libraries — the engines that do the actual XML patching and `.pak` writing — and adds the layer they deliberately don't: mod store, profiles, deploy backup/rollback.

## Intended Role

```text
mod archives + collections   -->   install / enable / profile
profile + game install       -->   plan -> deploy -> rollback
```

A future GUI (`Pagonia Land Manager.exe`, WINDOWS subsystem) will sit on top of the same `PagoniaLand.Manager.Core` library. CLI and GUI ship as two binaries from one solution because Windows fixes a binary's subsystem at compile time — a single-exe-for-both setup forces awkward output races and broken piping. The split mirrors the established model (`git` / `git-gui`, `code` / `Code.exe`, `docker` CLI / Docker Desktop).

## Interactive Mode (default)

Run `pagonia-manager` with **no arguments** in a terminal and it launches an interactive shell — coloured menus, prompts, progress spinners, no need to remember command names or flag spellings. Built on [Spectre.Console](https://spectreconsole.net/), AOT-compiled so it ships in the same single-file binary.

**First run:** if the resolved store folder hasn't been initialised yet, the shell says so up front — naming the location and where it was resolved from (`--store` flag / `PAGONIA_MANAGER_STORE` env / platform default) — and offers to initialise it on the spot, so a first-time user isn't left bouncing off "store not initialised" messages. Decline and you can still initialise later via *Advanced → Store → init*.

The main menu is grouped into three task-oriented clusters plus two utility entries at the bottom. Discovery is by **what you want to do**, not by which CLI verb to type:

### Mods

| Pick | What it does |
|---|---|
| **Install a mod** | Asks for a folder, `.zip`, GitHub repo, mod.io coordinate, or direct-download URL; validates through the patcher pipeline; **advises on overlay conflict risks** (for GameDatabase-overlay mods); installs; offers to enable it in the active profile in one step. *Advanced → Mods → advise* re-runs the advisor on any installed mod, with optional base-aware checks against a game root |
| **Browse community catalogs** | Federated aggregate view across every subscribed catalog (`vouched by N catalogs` trust signal). Select a repo to list the mods + collections it publishes, then install a mod (with the enable-in-profile prompt) or install-and-activate a collection — straight from the shell. Walks the user through adding a subscription if none exist yet |
| **Manage active profile** | Sub-menu: enable / disable a mod, reorder load order, **configure this mod (tweaks)**, **copy this profile**, **export this profile as a collection**, show profile details, create a new profile (multi-mod wizard), switch active profile |
| **Configure this mod (tweaks)** | Under *Manage active profile* (shown only when an enabled mod exposes tweaks): pick a mod, then edit each adjustable value with a type-matched prompt — yes/no for a boolean, a list for an enum, a validated number entry for a slider — or reset one / all to defaults |
| **Copy / export this profile** | Under *Manage active profile*: **copy** duplicates the profile under a new name (snapshot before a risky change, or branch to experiment, optionally activating the copy); **export** writes it out as a shareable `*.collection.yaml` (shown only when the profile has at least one enabled mod — a collection needs ≥1 mod). The same two verbs live under *Advanced → Profiles* for any profile, not just the active one |

### Game

| Pick | What it does |
|---|---|
| **Plan + deploy to game** | Suggests the persisted default game path (or the Windows Steam default if no default is set yet), shows the detected game version, runs `plan`, renders the result as a tree, prompts for dry-run / deploy / abort, handles conflicts. Prompts to overwrite if a live file changed out-of-band since the last deploy. Live game-install path (folder with `pak/*.pak`) auto-extracts canonical paks into a cache and repacks the affected ones; extracted-layout path (`core/gdb/*.gd.xml`) writes loose XMLs |
| **Roll back last deploy** | Shows what would be reverted, default-No confirm, then restores byte-identically (whole pak files for live-install deploys, loose XMLs for extracted-layout). Verifies backup SHA-256 before overwriting, and prompts to force if a live file drifted since the deploy |
| **View deploy history** | Latest deploy summary + full per-game-install history table (timestamp, profile, mods, files) |

### Status & Settings

| Pick | What it does |
|---|---|
| **Status dashboard** | Read-only overview: store + active profile + installed mods + last deploy. Surfaces warning panels when at least one orphaned deploy exists (game install moved or updated) or when `<store>/deploys/` exceeds ~15 GB |
| **Settings** | Sub-menu: default game folder (inspect / set / clear the persisted `state.yaml.defaultGameRoot`) + catalog subscriptions (add / remove / refresh all) + **Game expansions (DLC ownership)** — a Present/Owned/Effective screen with a tri-state Owned toggle for `decorations1`/`dlc1` (also mirrored under *Advanced → Game Ops → expansions*) |
| **Plan + deploy ownership nudge** | On the first deploy against an install with a DLC pak present but ownership undeclared, a one-time prompt asks whether you own it (owned / not-owned / ask-me-later). "Ask me later" leaves it unknown and is recorded so you aren't re-asked every deploy. The plan/deploy flow renders the ownership diagnostics inline, at parity with the scripted gate |
| **Clean up old deploy backups** | Lists orphaned deploys + trims per-fingerprint deploy directories to the N most recent. Dry-run first by default, with confirm-and-apply prompt after. The current `state.yaml.lastDeploy` entry is always protected from removal |

### Utility

| Pick | What it does |
|---|---|
| **Advanced** | Full CLI surface organised by category — every command from the [CLI contract](CLI.md), prompt-driven. Use this when you know what you want to do and the main-menu wizards don't cover it |
| **Quit** | Bye. |

The ordering is intentional: pick-by-task first (three clusters at the top), fall back to **Advanced** for the raw verb catalog. New modders should never need to leave the top three clusters for the common path.

The shell is the default when invoked with no args **and** stdin is a TTY. With stdin redirected (CI pipes, `</dev/null`, etc.) the binary falls back to the usage screen so scripted invocation never hangs on a prompt.

**Getting around.** Arrow keys move, Enter selects, and every selection menu carries a **Back** entry. In a free-text prompt — a mod source, a game-install path, a profile or collection name — submit an **empty line** to cancel and return to the previous menu, so opening a wizard by mistake never traps you in an input you have to type your way out of. Ctrl+C still quits the shell outright.

Pass any CLI arg (e.g. `--version`, `--help`, `store info`, anything) to skip the shell and use scripted mode instead — exactly as before.

## Documentation

- **Start here:** [`sandbox/examples/manager-walkthrough/`](../../sandbox/examples/manager-walkthrough/) — a self-contained walkthrough that drives every command in sequence against a fixture game tree. Run `pwsh sandbox/examples/manager-walkthrough/run.ps1` from the repo root.
- [`CLI.md`](CLI.md) is the user-facing contract: every command, every flag, every exit code, every diagnostic code.
- [`CHANGELOG.md`](CHANGELOG.md) tracks user-visible behaviour changes per release. The `[0.1.0]` entry covers live game-install deploy plus the follow-up optimisations (selective extract, sparse apply, game-update awareness, `deploys clean`, persistent default game folder, fingerprint stability across deploy ↔ rollback), the remote-source work (single-mod + collection install from GitHub repos, federated catalogs, on-disk catalog cache, mod.io adapter, direct-URL ZIP source, HTTP catalog sources), the interactive-main-menu restructure into task clusters with an Advanced submenu, and the install-state integrity + version-awareness work (game-version surfacing from the exe, live-state drift detection with `--force`, external-change detection for canonical paks, and a game-vs-mod `gameDatabaseVersion` compatibility check).

## How Changes Are Validated

If you modify anything under this folder, run [`scripts/preflight.ps1`](../../scripts/preflight.ps1) from the repository root before committing. The preflight builds every tool solution and runs every test suite; [`.github/workflows/tools.yml`](../../.github/workflows/tools.yml) runs the same steps on every push and PR.

## What It Does

The manager covers the full stateful mod lifecycle against a real game install:

- **Store** — `store init` / `store info`. On-disk layout under a per-user root (`%LOCALAPPDATA%\PagoniaLand\Manager` by default, overridable via `--store` or `PAGONIA_MANAGER_STORE`) for installed mods, profiles, collection lockfiles, and deploy state. Atomic writes throughout. A fresh `store init` seeds the official catalog as the default subscription (opt-out via `catalog remove`), so `catalog browse` works on day one.
- **Install lifecycle** — `install --from <folder|zip|gh:owner/repo[:base][#ref]/mod-id|https://.../mod.zip|modio:<game>/<mod-id>[#<ver>]>` validates the mod through the patcher's manifest + schema check, runs the conflict-minimising authoring advisor over any GameDatabase overlay it ships (advisory, never blocking), then extracts it into the store. The `gh:` form fetches a single mod from a GitHub repo (raw-content URLs, ref pinned to commit SHA for sidecar provenance); an optional `:base` segment points at a subdirectory holding the repo's `index.yaml`, so one repo can host a mod tree in a subfolder (e.g. `gh:pagonia-land/Pagonia-Land:official-mods/<mod-id>`); the `https://...zip` form streams any HTTP ZIP into a temp file (hashed on the fly, zip-slip-guarded extract, sidecar records `url:<url>#<sha256>` so drift on re-install is detectable); the `modio:` form resolves a pre-signed download URL via the mod.io API and delegates to the same direct-URL pipeline, with a Map-type check that defers to the game's in-game UGC list for PoP map mods. `uninstall <id>`, `list` round out the surface.
- **Active-profile mod selection** — `enable` / `disable` / `move --before/--after/--position` / `status`. The active profile's `loadOrder` directly drives apply order at deploy time.
- **Health roll-up** — `doctor [--game <path>]` is the scriptable one-stop check: store initialised, active profile, every enabled mod installed, cross-mod overlay conflicts, orphaned deploys, deploy-backup storage, and (with a game root) expansion ownership. Each is `[OK]`/`[WARN]`/`[FAIL]`/`[SKIP]` with a summary; exits non-zero only on an error, so it drops cleanly into CI or a pre-deploy script.
- **Named profiles** — `profile create / list / use / copy / export / delete / show`. Switching profiles restores each one's enabled mods + load order untouched; `profile copy` duplicates one in place for a quick snapshot or experiment branch; `profile export` writes a profile out as a shareable `*.collection.yaml` (tweak overrides folded into `mods[].tweaks`, sources recovered from install provenance) so "share my exact setup" reuses the collection format.
- **Collection install** — `collection install --from <file|gh:owner/repo[:base][#ref]/id>` resolves a collection manifest through the patcher's `CollectionResolver`, installs every mod, writes a lockfile, creates a pinned profile. The `gh:` form fetches the collection + every referenced mod (including cross-repo refs via per-mod `source: gh:`) from raw.githubusercontent.com; the lockfile records each mod's commit SHA so a re-install reproduces byte-identical. `--as-profile <name> --activate` turns the whole install into "saw a link in Discord → playing the publisher's exact stack" in one command. `collection list / show / uninstall` for the rest.
- **Catalog discovery** — `catalog browse` aggregates every mod-distribution repo across the catalogs the user is subscribed to (via `catalog add`), with federation (a catalog can reference other catalogs), cycle + depth + dedup protection, and a "vouched by N catalogs" trust signal. The catalog format is transport-agnostic — `gh:` for GitHub-hosted catalogs and `file:` for offline / classroom catalogs are first-class today; mod.io and raw-HTTP are reserved adapter slots. Designed to scale beyond one central registry: anyone can publish their own catalog and communities can specialise.
- **Plan + deploy + rollback** — `plan` produces a dry-run patch plan; `deploy` writes patched files into the game; `rollback` restores byte-identically. Both modes share one entry point with auto-detection (`GameLayoutDetector`):
   - **Extracted layout** (`<game>/core/gdb/*.gd.xml`, same shape as the repo's local `game-gdb/`): per-file atomic writes back into the game tree, originals backed up under `<store>/deploys/<fp>/<ts>/backup/<rel>`.
   - **Live install** (`<game>/pak/*.pak`, the Steam install): canonical paks get selectively extracted into a fingerprinted cache, patched XMLs pipe straight into a streaming `PakRebuilder`, rebuilt paks atomically replace the live ones with whole-pak backups under `<store>/deploys/<fp>/<ts>/backup/pak/`. Pure Pattern A mods take a sparse fast-path (no temp staging tree); Pattern B / entry-ops fall back to the disk-staging Apply. The detected install also surfaces its **game version** — read from `Pioneers of Pagonia.exe`'s ProductVersion (the same string mods declare as `gameDatabaseVersion`) — in the detect screen, `deploy-status` (its JSON gains a nullable `gameProductVersion`), and the deploy manifest.
   - Pattern B overlay paks (mods with a `pak:` block) are built via the paker's `PakPacker` and copied to `<game>/mods/<pak.name>.pak`; `rollback` deletes those paks too.
   - When the install's real version is readable (live exe), plan/deploy compare each enabled mod's `gameDatabaseVersion` against it — `manager.modGameVersionDrift` (info) for a same-line build difference, `manager.modGameVersionMismatch` (warning) for a real version gap. Advisory only, gated by `--accept-warnings`; degrades silently when the version is unknown.
- **Deploys maintenance** — `deploys list-orphans` enumerates deploy dirs whose game install moved or has been updated (Steam patch); `deploys clean --keep N` trims older timestamp dirs per fingerprint while refusing to remove the entry `state.yaml.lastDeploy` currently references.
- **Expansion ownership** — `expansions list` / `expansions set <decorations1|dlc1> <owned|not-owned|unknown>` track which DLC expansions a given install owns (user-declared per install, since Envision ships every pak to everyone). `plan` / `deploy` are ownership-aware: a mod targeting an expansion that isn't **present** errors, but present-but-**not-owned** only *warns* and still deploys — solo-inactive, yet the bytes a co-op participant needs to match an owning host. `--assume-owned` / `--assume-not-owned` simulate entitlement transiently.
- **Stable JSON reports + `schema-validate`** — every state-changing command accepts `--json <out>` and writes a schema-validated payload. The canonical schemas live under [`schemas/manager/`](../../schemas/manager/) and ship embedded in the AOT binary so `schema-validate` works without the repo on disk.

307 offline tests pass; Native AOT publishes a single-file `pagonia-manager` binary, smoke-verified end-to-end via the [sandbox walkthrough](../../sandbox/examples/manager-walkthrough/) (XML-mode + live-install-mode round-trips in 19 stages).

## Download

Grab a Native AOT single-file binary from the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest):

- Windows x64: [pagonia-manager-win-x64.zip](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-win-x64.zip)
- Linux x64: [pagonia-manager-linux-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-linux-x64.tar.gz)
- macOS Intel: [pagonia-manager-osx-x64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-x64.tar.gz)
- macOS Apple Silicon: [pagonia-manager-osx-arm64.tar.gz](https://github.com/pagonia-land/Pagonia-Land/releases/latest/download/pagonia-manager-osx-arm64.tar.gz)

No .NET runtime needed at the destination — drop the binary somewhere on `PATH` and run.

Want to build it yourself? Recipe below.

## Build

```powershell
dotnet build .\PagoniaLand.Manager.slnx
```

## Run The CLI

```powershell
dotnet run --project .\src\PagoniaLand.Manager.Cli -- --version
dotnet run --project .\src\PagoniaLand.Manager.Cli -- --info

dotnet run --project .\src\PagoniaLand.Manager.Cli -- store init --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- store info --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- install --from ..\pagonia-patcher\fixtures\mods\cheaper-sawmill --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- list --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- enable pagonia-land.fixture.cheaper-sawmill --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- status --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- disable pagonia-land.fixture.cheaper-sawmill --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- profile create dlc1-testing --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- profile use dlc1-testing --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- profile list --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- profile show dlc1-testing --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- collection install --from ..\..\docs\examples\collections\beginner-qol.collection.yaml --mods-root ..\pagonia-patcher\fixtures\mods --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- collection list --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- collection show pagonia-land.collections.beginner-qol --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- collection uninstall pagonia-land.collections.beginner-qol --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- plan --game ..\pagonia-patcher\fixtures\game-gdb-mini --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- plan --game ..\pagonia-patcher\fixtures\game-gdb-mini --store .\out\store --json .\out\plan.json --out .\out\plan.md

dotnet run --project .\src\PagoniaLand.Manager.Cli -- deploy --game .\out\sandbox-game-gdb --store .\out\store --dry-run
dotnet run --project .\src\PagoniaLand.Manager.Cli -- deploy --game .\out\sandbox-game-gdb --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- deploy-status --game .\out\sandbox-game-gdb --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- deploy-list --game .\out\sandbox-game-gdb --store .\out\store
dotnet run --project .\src\PagoniaLand.Manager.Cli -- rollback --game .\out\sandbox-game-gdb --store .\out\store

dotnet run --project .\src\PagoniaLand.Manager.Cli -- install --from .\my-mod --store .\out\store --json .\out\install.json
dotnet run --project .\src\PagoniaLand.Manager.Cli -- schema-validate --kind install --report .\out\install.json

# Native AOT single-file publish (no .NET runtime required at the destination)
dotnet publish .\src\PagoniaLand.Manager.Cli -c Release -r win-x64
# Output: .\src\PagoniaLand.Manager.Cli\bin\Release\net8.0\win-x64\publish\pagonia-manager.exe

dotnet run --project .\src\PagoniaLand.Manager.Cli -- uninstall pagonia-land.fixture.cheaper-sawmill --store .\out\store
```

## Run The Tests

```powershell
dotnet run --project .\tests\PagoniaLand.Manager.Tests
```

## C# Structure

```text
tools/pagonia-manager/
  PagoniaLand.Manager.slnx
  README.md
  CLI.md
  src/
    PagoniaLand.Manager.Cli/      # CONSOLE-subsystem CLI binary -> pagonia-manager.exe
    PagoniaLand.Manager.Core/     # shared core library (refs Patcher.Core + Paker.Core)
  tests/
    PagoniaLand.Manager.Tests/
```

## Design Rules

- the manager orchestrates, the engines do the work — every install/validate/plan/deploy operation goes through `PagoniaLand.Patcher.Core` or `PagoniaLand.Paker.Core`, not a duplicated implementation here
- `PagoniaLand.Manager.Core` stays AOT-friendly (no reflection-heavy patterns) so the CLI binary publishes as a single-file Native AOT executable
- every state-changing command emits stable JSON via `--json <path>`; downstream tools (mod installers, a future GUI) consume schemas under `schemas/manager/` and `manager.*` diagnostic codes from [`CLI.md`](CLI.md)
- the GUI binary is a separate project against the same core library, never a second mode of the CLI binary
