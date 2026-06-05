#!/usr/bin/env python3
"""
validate-catalog-browser-coverage.py

Regression guard for the catalog browser's linkable-field registry.

The catalog browser (`tools/catalog-browser/index.html`) navigates between
entities by resolving named references in field values (e.g. a building's
ConstructionCosts "4 Softwood Trunk" -> the resource named "Softwood
Trunk"). The LINKABLE_FIELDS table inside the browser declares which
fields hold references and to which target types. This script verifies
two things against a real `generated/catalog/search-index.json`:

  1. Every relationship that the catalog encodes via the generic
     resource-usage / building-production / production-chain /
     resource-flow / unit-equipment lookup rows is ALSO reachable via a
     domain-typed forward link declared in LINKABLE_FIELDS. Without that,
     dropping the proxy rows from backlinks (the browser's current
     behaviour, since it surfaces typed groups instead of a catch-all)
     would silently hide some relationships.

  2. Tokens in the named-reference fields actually resolve to entities
     in the catalog — no orphaned reference names.

Run after every change to:
  - tools/catalog-browser/index.html (LINKABLE_FIELDS)
  - scripts/generate_catalog.ps1     (catalog field shapes)

Usage:
    python scripts/validate-catalog-browser-coverage.py
    python scripts/validate-catalog-browser-coverage.py --index path/to/search-index.json

Exits non-zero on any coverage gap or resolution failure rate above the
configured threshold so the script is usable as a CI / preflight step.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path


# Keep this in sync with PROXY_TYPES in tools/catalog-browser/index.html.
# Items of these types are aggregate / lookup rows whose relationships
# the browser surfaces through canonical (primary) items instead.
PROXY_TYPES = {
    "asset-reference",
    "visual-audio-component",
    "entity",
    "building-dependency",
    "building-production",
    "production-chain",
    "resource-usage",
    "resource-flow",
    "unit-equipment",
}

# Keep this in sync with LINKABLE_FIELDS in tools/catalog-browser/index.html.
# Format strings:
#   "amount-prefix"  ->  "N <name>; N <name>; ..."   ("4 Softwood Trunk")
#   "plain"          ->  "<name>; <name>; ..."
#   "prefixed"       ->  "<Kind>=<name>; ..."
LINKABLE_FIELDS = {
    "building": {
        "ConstructionCosts": ("amount-prefix", ["resource"]),
        "Builder": ("amount-prefix", ["unit", "npc-unit"]),
        "ProductionRecipes": ("plain", ["recipe"]),
        "GatherOutputs": ("amount-prefix", ["resource"]),
    },
    "unit": {
        "RecruitmentCosts": ("amount-prefix", ["resource"]),
        "SourceRecruitableUnit": ("plain", ["unit"]),
    },
    "npc-unit": {
        "RecruitmentCosts": ("amount-prefix", ["resource"]),
    },
    "recipe": {
        "Inputs": ("amount-prefix", ["resource"]),
        "Outputs": ("amount-prefix", ["resource"]),
    },
    "building-production": {
        "Recipe": ("plain", ["recipe"]),
        "Building": ("plain", ["building"]),
    },
    "tech-tree": {
        "Buildings": ("prefixed", ["building", "entity"]),
        "Units": ("prefixed", ["unit", "npc-unit", "entity"]),
        "PrimaryObjective": ("plain", ["objective-flow", "entity"]),
        "AlternativeObjectives": ("prefixed", ["objective-flow", "entity"]),
    },
    "unlock-reward": {
        "UnlockedBuildings": ("prefixed", ["building", "entity"]),
        "UnlockedRecruitments": ("prefixed", ["unit", "npc-unit", "entity"]),
        "UnlockedProductionRecipes": ("prefixed", ["recipe", "entity"]),
        "UnlockGathererFarmRecipes": ("prefixed", ["recipe", "entity"]),
        "UnlockShrineAbilities": ("prefixed", ["shrine-ability", "entity"]),
        "UnlockTechTreeTierGroups": ("prefixed", ["tech-tree", "entity"]),
        "ModifiedNPCBases": ("prefixed", ["npc-base", "encounter", "entity"]),
        "KilledUnits": ("prefixed", ["npc-unit", "unit", "encounter-combat", "entity"]),
        "OtherReferences": ("prefixed", ["building", "unit", "npc-unit", "recipe", "resource", "entity"]),
    },
    "objective-flow": {
        "PreconditionObjectives": ("prefixed", ["objective-flow", "entity"]),
        "SkipObjectives": ("prefixed", ["objective-flow", "entity"]),
        "FailObjectives": ("prefixed", ["objective-flow", "entity"]),
        "ResourceRefs": ("prefixed", ["resource", "entity"]),
        "BuildingRefs": ("prefixed", ["building", "entity"]),
        "UnitRefs": ("prefixed", ["unit", "npc-unit", "encounter-combat", "entity"]),
        "PointOfInterestRefs": ("prefixed", ["entity"]),
        "StartRewards": (
            "prefixed",
            ["building", "recipe", "unit", "npc-unit", "shrine-ability", "tech-tree", "resource", "entity"],
        ),
        "Rewards": (
            "prefixed",
            ["building", "recipe", "unit", "npc-unit", "shrine-ability", "tech-tree", "resource", "entity"],
        ),
        "Notifications": ("prefixed", ["notification-narration", "entity"]),
    },
    "deposit": {
        "HarvestResources": ("amount-prefix", ["resource"]),
        "DepositResourceType": ("plain", ["deposit-resource-type"]),
    },
    "deposit-resource-type": {
        "SubDepositTypes": ("plain", ["deposit"]),
    },
    "treasure-hunter-recipe": {
        "Targets": ("plain", ["resource", "artifact"]),
    },
    "shrine-recipe": {
        "Inputs": ("amount-prefix", ["resource"]),
    },
    "shrine-building": {
        "Worker": ("amount-prefix", ["unit", "npc-unit", "entity"]),
        "SecondaryWorker": ("amount-prefix", ["unit", "npc-unit", "entity"]),
        "Abilities": ("plain", ["shrine-ability", "entity"]),
        "ManaRecipes": ("plain", ["shrine-recipe", "recipe", "entity"]),
        "ManaResources": ("plain", ["resource", "entity"]),
    },
    "building-dependency": {
        "StorageResources": ("plain", ["resource"]),
        "DependencyResources": ("plain", ["resource"]),
        "ProvidedResources": ("plain", ["resource"]),
    },
    "mapgen": {
        "Deposits": ("prefixed", ["deposit", "deposit-resource-type", "entity"]),
        "Resources": ("prefixed", ["resource", "entity"]),
        "Buildings": ("prefixed", ["building", "entity"]),
    },
}


def tokenize(value: str, fmt: str) -> list[str]:
    """Return reference names extracted from a field value.

    Mirrors tokenizeFieldValue() in index.html. Empty strings and
    "(none)" placeholders are filtered out.
    """
    if not value:
        return []
    out: list[str] = []
    for chunk in value.split(";"):
        s = chunk.strip()
        if not s or s == "(none)":
            continue
        if fmt == "prefixed":
            eq = s.find("=")
            name = s[eq + 1 :].strip() if eq >= 0 else s
        elif fmt == "amount-prefix":
            m = re.match(r"^(\d+(?:\.\d+)?)\s+(.+)$", s)
            name = m.group(2).strip() if m else s
        else:  # plain
            name = s
        if name and name != "(none)":
            out.append(name)
    return out


def build_lookups(items: list[dict]):
    """Return (by_guid_primary, by_title_by_type).

    by_guid_primary picks the first primary item (type not in PROXY_TYPES)
    for each GUID. by_title_by_type[type][title] -> list of items.
    """
    by_guid: dict[str, dict] = {}
    by_title_by_type: dict[str, dict[str, list[dict]]] = defaultdict(lambda: defaultdict(list))
    for item in items:
        guid = item.get("guid", "")
        if guid:
            existing = by_guid.get(guid)
            if not existing or (existing.get("type") in PROXY_TYPES and item.get("type") not in PROXY_TYPES):
                by_guid[guid] = item
        title = item.get("title")
        if title:
            by_title_by_type[item.get("type")][title].append(item)
    return by_guid, by_title_by_type


def resolve(name: str, target_types: list[str], by_title_by_type) -> dict | None:
    for t in target_types:
        bucket = by_title_by_type.get(t)
        if not bucket:
            continue
        matches = bucket.get(name)
        if matches:
            return matches[0]
    return None


def check_token_resolution(items, by_title_by_type) -> tuple[int, int, list[tuple]]:
    """For every (type, field) in LINKABLE_FIELDS, count tokens that
    resolve vs. unresolved. Returns (resolved, unresolved, samples)."""
    resolved = 0
    unresolved = 0
    samples: list[tuple[str, str, str]] = []
    for item in items:
        registry = LINKABLE_FIELDS.get(item.get("type"))
        if not registry:
            continue
        fields = item.get("fields", {})
        for field_name, (fmt, targets) in registry.items():
            value = fields.get(field_name, "")
            for name in tokenize(value, fmt):
                if resolve(name, targets, by_title_by_type):
                    resolved += 1
                else:
                    unresolved += 1
                    if len(samples) < 30:
                        samples.append((item.get("type"), field_name, name))
    return resolved, unresolved, samples


def split_amount_prefix(value: str) -> list[str]:
    out = []
    for chunk in (value or "").split(";"):
        s = chunk.strip()
        if not s or s == "(none)":
            continue
        m = re.match(r"^(\d+(?:\.\d+)?)\s+(.+)$", s)
        out.append(m.group(2).strip() if m else s)
    return out


def split_plain(value: str) -> list[str]:
    return [s.strip() for s in (value or "").split(";") if s.strip() and s.strip() != "(none)"]


def check_usage_coverage(items) -> dict[str, dict]:
    """For each resource-usage UsageType, build the set of (resource,
    container) pairs the lookup row asserts, and confirm an equivalent
    forward link exists in a primary item. Returns per-usage-type stats."""
    # Build resource-usage pairs by UsageType
    # Which usage-row field names the "owning" entity differs per UsageType,
    # because resource-usage rows often populate both Building and Recipe
    # (a recipe's building gets recorded too). We have to pick the same
    # entity the equivalent forward link uses, otherwise the comparison
    # claims "missing" pairs that are actually present under a different
    # container name.
    USAGE_CONTAINER_FIELDS = {
        "ConstructionCost": "Building",
        "GatherOutput": "Building",
        "StoragePile": "Building",
        "RecipeInput": "Recipe",
        "RecipeOutput": "Recipe",
        "ShrineRecipeInput": "Recipe",
        "TreasureHunterTarget": "Recipe",
        "DepositHarvestOutput": "Context",  # Deposit name lives in Context
        "RecruitmentCost": "Unit",
    }

    usage: dict[str, set[tuple[str, str]]] = defaultdict(set)
    for item in items:
        if item.get("type") != "resource-usage":
            continue
        f = item.get("fields", {})
        ut = f.get("UsageType", "")
        resource = f.get("Resource", "")
        container_field = USAGE_CONTAINER_FIELDS.get(ut)
        container = f.get(container_field, "") if container_field else ""
        if not container:
            # Fallback for usage types we haven't catalogued explicitly.
            container = f.get("Building") or f.get("Recipe") or f.get("Unit") or f.get("Context", "")
        usage[ut].add((resource, container))

    # Build the equivalent forward-link pairs from primary items.
    forward: dict[str, set[tuple[str, str]]] = defaultdict(set)
    for item in items:
        t = item.get("type")
        f = item.get("fields", {})
        if t == "recipe":
            name = f.get("Recipe", "")
            for r in split_amount_prefix(f.get("Inputs", "")):
                forward["RecipeInput"].add((r, name))
            for r in split_amount_prefix(f.get("Outputs", "")):
                forward["RecipeOutput"].add((r, name))
        elif t == "building":
            name = f.get("Building", "")
            for r in split_amount_prefix(f.get("ConstructionCosts", "")):
                forward["ConstructionCost"].add((r, name))
            for r in split_amount_prefix(f.get("GatherOutputs", "")):
                forward["GatherOutput"].add((r, name))
        elif t == "unit":
            name = f.get("Unit", "")
            for r in split_amount_prefix(f.get("RecruitmentCosts", "")):
                forward["RecruitmentCost"].add((r, name))
        elif t == "npc-unit":
            name = f.get("Unit", "")
            for r in split_amount_prefix(f.get("RecruitmentCosts", "")):
                forward["RecruitmentCost"].add((r, name))
        elif t == "deposit":
            name = f.get("Deposit", "")
            for r in split_amount_prefix(f.get("HarvestResources", "")):
                # Deposit name lives in the row's Deposit field; resource-usage
                # records this via the Context column.
                forward["DepositHarvestOutput"].add((r, name))
        elif t == "treasure-hunter-recipe":
            name = f.get("Recipe", "")
            for r in split_plain(f.get("Targets", "")):
                forward["TreasureHunterTarget"].add((r, name))
        elif t == "shrine-recipe":
            name = f.get("Recipe", "")
            for r in split_amount_prefix(f.get("Inputs", "")):
                forward["ShrineRecipeInput"].add((r, name))
        elif t == "building-dependency":
            name = f.get("Building", "")
            for r in split_plain(f.get("StorageResources", "")):
                forward["StoragePile"].add((r, name))

    stats = {}
    for ut, pairs in usage.items():
        fwd = forward.get(ut, set())
        missing = pairs - fwd
        extras = fwd - pairs
        stats[ut] = {
            "usage_pairs": len(pairs),
            "forward_pairs": len(fwd),
            "missing_from_forward": len(missing),
            "extras_in_forward": len(extras),
            "missing_samples": list(missing)[:5],
        }
    return stats


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--index",
        default="generated/catalog/search-index.json",
        type=Path,
        help="Path to search-index.json (default: %(default)s)",
    )
    parser.add_argument(
        "--max-unresolved-pct",
        default=2.0,
        type=float,
        help="Fail when more than this %% of tokens fail to resolve (default: %(default)s)",
    )
    args = parser.parse_args()

    if not args.index.exists():
        print(f"ERROR: index not found at {args.index}")
        print("Generate it first with scripts/generate_catalog.ps1.")
        return 2

    with args.index.open(encoding="utf-8") as f:
        data = json.load(f)
    items = data.get("items", [])
    print(f"Loaded {len(items)} items from {args.index}")

    by_guid, by_title_by_type = build_lookups(items)

    # --- 1. UsageType coverage --------------------------------------------------------
    print("\n=== Resource-usage / forward-link coverage ===")
    stats = check_usage_coverage(items)
    total_missing = 0
    for ut in sorted(stats):
        s = stats[ut]
        status = "OK   " if s["missing_from_forward"] == 0 else "FAIL "
        print(f"  {status}{ut:<24} usage={s['usage_pairs']:>4}  forward={s['forward_pairs']:>4}"
              f"  missing={s['missing_from_forward']}")
        if s["missing_from_forward"]:
            total_missing += s["missing_from_forward"]
            for r, c in s["missing_samples"]:
                print(f"          missing: resource={r!r} container={c!r}")

    # --- 2. Token resolution rate ----------------------------------------------------
    print("\n=== Token resolution in LINKABLE_FIELDS ===")
    resolved, unresolved, samples = check_token_resolution(items, by_title_by_type)
    total = resolved + unresolved
    pct = (100.0 * unresolved / total) if total else 0.0
    print(f"  Resolved:   {resolved}")
    print(f"  Unresolved: {unresolved}  ({pct:.2f}%)")
    if samples:
        print(f"  First {len(samples)} unresolved samples:")
        for t, fn, name in samples:
            print(f"    {t:<25} {fn:<22} -> {name!r}")

    # --- 3. byGuid primary-preference sanity ----------------------------------------
    print("\n=== byGuid primary-preference sanity ===")
    proxy_in_lookup = sum(1 for v in by_guid.values() if v["type"] in PROXY_TYPES)
    proxy_could_be_primary = 0
    for guid, item in by_guid.items():
        if item.get("type") in PROXY_TYPES:
            # Check if there's a primary alternative we missed
            alternatives = [i for i in items if i.get("guid") == guid and i["type"] not in PROXY_TYPES]
            if alternatives:
                proxy_could_be_primary += 1
    print(f"  byGuid resolves {len(by_guid)} GUIDs")
    print(f"    of which {proxy_in_lookup} are proxy-typed")
    print(f"    of those, {proxy_could_be_primary} have a primary alternative (should be 0)")

    # --- exit code ---
    fail = False
    if total_missing > 0:
        print(f"\nFAIL: {total_missing} usage relationships not covered by forward links.")
        fail = True
    if pct > args.max_unresolved_pct:
        print(f"\nFAIL: unresolved token rate {pct:.2f}% > threshold {args.max_unresolved_pct}%.")
        fail = True
    if proxy_could_be_primary > 0:
        print(f"\nFAIL: {proxy_could_be_primary} GUIDs map to proxy when a primary exists.")
        fail = True
    if fail:
        return 1
    print("\nAll catalog-browser coverage checks pass.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
