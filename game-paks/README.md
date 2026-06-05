# Local Pak Archives

This folder is the local input area for the **original Pioneers of Pagonia `.pak` archives** that ship with the game. It mirrors what `game-gdb/` is for extracted XML files: a place to drop proprietary game content on your own machine so the tools can work against it, never committed to Git.

## What Belongs Here

Drop the unmodified `.pak` files from the game install, for example:

```text
game-paks/
├── core.pak
├── decorations1.pak
├── dlc1.pak
├── tools.pak
├── Pioneers of Pagonia.exe   (optional — lets the tools read the game version)
└── patch_notes.txt           (optional — copied into the version snapshot)
```

The exact set of `.pak` files depends on the installed game version and DLCs.

Two optional companions are welcome here:

- **`Pioneers of Pagonia.exe`** — its `ProductVersion` (e.g. `1.3.1-11826+193733`) is the game's real version string, the same one mods declare as `gameDatabaseVersion`. Dropping it here lets the refresh pipeline read the version automatically instead of you typing it.
- **`patch_notes.txt`** — the official notes for the version you just installed. The update orchestrator copies it into `snapshots/<version>/` so the changelog writer can cross-reference it later.

## What Is This Used For

- [`pagonia-paker`](../tools/pagonia-paker/CLI.md) reads, lists, unpacks, packs, and patches `.pak` archives. It points at this folder for real-world testing.
- [`scripts/extract-xmls-from-paks.ps1`](../scripts/extract-xmls-from-paks.ps1) unpacks the `*.gd.xml` files from `game-paks/<package>.pak` into the matching subfolder of [`game-gdb/`](../game-gdb/README.md) (pass `-Clean` when refreshing for a new game version).
- Future regression tests for big-archive scenarios can opt-in to use a local copy of `core.pak` if it is present, without ever committing it.

## What Must Not Happen

- Do not commit `.pak` files. They are proprietary content owned by Envision Entertainment.
- Do not redistribute the contents of this folder.
- Do not commit the `.exe` either — it is the proprietary game binary, owned by Envision Entertainment.
- `.gitignore` excludes everything under `game-paks/` except this README; if you ever see a `.pak` or `.exe` file staged by Git, stop and unstage it.

## Where The Files Come From

Pioneers of Pagonia ships its `.pak` archives in a **`pak`** subfolder of the installation folder — on a default Steam install that is `…\steamapps\common\Pioneers of Pagonia\pak\`. The exact base path depends on the launcher and where you installed the game.

You can copy them here either by hand, or — since the install folder is known — with the staging script:

```powershell
pwsh ./scripts/prepare-update.ps1                       # default Steam install
pwsh ./scripts/prepare-update.ps1 -GameRoot '<path>'    # install lives elsewhere
pwsh ./scripts/prepare-update.ps1 -DryRun               # preview, copy nothing
```

It reads the version from the exe first, skips the copy if you already have a snapshot for that version, cleans stale paks out of this folder (keeping `README.md` and a hand-edited `patch_notes.txt`), then copies the paks + exe in. It only ever **reads** from the game install. After it runs, update `patch_notes.txt` and continue with [`update-from-paks.ps1`](../UPDATE_PLAYBOOK.md#one-command-refresh).
