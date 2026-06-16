# Local User-Map Paks

This folder is the local input area for **user-map `.pak` archives** — the kind produced by the Pagonia Editor and shared on mod.io. It mirrors what [`game-paks/`](../game-paks/README.md) is for the shipped game paks: a place to drop content on your own machine so the tools can inspect it, never committed to Git.

## What Belongs Here

User maps exported from the Pioneers of Pagonia editor, or downloaded from mod.io. For example:

```text
game-maps/
├── my-first-map.pak
├── valley-of-pagonia.pak
└── treasure-island.pak
```

Each pak typically contains a `manifest.json`, one `*.popmap` file with the actual map data, and preview images — see the **Pattern C** section of [`docs/mod-distribution.md`](../docs/mod-distribution.md#pattern-c--user-maps-editor-output).

## What Is This Used For

- Empirical inspection: list and unpack with `pagonia-paker` to confirm the user-map pak shape across different authors and versions.
- A reference set when documenting Pattern C, the editor output, and the `.popmap` format.
- Future tests that verify our tools handle user-map paks correctly (list, unpack, classify) without ever shipping the maps themselves.

## What Must Not Happen

- Do not commit `.pak` files from this folder. Even author-shared maps may contain assets, names, or thumbnails that are not yours to redistribute through this repository.
- `.gitignore` excludes everything under `game-maps/` except this README; if you ever see a `.pak` file staged by Git, stop and unstage it.

## Where The Files Come From

A map pak comes from one of two places — and they are **different folders**:

- **You authored it.** The editor's *Publish package* wizard writes the built `.pak`
  (alongside a `.zip`) into the package's `dist` folder under the authoring area:

  ```text
  %LOCALAPPDATA%\Pioneers of Pagonia\My Mods\<package-name>\dist\
  ```

- **You subscribed to or installed it.** Maps you subscribe to through the game's
  built-in mod.io browser, or drop in by hand, live in the per-user **UGC** folder:

  ```text
  %LOCALAPPDATA%\Pioneers of Pagonia\UGC\
  ```

  (i.e. `C:\Users\<you>\AppData\Local\Pioneers of Pagonia\UGC` on Windows.) Maps are
  the only UGC type on mod.io for the game today, and that subscription list is
  managed **in-game**, not by `pagonia-manager`.

Copy whichever pak you want to inspect into this folder yourself; the tools never
download, subscribe, or import anything on their own.

> See the community wiki, [How to create a map package](https://pioneersofpagonia.wiki.gg/wiki/How_to_create_a_map_package), for the full publish workflow.
