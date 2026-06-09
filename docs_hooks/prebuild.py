"""MkDocs prebuild hook.

Builds docs_build/ as a complete mirror of docs/ plus selected root-level
docs + tools READMEs + schemas READMEs, with relative markdown links
rewritten so cross-tree references resolve correctly inside MkDocs.

mkdocs.yml has docs_dir: docs_build. docs_build/ is gitignored — it is
regenerated on every build from the source tree.

Why a build-dir mirror (vs editing source files): the cross-tree links
in source files use repo-root-relative forms like '../CONTRIBUTING.md'
that GitHub renders correctly. Rewriting them in source would break the
GitHub render. Mirroring + rewriting only the copies keeps both renders
working from one source of truth.
"""

import re
import shutil
from os.path import relpath as osp_relpath
from pathlib import Path, PurePosixPath

REPO_ROOT = Path(__file__).parent.parent
DOCS_SRC = REPO_ROOT / "docs"
DOCS_BUILD = REPO_ROOT / "docs_build"

# Links to real repo files that are NOT mirrored into the site (scripts,
# schemas, workflows, tool sources, example manifests, game-gdb/README, ...)
# would 404 on the GitHub Pages site if left as relative paths. They are
# rewritten to absolute GitHub source URLs so they resolve on the published
# site; the unmodified source files keep the relative links that GitHub's
# own in-repo render needs. git ls-files is the source of truth for "exists
# on GitHub", so gitignored local data (generated/, snapshots/, ...) is never
# linked to a URL that would 404.
GITHUB_REPO = "https://github.com/pagonia-land/Pagonia-Land"
GITHUB_BRANCH = "main"  # the published repo default branch the rewritten source links point at

# Mapping: source path relative to repo root -> destination path relative to
# docs_build/ (under _imported/ for clarity).
IMPORTS = {
    # Root-level project metadata + analytical docs
    "CONTRIBUTING.md": "_imported/contributing.md",
    "CODE_OF_CONDUCT.md": "_imported/code-of-conduct.md",
    "SECURITY.md": "_imported/security.md",
    "REVIEWING.md": "_imported/reviewing.md",
    "NOTICE.md": "_imported/notice.md",
    "CHANGELOG.md": "_imported/changelog.md",
    "DATABASE_OVERVIEW.md": "_imported/database-overview.md",
    "REFERENCE.md": "_imported/reference.md",
    "VALIDATION_BASELINE.md": "_imported/validation-baseline.md",
    "UPDATE_PLAYBOOK.md": "_imported/update-playbook.md",
    # Tool docs
    "tools/README.md": "_imported/tools/index.md",
    "tools/pagonia-manager/README.md": "_imported/tools/pagonia-manager/index.md",
    "tools/pagonia-manager/CLI.md": "_imported/tools/pagonia-manager/cli.md",
    "tools/pagonia-manager/CHANGELOG.md": "_imported/tools/pagonia-manager/changelog.md",
    "tools/pagonia-patcher/README.md": "_imported/tools/pagonia-patcher/index.md",
    "tools/pagonia-patcher/CLI.md": "_imported/tools/pagonia-patcher/cli.md",
    "tools/pagonia-paker/README.md": "_imported/tools/pagonia-paker/index.md",
    "tools/pagonia-paker/CLI.md": "_imported/tools/pagonia-paker/cli.md",
    "tools/catalog-browser/CHANGELOG.md": "_imported/tools/catalog-browser/changelog.md",
    # Sandbox workshop docs — substantive READMEs only. The "your stuff goes
    # here" placeholders under sandbox/mods/ and sandbox/collections/ stay
    # local-only since they only make sense in a filesystem-tree context.
    "sandbox/README.md": "_imported/sandbox/index.md",
    "sandbox/examples/README.md": "_imported/sandbox/examples.md",
    "sandbox/examples/manager-walkthrough/README.md": "_imported/sandbox/manager-walkthrough.md",
    # Schema folder READMEs
    "schemas/README.md": "_imported/schemas/index.md",
    "schemas/manager/README.md": "_imported/schemas/manager.md",
    "schemas/mod-patches/README.md": "_imported/schemas/mod-patches.md",
    "schemas/patcher/README.md": "_imported/schemas/patcher.md",
    "schemas/paker/README.md": "_imported/schemas/paker.md",

    "examples/README.md": "_imported/examples/index.md",
    "examples/mod-repo-example/README.md": "_imported/examples/mod-repo-example.md",
    "examples/mod-catalog-example/README.md": "_imported/examples/mod-catalog-example.md",
}

# Match markdown links: [text](path) — but not images ![](path).
# Limit to NOT-! prefix to skip image links (we don't import images anyway).
LINK_PATTERN = re.compile(r'(?<!!)(?P<pre>\[[^\]]*\]\()(?P<link>[^)\s]+)(?P<post>\))')


def normalize_posix(path: PurePosixPath) -> str:
    """Resolve .. and . segments in a posix path string."""
    parts = []
    for part in path.parts:
        if part == '..':
            if parts and parts[-1] != '..':
                parts.pop()
            else:
                parts.append(part)
        elif part != '.':
            parts.append(part)
    return PurePosixPath(*parts).as_posix() if parts else '.'


