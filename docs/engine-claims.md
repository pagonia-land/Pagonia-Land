# 🔎 What We Know About the Engine

This page is a **single, scannable register of every load-bearing claim** this documentation makes about how the Pioneers of Pagonia engine and GameDatabase behave — each tagged with *how sure we are* and *where the evidence comes from*.

It exists for two audiences:

- **Envision Entertainment developers reviewing this site** — scan one page, see exactly which claims are EE-confirmed, which we derived from scanning shipped data, and which are reasoned inferences. Correcting a single row here is the cheapest possible way to keep the whole docs set accurate. (Thank you — the 2026-06-06 review corrected two rows below.)
- **Modders and contributors** — know which guidance is solid ground and which is "best current understanding, verify in-game."

> This page is a deliberately *flat register* — one row per claim, source-linked. The detail and reasoning live on the linked pages; this is the index over them.

> **Maintenance rule.** When a claim is confirmed, corrected, or refuted, update its row here *and* the canonical page it links to. Never let this register and a detail page disagree. When you add a new engine/database claim anywhere in the docs, add its row here too. See the methodology note in [quirks-and-anomalies.md → `InheritanceMode="Unload"` ships but is unused](quirks-and-anomalies.md#inheritancemodeunload-ships-but-is-unused) for *why* a data scan alone is not enough.

**Baseline:** `1.4.0-12032+195221` (1.4.0 "Quality of Life & Pagonia Editor Update", official release). **Register captured:** 2026-06-14 (register unchanged through 1.4.0-12032 — every 1.4.0 build since capture added only non-engine-behaviour data: localization tags, a mod-browser filter entity, an `IncludeForEmployAllUnitsAchievement` opt-in flag, and a four-recipe steel-weapon rework — none of which touch the engine/database claims below).

## How to read the status column

| Status | Meaning |
| --- | --- |
| 🟢 **EE-confirmed** | An Envision Entertainment developer stated it directly. The most authoritative tier. Cited to the dev statement. |
| 🔵 **Data-derived** | Measured directly from shipped paks / XML scans / version diffs. High confidence on *what the data contains* — but blind to engine features the data doesn't exercise (see the Unload lesson). |
| 🟡 **Inferred** | A reasoned conclusion from data + patterns, not directly confirmed. Usually right, but the place to be skeptical. |

---

## 🟢 EE-confirmed

Direct developer statements — each row's **Source** links straight to the quoted statement in [mod-distribution.md](mod-distribution.md#cross-pak-entity-merging-meadowsong).

| Claim | Source |
| --- | --- |
| The engine merges every loaded module's `*.gd.xml` into one logical entity set per game session. | [Dev statement 1](mod-distribution.md#dev-statement-1) |
| Modders relate entities across paks via four primitives the devs named: **unload / replace / template / extend**. | [Dev statement 1](mod-distribution.md#dev-statement-1) |
| GDB modding was announced as a two-wave rollout — **per-map first**, then **globally active** while the mod is enabled; **both scopes shipped with the 1.4.0 official release** (2026-06-24). | [Dev statement 2](mod-distribution.md#dev-statement-2), [1.4.0 patch notes](mod-distribution.md) |
| There is an **editor for the game database** (originally EE's internal authoring tool); EE surfaced it to modders by extending the Pagonia Editor. **As of the 1.4.0 official release the editor authors both map-scoped and globally-active GDB mods** — create / replace / unload / enhance via *proxies* — plus single-language localization. No hand-written `*.gd.xml` required (it remains the power-user / CI / batch path). | [Dev reply](mod-distribution.md#dev-reply), [1.4.0 patch notes](mod-distribution.md) |
| **Custom mesh import is not supported yet** — new entities must reuse shipped meshes/textures/audio. | [Dev reply](mod-distribution.md#dev-reply) |
| **`InheritanceMode="Unload"` is engine-implemented since Meadowsong but unused in shipped content.** A proxy entity unloads a target so the merged DB omits it entirely. | [Dev statement 3](mod-distribution.md#dev-statement-3) |
| An `Unload` is **safe only when nothing else references the target**; an effective unload usually means unloading the target's dependents together. Some subsystems validate references, others don't → finding the safe set is currently trial-and-error. | [Dev statement 3](mod-distribution.md#dev-statement-3) |
| **Externally rewriting shipped paks is not EE's intended path.** The intended model is *separate* paks that replace files / contribute entities **at load time**. | [Dev statement 4](mod-distribution.md#dev-statement-4) |
| **Multi-mod conflict resolution by load order:** for same-path file conflicts and for two mods that `Replace` the same entity, the **last-loaded mod wins**. `Incremental` is the exception — multiple mods **stack** additions onto the same inherited entity. | [Dev statement 4](mod-distribution.md#dev-statement-4) |
| **Authoring guidance:** prefer `Incremental` / `Template` over `Replace` / `Unload` when editing core/dlc entities, to minimise inter-mod conflicts. | [Dev statement 4](mod-distribution.md#dev-statement-4) |

---

## 🔵 Data-derived

Measured from local scans of the shipped paks and version diffs. Reproduce with `scripts/validate_database.ps1`, `scripts/analyze_database.ps1`, and `scripts/diff_versions.ps1`.

| Claim | Evidence |
| --- | --- |
| 4,717 entity definitions, **0 duplicate GUIDs** (1.4.0). | [dlc-patch-override-model.md → Current Evidence](dlc-patch-override-model.md#current-evidence) |
| `InheritanceMode` use in shipped data: **18 Template / 14 Replace / 19 Incremental / 0 Unload**. | [mod-distribution.md → Confirmed primitives](mod-distribution.md#confirmed-primitives) |
| Each of the 51 shipped `InheritanceMode` uses targets a **distinct** `InheritedGuid` — no two-mods-on-one-target conflicts exist *in shipped data*. | [quirks-and-anomalies.md → Each InheritedGuid target is hit exactly once](quirks-and-anomalies.md#each-inheritedguid-target-is-hit-exactly-once) |
| The encounter-level null-GUID `<ReplaceSelf>` "unload" pattern is pre-Meadowsong (13 in 1.2.2 → 17 in 1.3.0). It is **distinct** from the entity-level `Unload` mode. | [mod-distribution.md → Confirmed primitives](mod-distribution.md#confirmed-primitives) |
| The `.gd.bin` index format (7-byte v3 header) is stable across the 1.2.2 → Meadowsong jump. | [mod-distribution.md](mod-distribution.md#cross-pak-entity-merging-meadowsong) |
| Reference resolution rate ≈ **77%**; null-GUID share ≈ **23%** and rising — driven by optional-empty fields, not reference rot. | [quirks-and-anomalies.md → Reference resolution rate drops](quirks-and-anomalies.md#reference-resolution-rate-drops-with-each-new-version) |
| Component vocabulary grew 281 → 313 (Meadowsong) → 315 (1.3.1) types; unchanged through 1.4.0. | [CHANGELOG.md](../CHANGELOG.md) |
| The Pattern B overlay model is real and shipping in the wild (the mod.io camera/zoom mod overrides `system.json` from a side-by-side pak). | [mod-distribution.md → Pattern B](mod-distribution.md) |

---

## 🟡 Inferred

Reasoned from the data and from how the shipped content is organised. Usually correct, but not directly confirmed — flag these as "best understanding."

| Claim | Note |
| --- | --- |
| Entity GUIDs are **global**, not package-local. | Strongly implied by 0 duplicate GUIDs + heavy cross-package references; not stated by EE. |
| Package folders are **source layers**, not isolated databases; same file names across packages **merge** rather than file-replace. | The merge behaviour for `*.gd.xml` is confirmed; "folders are just source layers" is our framing. |
| The `tools` package is editor/Magmaview data, **possibly not loaded** in normal gameplay the way `core`/`dlc1` are. | Load context unconfirmed. |
| "Variant by reference" (new GUID + scenario/unlock pointer) is **safer** than overriding a core entity. | A best-practice judgement, now reinforced by EE's prefer-additive guidance. |
| The Pattern A / B / C distribution taxonomy. | Pattern B is observed in the wild; the three-way taxonomy is *our* organising model, not EE's vocabulary. |
| EE's `NoMVP.` prefix means "not part of MVP" (unshipped placeholder content). | Prefix is observed; the expansion of the acronym is inferred. |

---

## See also

- [DLC Patch and Override Model](dlc-patch-override-model.md) — the working merge/override model in depth.
- [Cross-Pak Entity Merging (Meadowsong)](mod-distribution.md#cross-pak-entity-merging-meadowsong) — the canonical home of all dev statements.
- [Quirks and Anomalies](quirks-and-anomalies.md) — surprising-but-true observations, including the Unload methodology lesson.
- [VALIDATION_BASELINE.md](../VALIDATION_BASELINE.md) — the current measured baseline numbers.
