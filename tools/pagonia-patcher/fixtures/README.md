# Patcher Fixtures

These fixtures are for Pagonia Land Patcher development.

They are intentionally tiny and artificial. They are not copied from extracted game files.

## Layout

```text
fixtures/
  collections/
    local-beginner.collection.yaml
  game-gdb-mini/
    core/
      buildings.gd.xml
      resources.gd.xml
  mods/
    cheaper-sawmill/
      mod.yaml
      patches/
        buildings.yaml
    conflicting-sawmill/
      mod.yaml
      patches/
        buildings.yaml
    broken-manifest/
      mod.yaml
      patches/
        buildings.yaml
    missing-target/
      mod.yaml
      patches/
        buildings.yaml
    expected-value-mismatch/
      mod.yaml
      patches/
        buildings.yaml
```

## Purpose

The mini XML database should be enough to test:

- loading mod manifests
- loading patch files
- resolving entity GUIDs
- resolving a component path
- checking an expected old value
- detecting duplicate write targets
- writing a patched output copy
- resolving a local collection
- writing a collection lockfile

## Fixture GUIDs

| Name | GUID |
| --- | --- |
| Sawmill | `c732cb26-7487-4a7b-b1ba-b65e094f9bac` |
| Softwood Trunk | `c22b4997-5563-44ab-8aa0-04a7b2c826be` |
| Stone Block | `d8dd765a-ac73-49cc-a9b9-f6102f6f8e07` |

These GUIDs mirror examples used in the documentation so examples stay easy to compare. The XML fixture structure itself is artificial and minimal.

## Expected Test Behavior

Using only `cheaper-sawmill`:

```text
Expected old value: 4
New value: 3
Result: valid plan
```

Applying `cheaper-sawmill` to `fixtures/out`:

```text
Output Softwood Trunk amount: 3
Source Softwood Trunk amount: 4
```

Using `cheaper-sawmill` and `conflicting-sawmill` together:

```text
Both mods write the same target.
Result: blocking conflict
```

Using `broken-manifest`:

```text
The mod can be read, but validation should fail.
Expected validation errors include invalid id, invalid GameDatabase version, unknown package, and duplicate operation id.
```

Using `missing-target`:

```text
Validation should pass, but planning should fail because the target entity GUID does not exist.
```

Using `expected-value-mismatch`:

```text
Validation should pass, but planning should fail because the XML value is 4 and the patch expects 99.
```

Using `collections/local-beginner.collection.yaml`:

```text
The resolver should enable cheaper-sawmill, skip disabled conflicting-sawmill, and write a lockfile with one enabled mod.
```
