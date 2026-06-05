---
name: Game database update
about: Track docs and tooling updates after a new game version
title: "[Game Update]: "
labels: game-update
assignees: ""
---

## Game Version

Version or build:

## Packages Updated

Which `.pak` packages changed? (The names below are the packages that ship today;
a future update may add or rename packs — use **other** for anything not listed.)

- [ ] `core.pak`
- [ ] `decorations1.pak`
- [ ] `dlc1.pak`
- [ ] `tools.pak`
- [ ] other:

## Local Refresh Checklist

- [ ] Extract XML files into `game-gdb/`
- [ ] Run `scripts\validate_database.ps1`
- [ ] Run `scripts\analyze_database.ps1`
- [ ] Run `scripts\generate_catalog.ps1`
- [ ] Compare against `VALIDATION_BASELINE.md`
- [ ] Update counts in `README.md`
- [ ] Update `VALIDATION_BASELINE.md`
- [ ] Update affected docs

## Notable Changes

List notable new, removed, or changed systems.

## Validation Changes

Paste changed validation counts or warning samples.

Do not attach extracted game XML files, `.pak` files, assets, or generated data dumps.
