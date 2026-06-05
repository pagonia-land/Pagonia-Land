# Security Policy

## Scope

This policy covers the tools and scripts shipped from this repository:

- `pagonia-paker` — `.pak` archive tool
- `pagonia-patcher` — declarative XML patcher
- `pagonia-manager` — mod install / profile / deploy CLI
- Helper scripts under `scripts/`
- The catalog browser under `tools/catalog-browser/`

It does **not** cover Pioneers of Pagonia itself, Envision Entertainment's game client, or third-party mods. Please report game-side issues directly to the game's publisher.

## Reporting A Vulnerability

If you believe you have found a security issue (e.g. arbitrary file write, path traversal, code execution via crafted input, credential exposure), please **do not open a public issue**.

Preferred channel: open a private security advisory via GitHub's "Report a vulnerability" button on the repository's Security tab. This keeps the report private and gives us a structured place to coordinate a fix.

If GitHub advisories are not workable for you, contact the maintainers via a private channel (project Discord DM, or an email address linked from a maintainer profile).

The same private channels also accept confidential [Code of Conduct](CODE_OF_CONDUCT.md) reports — just note in the report that it is a Code of Conduct matter, not a security vulnerability.

When reporting, include:

- A short description of the issue
- Steps to reproduce, ideally with a minimal input
- The affected tool and version (commit hash or release tag)
- Your assessment of impact (what an attacker could do)

## What To Expect

- Acknowledgement: within a few days under normal conditions. This is a small-team project, so response time can vary around game updates and busy periods.
- Triage: we will confirm or contest the issue and discuss a fix timeline with you.
- Fix: addressed in a follow-up commit / release, with a credit line in the release notes (or the relevant tool changelog) unless you ask to remain anonymous.

## Safe-Harbour Expectations

We treat good-faith security research as a contribution. We will not pursue legal action against researchers who:

- act in good faith to identify and report issues
- do not exfiltrate data beyond the minimum needed to demonstrate the issue
- give us reasonable time to fix the issue before public disclosure
- do not target game-data extraction beyond what the tools already legitimately do
