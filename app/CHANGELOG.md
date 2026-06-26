# Pagonia Land App Changelog

User-visible changes to the **Pagonia Land** desktop app — the native viewer that generates an interactive GameDatabase catalog from your own Pioneers of Pagonia install. Follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and [Semantic Versioning](https://semver.org/).

> The bundled CLI tools have their own [changelog](../tools/pagonia-manager/CHANGELOG.md), as does the browser-based [catalog browser](../tools/catalog-browser/CHANGELOG.md). The repo-root [`CHANGELOG.md`](../CHANGELOG.md) is a separate log: it tracks the **GameDatabase** data per game version, not this app.

## [0.4.0] - 2026-06-26

### Added

- Initial release of the **Pagonia Land** app — point it at your Pioneers of Pagonia install (or a folder of `.pak` files, or an extracted `game-gdb`) and it generates a browsable GameDatabase catalog on the fly, with real icons decoded from the game's paks.
- **Five domains** — Resources, Buildings, Recipes, Units and Objectives — each a searchable list with a detail pane showing its full fields, plus an overview landing page with counts and how the domains connect.
- **Cross-navigation** — every reference is a clickable link that jumps to the related entity, in **both directions**: a building's costs → the resource, and a resource → who produces / gathers / consumes it and what it builds or recruits; recipes show which buildings run them; objectives link their buildings / units / resources / prerequisite objectives.
- **Search** — a global "find anything" box across every domain (icon + name + domain), plus a live filter for the open tab. Keyboard shortcuts (`Ctrl+F` search, `Esc` clear, `Enter` generates).
- **Navigation** — browser-style Back / Forward across jumps (buttons and the mouse back/forward buttons); double-click any row to copy its GUID.
- **Reads your whole install** — all paks (core + any DLC / decorations / tools), shown in a Sources panel, with the detected game version.
- **Warm restart + auto-update** — the generated catalog is cached and reloads instantly on launch when the game is unchanged; when the game has updated since the last catalog the app regenerates automatically and says so. A **Cache folder** button opens the local cache.
- **Search-index export** — each generation also writes a browser-compatible `search-index.json` (every entity + the five domains) into the cache next to the snapshot, kept current on game updates. (A *partial* index — the full online index is the larger PowerShell-pipeline composite.)
- Remembers its window size / position and which monitor it was maximized on; centres on first launch.
- Polish: keyboard shortcuts (`Ctrl+F` / `Esc` / `Enter`), empty-state hints, a filter match count, accent styling, and Generate enabled only for a recognised install.
- **Settings / Catalog views** — the install / Generate / cache controls live on a **Settings** page reached by the ⚙ button (top-right); the catalog itself fills the window. Settings opens first when there's nothing cached; an auto-load opens straight to the catalog. Click the brand (top-left) to return to the catalog. Detail-pane text is selectable + copyable.
- **Texture → PNG export** — a *Dump → PNG* button in Settings (next to *Cache folder*, with a hover help badge that explains it) walks a folder and writes a lossless, full-quality PNG next to every game texture (`.image` / `.texture`), same name; point it at an extracted game-gdb or pak folder to pull icons and textures out of the game.
- **Direct download links in Settings** — a row that links straight to the latest release's CLI tools (manager / patcher / paker) built for your platform, plus this app, the schemas bundle, and `SHA256SUMS`, so you can grab a fresh build in the browser without leaving for the website.
