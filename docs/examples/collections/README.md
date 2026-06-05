# Collection Examples

These examples show v0.1 collection manifests that `pagonia-manager`, the patcher's collection resolver, and any future mod manager built against the [`schemas/mod-patches/`](../../../schemas/mod-patches/) contract can consume.

Collections do not patch XML directly. They describe which mods should be installed together and in which order.

| Example | Purpose |
| --- | --- |
| [Beginner QoL Collection](beginner-qol.collection.yaml) | Small curated starter set |
| [Beginner QoL Lockfile](beginner-qol.collection-lock.yaml) | Example resolved installation state |

The examples are intentionally small. They are meant to test the format, not to describe a real downloadable mod pack yet.

A few things are deliberately illustrative:

- The collection id here uses the `pagonia-land.collections.*` namespace. The separate, runnable [`examples/mod-repo-example/`](https://github.com/pagonia-land/Pagonia-Land/tree/main/examples/mod-repo-example) tree ships its own *independent* `beginner-qol` collection under the `pagonia-land.example.*` namespace (matching the example *mod* ids in that repo). The two trees do not reference each other — they are two standalone illustrations, so the differing namespaces are intentional, not a mismatch.
- In the lockfile, `resolvedSource` (`https://example.invalid/...`) and the `archiveSha256` placeholder hashes (`0000…`, `1111…`) are **dummy values**. A real resolver fills them with the actual download URL and content hash at resolve time.
