# Version history and provenance

This repository uses annotated tags as source milestones and GitHub Releases as public summaries.
The v1-v4 tags are historical annotations placed on real existing commits; they do not claim that a
binary was published on that date. v5 is the first release with the complete managed test,
packaging, UI-capture, and validation-evidence workflow.

| Version | Commit | Date | Provenance milestone |
|---|---|---|---|
| v1.0.0 | `bc23e8b6c4791f9dcbec8a7ee1c0eab031e7b354` | 2026-06-29 | Initial maintained forward port, repository structure, modern Windows baseline |
| v2.0.0 | `d2e0b7fdfef284945d364acc1b966f70603eaf21` | 2026-06-29 | Cross-build catalog/architecture gates and repaired privilege/injection diagnostics |
| v3.0.0 | `ae74e6ac0463bb58514ab5402ba858741f5ebd8e` | 2026-06-29 | Deterministic PDB-profile generation and fail-closed native hardening foundation |
| v4.0.0 | `3d23617920cfc83e9afa64e8fcda3e1271f0748b` | 2026-06-29 | Multi-profile compatibility UX, offline validation, and simplified help flow |
| v5.0.0 | release commit/tag | 2026-07-10 | .NET 8 control plane, intent/recovery correctness, exact multi-monitor/domain routing, MSI/portable packaging, liquid-glass UI, validation lab |

## What visitors should inspect

- `CHANGELOG.md` describes user-visible behavior by release.
- The annotated tag message states whether the tag is a historical source milestone or a shipped release.
- GitHub Release notes summarize the milestone and link to the commit comparison.
- Pull requests group a coherent feature/release while individual commits remain reviewable.
- `docs/implementation` preserves the implementation plan/report.
- `docs/validation` and `artifacts/validation` define and collect evidence without overstating it.

## Commit policy

Use small, truthful commits that each build or document one concern. Good examples:

```text
test: cover unknown HDR telemetry and smoke instance isolation
fix: preserve unknown display signal state
feat: add liquid glass design system
feat: rebuild main WPF screens
test: isolate and capture the modern UI
feat: add sandbox and hardware validation tooling
docs: publish architecture review and v1-v5 provenance
```

Do not rewrite one large finished change into fabricated “development steps,” backdate commits, or
claim old binaries existed. Granular provenance is useful only when every commit corresponds to a
real state of the source tree.

## Release mechanics

1. Create a branch and keep each concern in a buildable commit.
2. Open a pull request with the automated, Sandbox/VM, and physical validation status separated.
3. Merge after CI/review.
4. Create an annotated tag on the merge commit: `git tag -a v5.0.0 -m "DWM LUT Modern v5.0.0"`.
5. Push the tag and create a GitHub Release with ZIP/MSI/checksum assets.
6. Preserve the completed evidence template and sanitized logs for each promoted DWM profile.

Historical milestone releases should say “source milestone; no binary assets” in their notes. That
keeps the public timeline useful without implying distribution or validation that did not occur.
