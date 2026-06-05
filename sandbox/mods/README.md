# Your Mods

Drop your own mods into this folder. Each mod is one subfolder with a `mod.yaml` and a `patches/` directory:

```text
sandbox/mods/
├── my-first-mod/
│   ├── mod.yaml
│   └── patches/
│       └── buildings.yaml
└── another-mod/
    └── …
```

Anything in this folder (except this README and `.gitkeep`) is ignored by Git, so your work-in-progress mods stay local.

Quickest start: copy a template from [`sandbox/examples/`](../examples/README.md):

```powershell
Copy-Item -Recurse .\sandbox\examples\lower-sawmill-cost .\sandbox\mods\my-first-mod
```

Then run the apply script:

```powershell
.\scripts\sandbox-apply.ps1
```

See [`sandbox/README.md`](../README.md) for the full workflow.
