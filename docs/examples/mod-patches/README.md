# Mod Patch Examples

This folder contains v0.1 examples for the declarative mod patch format.

These examples are documentation, not ready-to-run mods. They show how a modder can describe changes in a structured way so the patcher can validate and apply them safely.

Read first:

- [Declarative Mod Patch Format](../../mod-patch-format.md)
- [Mod Patch Conflict Model](../../mod-conflicts.md)
- [GUID Reference](../../guid-reference.md)
- [Validation Baseline](../../../VALIDATION_BASELINE.md)

## Examples

| Example | What It Shows |
| --- | --- |
| [Cheaper Sawmill Patch](cheaper-sawmill.md) | Low-risk `replaceValue` patch for a building construction cost |
| [Optional DLC Patch Set](optional-dlc-patch.md) | Package-aware patch sets for core plus optional `dlc1` support |
| [Resource Icon Patch](resource-icon-patch.md) | Low-risk UI asset path replacement with expected old value |
| [Conflict Example](conflict-example.md) | How two mods can conflict by writing the same target |

## Important

Do not copy these files into a game directory. They are format examples for discussion and for the Pagonia Land Patcher to consume.

The recommended mental model is:

```text
Patch file -> dry-run validation -> conflict report -> apply to a test copy -> database validation -> in-game test
```