def copy_into_build():
    """Mirror docs/ + imports into docs_build/. Return {build_rel: source_rel}."""
    if DOCS_BUILD.exists():
        shutil.rmtree(DOCS_BUILD)
    DOCS_BUILD.mkdir(parents=True)
    source_of = {}

    # Copy docs/ tree as-is
    for src_file in DOCS_SRC.rglob("*"):
        if src_file.is_dir():
            continue
        rel = src_file.relative_to(DOCS_SRC).as_posix()
        dst = DOCS_BUILD / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src_file, dst)
        source_of[rel] = f"docs/{rel}"

    # Copy imports
    missing = []
    for src_rel, dst_rel in IMPORTS.items():
        src_path = REPO_ROOT / src_rel
        if not src_path.exists():
            missing.append(src_rel)
            continue
        dst_path = DOCS_BUILD / dst_rel
        dst_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src_path, dst_path)
        source_of[dst_rel] = src_rel

    if missing:
        print(f"[prebuild] WARN: missing import sources skipped: {missing}")

    return source_of


def git_tracked_paths():
    """Return (files, dirs) sets of git-tracked repo paths (posix), or
    (None, None) if git is unavailable — callers then skip GitHub-URL rewriting
    and leave such links relative (best effort)."""
    import subprocess
    try:
        out = subprocess.run(
            ["git", "ls-files"], cwd=str(REPO_ROOT),
            capture_output=True, text=True, check=True,
        ).stdout
    except Exception:
        return None, None
    files = {line for line in out.split("\n") if line}
    dirs = set()
    for f in files:
        for parent in PurePosixPath(f).parents:
            s = parent.as_posix()
            if s != ".":
                dirs.add(s)
    return files, dirs


def rewrite_file_links(build_rel, source_of, src_to_build, tracked_files, tracked_dirs):
    """Rewrite cross-tree relative markdown links in one file."""
    file_path = DOCS_BUILD / build_rel
    if not file_path.is_file() or not build_rel.endswith('.md'):
        return 0

    src_rel = source_of[build_rel]
    src_parent = PurePosixPath(src_rel).parent.as_posix()
    new_parent = PurePosixPath(build_rel).parent.as_posix()

    content = file_path.read_text(encoding='utf-8')
    rewrites = 0

    def repl(match):
        nonlocal rewrites
        link = match.group('link')

        # Skip external and pure-anchor links
        if link.startswith(('http://', 'https://', 'mailto:', '#', '/')):
            return match.group(0)

        # Split anchor
        if '#' in link:
            path_part, anchor = link.split('#', 1)
            anchor_suffix = '#' + anchor
        else:
            path_part, anchor_suffix = link, ''

        if not path_part:
            return match.group(0)

        # Resolve link as path relative to source file's original location
        if src_parent and src_parent != '.':
            resolved = PurePosixPath(src_parent) / path_part
        else:
            resolved = PurePosixPath(path_part)
        resolved_str = normalize_posix(resolved)

        # Look up in our path map
        if resolved_str in src_to_build:
            target_in_build = src_to_build[resolved_str]
            anchor_from = new_parent if new_parent and new_parent != '.' else '.'
            new_link = osp_relpath(target_in_build, anchor_from).replace('\\', '/')
            rewrites += 1
            return f"{match.group('pre')}{new_link}{anchor_suffix}{match.group('post')}"

        # Not a mirrored doc. If it points at a real git-tracked repo file or
        # directory (scripts, schemas, workflows, tool sources, example
        # manifests, game-gdb/README, ...), a relative link 404s on the
        # published site — rewrite it to the GitHub source so it resolves there
        # too. The in-repo source file is untouched, so GitHub's own render
        # keeps using the working relative path.
        if tracked_files is not None and resolved_str not in ('', '.'):
            if resolved_str in tracked_files:
                gh = f"{GITHUB_REPO}/blob/{GITHUB_BRANCH}/{resolved_str}"
                rewrites += 1
                return f"{match.group('pre')}{gh}{anchor_suffix}{match.group('post')}"
            if resolved_str in tracked_dirs:
                gh = f"{GITHUB_REPO}/tree/{GITHUB_BRANCH}/{resolved_str}"
                rewrites += 1
                return f"{match.group('pre')}{gh}{anchor_suffix}{match.group('post')}"

        # Truly unknown (gitignored local data, typo, ...) — leave as-is.
        return match.group(0)

    new_content = LINK_PATTERN.sub(repl, content)
    if new_content != content:
        file_path.write_text(new_content, encoding='utf-8')
    return rewrites


def _do_full_build():
    source_of = copy_into_build()
    src_to_build = {v: k for k, v in source_of.items()}
    tracked_files, tracked_dirs = git_tracked_paths()

    total_rewrites = 0
    for build_rel in source_of:
        total_rewrites += rewrite_file_links(build_rel, source_of, src_to_build, tracked_files, tracked_dirs)

    print(f"[prebuild] mirrored {len(source_of)} files into docs_build/, "
          f"rewrote {total_rewrites} cross-tree links")


def on_startup(command, dirty):  # noqa: ARG001 — mkdocs hook signature
    """Runs before config validation. Needed because mkdocs validates that
    docs_dir exists at config-load time, before on_pre_build fires."""
    _do_full_build()


def on_pre_build(config, **kwargs):  # noqa: ARG001
    """Runs on every (re)build. With mkdocs serve, this catches changes to
    files outside docs_build/ that are watched via the watch: block."""
    _do_full_build()
