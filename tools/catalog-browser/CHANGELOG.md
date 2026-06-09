# Pagonia Land Catalog Browser Changelog

User-visible changes to the **catalog browser** — the in-browser search UI for generated catalog data, deployed to the [catalog page](https://pagonia-land.github.io/Pagonia-Land/catalog/) on GitHub Pages.

It ships continuously with the docs site rather than in the tagged tool releases (manager / patcher / paker), so entries here are **dated**, not versioned. Loosely follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

> The bundled tool binaries have their own [changelog](../pagonia-manager/CHANGELOG.md). The repo-root [`CHANGELOG.md`](../../CHANGELOG.md) tracks GameDatabase data per game version.

## 2026-06-07

### Changed

- Dark mode now uses the **same blue-grey palette as the docs site** — the homepage hero / CTA-button family (`#10151d` / `#1f2937` / `#2d3748`) instead of the previous neutral greys, so the catalog reads as one site with the docs. Accent stays teal (matching the docs' `accent: teal`); the brand bar already shared the hero gradient.

## 2026-06-05

### Changed

- Removed the redundant **Load default index** button and the confusing relative-path hint from the loader. The catalog still loads automatically when the page opens; the file picker remains so you can load your own `search-index.json`, including on the hosted catalog page.
