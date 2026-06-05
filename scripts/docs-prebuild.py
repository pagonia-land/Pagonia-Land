"""Run the docs prebuild step standalone.

MkDocs validates `docs_dir` at config-load time, which is BEFORE plugin
hooks fire — so the docs_build/ mirror needs to exist before `mkdocs
build` or `mkdocs serve` is invoked. This script does that mirror.

Once docs_build/ exists, `mkdocs serve` re-runs the prebuild on every
reload via the on_pre_build hook (see docs_hooks/prebuild.py), so
in-progress edits to imported source files (CONTRIBUTING.md,
tools/*/README.md, etc.) show up live.

Usage:
    python scripts/docs-prebuild.py

Or use one of the convenience wrappers:
    pwsh scripts/docs-serve.ps1
    pwsh scripts/docs-build.ps1
"""

import sys
from pathlib import Path

HOOK_DIR = Path(__file__).parent.parent / "docs_hooks"
sys.path.insert(0, str(HOOK_DIR))

from prebuild import _do_full_build  # type: ignore  # noqa: E402

if __name__ == "__main__":
    _do_full_build()
