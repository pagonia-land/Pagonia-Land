# ✨ Mod Tweaks

A **tweak** is a value a mod author marks as *user-adjustable* — a building-cost multiplier, an on/off flag, a difficulty preset — so a player picks the value in the mod manager **without editing the mod files**. One mod, many tastes.

This page is the map of the whole feature. It spans three roles and three tools, so the details live on the page that owns each layer; this guide tells the end-to-end story and links you to the right place.

> **Already know the feature?** Jump to the layer you need: [declare a tweak](#author-declare-a-tweak), [ship presets in a collection](#curator-ship-presets), [configure it as a player](#player-configure-a-tweak), or the [precedence ladder](#which-value-wins-precedence).

## Why tweaks exist

Without tweaks, a player who wants a mod "but a bit cheaper" has to fork it — copy the mod, edit the XML, and re-do that on every update. Tweaks remove the fork:

- ship **one** mod and let every player tune it to taste;
- ship **one** collection that pulls in the same mods with different values, so a "Hardcore" and an "Easy" preset are two manifests, not two forks;
- keep the player's choices intact across mod updates, even when the author renames a parameter.

## The three roles

Tweaks pass through three hands. Each is documented in full on its own page; the links below are the authoritative reference.

### Author: declare a tweak

The mod author lists the adjustable parameters in a `tweaks:` block in `mod.yaml`, then references them in patch operations with a `{{ tweaks.<id> }}` placeholder that the patcher substitutes at plan time. Each tweak has a type (`number` / `integer` / `boolean` / `enum`), a default, a label, and optional bounds.

```yaml
# in mod.yaml
tweaks:
  - id: softwood-cost
    type: integer
    label: Softwood trunk cost
    default: 3
    min: 1
    max: 8
```
```yaml
# in a patch operation
value: "{{ tweaks.softwood-cost }}"
```

**Full reference:** [Declarative Patch Format → Tweaks (`tweaks:` block)](mod-patch-format.md#tweaks-tweaks-block), including the field rules, the `{{ tweaks.x ? 'a' : 'b' }}` boolean ternary, and the stable-id rules. Tag a mod that exposes tweaks with the [`tweakable`](mod-tags.md) tag so catalog browsers can filter for it.

### Curator: ship presets

A [collection](mod-collections.md) can set its own values for a mod's tweaks via a `tweaks:` map on the mod's entry — the headline collection use case. Ship the *same* mods twice with different values and you have two presets without forking anything.

```yaml
# in a .collection.yaml
mods:
  - id: pagonia-land.example.tweakable-economy
    version: "0.1.0"
    tweaks:
      softwood-cost: "6"   # this preset dials the mod up
```

When the manager installs the collection it **seeds** these curator values into the new profile, so a plan/deploy honours them. **Full reference:** [Mod Collections → Tweak Overrides](mod-collections.md#tweak-overrides).

### Player: configure a tweak

In `pagonia-manager`, tweak overrides live per profile. Three CLI verbs cover scripting, and an interactive wizard covers point-and-click:

```powershell
pagonia-manager tweak list  pagonia-land.example.tweakable-economy
pagonia-manager tweak set   pagonia-land.example.tweakable-economy softwood-cost 5
pagonia-manager tweak reset pagonia-land.example.tweakable-economy softwood-cost
```

`tweak list` shows each value's **origin** — `default`, `collection-default` (seeded by a collection, unchanged), or `profile-override` (you set it). The interactive shell offers the same under **Manage active profile → Configure this mod (tweaks)**. **Full reference:** [Manager CLI → Tweaks](../tools/pagonia-manager/CLI.md#tweaks).

## Which value wins (precedence)

The same tweak can be set in more than one place. Highest wins:

1. **lockfile pin** — a value recorded in a [collection lockfile](mod-collections.md#lockfiles) reproduces an exact past install, so it beats everything.
2. **`--tweak` CLI override** — an ad-hoc value for a single patcher run.
3. **profile override** — what you stored with `tweak set` (surfaces in the plan report as origin `external`).
4. **collection-supplied default** — the curator's preset, seeded into the profile at install.
5. **mod default** — the floor the author declared.

## Renaming a tweak without losing the player's choice

A manager stores a player's value keyed by the tweak `id`. If an author renamed a tweak, the stored choice would silently fall back to the default — the pitfall the [iModYourAnno tweak feature](https://github.com/anno-mods/iModYourAnno/wiki/Setting-up-your-Mod-for-tweaking) documents. Pagonia avoids it: the author keeps the new `id` and lists the old one under `aliases:`, and the manager **migrates the stored value forward** on first read (and rewrites the profile so it catches up). See [Manager CLI → Renamed tweaks (aliases)](../tools/pagonia-manager/CLI.md#renamed-tweaks-aliases).

## Where next

- [Declarative Patch Format → Tweaks](mod-patch-format.md#tweaks-tweaks-block) — declare them, the templating grammar, field rules.
- [Mod Collections → Tweak Overrides](mod-collections.md#tweak-overrides) — curator presets, seeding, reinstall behaviour.
- [Manager CLI → Tweaks](../tools/pagonia-manager/CLI.md#tweaks) — `tweak list/set/reset`, origins, JSON reports.
- [Mod Tags → `tweakable`](mod-tags.md) — the content tag for adjustable mods.
