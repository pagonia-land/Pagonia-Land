This file is a placeholder ridealong for the add-extra-asset example mod.

When `pagonia-patcher apply` runs, this exact byte payload is copied to
sandbox/out/core/gdb/notes-readme.txt (matching the `entries.add.path` from
mod.yaml). When sandbox-pack -BasePak then bundles the result into a .pak,
`pagonia-paker patch` classifies this path as Add — the entry name doesn't
already exist in the base pak, so it's appended as a new entry.

Replace this file (and the path in mod.yaml) with your real asset for a real
mod.
