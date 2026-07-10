# Release Process

1. Update `CHANGELOG.md`, `docs/version-history.md`, and the compatibility matrix.
2. Keep the release work in granular, buildable commits; do not fabricate or backdate history.
3. Run `.\scripts\build-release.ps1` and `.\scripts\test-build.ps1`.
4. Run `.\scripts\capture-modern-ui.ps1`, inspect every screenshot, then use `-UpdateDocs` only for an approved capture.
5. Run `.\scripts\package-modern-installer.ps1 -Version <version>` (also builds the self-contained portable ZIP).
6. Optionally run `.\scripts\package-release.ps1 -Version <version>` for the legacy rollback.
7. Run `.\scripts\validation\Start-WindowsSandboxValidation.ps1 -Launch` against the package.
8. Complete the VM/physical cases in `docs/validation-lab.md` and collect elevated evidence.
9. Inspect `dist\DwmLutModern-x64-portable-selfcontained`, its ZIP, MSI, checksums, and evidence.
10. Merge the reviewed PR, then create an annotated SemVer tag on the merge commit.
11. Publish ZIP, MSI, checksum files, release notes, exact Windows/GPU/topology coverage, and limitations.

Do not promote a profile from built/experimental to live-validated without preserved test evidence
and sanitized DWM logs.

Historical v1-v4 source milestone tags/releases must explicitly say that no binary is claimed. See
`docs/version-history.md`.

GitHub Actions artifact documentation is relevant for CI release evidence:

https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
