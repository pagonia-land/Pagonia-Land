# Your Collections

Drop your own collection manifests here. A collection bundles several mods, locks them to specific versions, and gives them a stable load order:

```text
sandbox/collections/
├── my-qol-bundle.collection.yaml
└── another-bundle.collection.yaml
```

Anything in this folder (except this README and `.gitkeep`) is ignored by Git.

Apply a collection instead of a flat mods list with:

```powershell
.\scripts\sandbox-apply.ps1 -Collection .\sandbox\collections\my-qol-bundle.collection.yaml
```

The patcher resolves the collection against `sandbox/mods/`, so the referenced mods need to be present there.

A complete starter collection lives in [`sandbox/examples/example.collection.yaml`](../examples/example.collection.yaml). See [Mod Collections](../../docs/mod-collections.md) for the full format.
