# Reviewing Pagonia Land

This page is for people **evaluating** the repository before they commit time to it — modders deciding whether to dive in, tooling devs checking whether the schemas are worth integrating against, or anyone giving the project a first critical read. If you're already past evaluation and want to contribute changes, go to [CONTRIBUTING.md](CONTRIBUTING.md) instead.

The goal here is to make your evaluation **useful with the time you have**: pick a path, follow it, and tell us what you found (see [How To Send Feedback](#how-to-send-feedback)).

## Pick A Path

### 🔍 I want to try the tools (15–30 min)

The fastest "does this actually work for me" path:

1. Download the right binary for your OS from the [latest release](https://github.com/pagonia-land/Pagonia-Land/releases/latest) — manager, patcher, paker as Native AOT single-files.
2. Run `pagonia-manager` with no arguments. The interactive shell launches; pick **Install a mod** and point it at one of the sample mods under `sandbox/examples/`.
3. Go through **Plan + deploy to game** against a real Pioneers of Pagonia install (the manager backs up first, so rollback is safe).
4. Try **Roll back last deploy** to verify the byte-identical restore.

Worth telling us:
- Did the binary launch on your platform without warnings? (Especially macOS — the binaries are ad-hoc signed for Intel and Apple Silicon; Gatekeeper might still complain on first run.)
- Did the interactive menu feel intuitive, or did you bounce off any wizard step?
- Did anything in the flow surprise you in a bad way?

### ✏️ I want to try writing a mod (30–60 min)

Walk through one of the example flows end-to-end:

1. Read [Local Setup](docs/setup.md) (start with its **Quick Path** section).
2. Read one worked example, e.g. [Change A Building Construction Cost](docs/examples/change-building-cost.md) or [Trace The Sawmill Production Chain](docs/examples/trace-sawmill-chain.md).
3. Look at the `mod.yaml` + patch format under [`schemas/mod-patches/`](schemas/mod-patches/) and one or two examples under [`sandbox/examples/`](sandbox/examples/).
4. Write your own tiny patch (rename a building, change a cost) using `pagonia-patcher`.

Worth telling us:
- Where did the docs stop making sense? Which concept was missing or hard to find?
- Is the `mod.yaml` format ergonomic, or does the patch DSL feel awkward for what you wanted to do?
- Did `pagonia-patcher plan` show you what you expected before applying?

### 🧩 I'm a tooling dev / integrator (20–40 min)

If you're considering building a GUI, IDE plugin, web validator, or mod manager that integrates against the contracts:

1. Skim [`schemas/`](schemas/) — `mod-patches/` is the patch + mod manifest format; `manager/` is every CLI report shape; `patcher/` + `paker/` are tool-specific reports.
2. Run `pagonia-manager schema-validate` and `pagonia-patcher schema-validate` against the sample outputs in [`sandbox/examples/`](sandbox/examples/) to see how the reference implementation behaves.
3. Read [`CONTRIBUTING.md`](CONTRIBUTING.md#when-you-touch-tools-or-schemas) for the schema-stability commitment.

Worth telling us:
- Are the schemas readable on their own, or do you need to read the C# source to understand a field?
- Is anything missing from the report shapes that you'd need? (Per-file timings, deeper conflict info, additional status fields…)
- Would you want a versioned schema URL (e.g. `pagonia.land/schemas/manager/1/...`) that you could trust across releases?

### 📖 I'm reviewing the documentation (30 min)

The docs are split into chapters, each one trying to stand alone:

1. Skim the [README](README.md) and pick three chapters from the table of contents that interest you.
2. Read each as if you didn't know the rest of the repo. Note anywhere a term is used that wasn't defined, anywhere a link points at the wrong thing, anywhere advice contradicts another page.
3. Try one worked example from start to finish — does it actually walk you to a working result?

Worth telling us:
- Where does the conceptual order feel wrong? Should something be moved earlier / later?
- What's the most confusing single page?
- What modding question did you arrive with that the docs **don't** answer?

## Things To Ignore

These are deliberate boundaries, not gaps — no need to flag them:

- **GUI on top of the manager.** A separate project, not in scope here. The CLI + schemas are the foundation a GUI would build on.
- **Tool-internal localization.** The CLI strings are English-only today.
- **Catalog data redistribution.** The catalog browser is a tool; the search index it reads is generated locally from your own extracted game data and is never shipped in this repository. That's a deliberate content boundary (the repo publishes tooling, not game-derived data), not a missing feature.

## How To Send Feedback

- **Bugs or concrete issues:** open a GitHub issue using one of the [issue templates](.github/ISSUE_TEMPLATE/).
- **Open-ended thoughts, "felt weird" reactions, "have you considered" suggestions:** GitHub Discussions if available, otherwise a single Issue with a `feedback:` prefix in the title is fine.
- **Security issues:** see [SECURITY.md](SECURITY.md) — please don't open a public issue.

The most valuable feedback is the stuff that's obvious to you in the first ten minutes and invisible to someone who built the repo — first impressions, a term that wasn't defined, a step that didn't work. That's exactly what outside eyes catch.
