# Release Process

1. Update `CHANGELOG.md`.
2. Run `.\scripts\build-release.ps1`.
3. Run `.\scripts\test-build.ps1`.
4. Run `.\scripts\package-modern-installer.ps1 -Version <version>` (also builds the self-contained portable ZIP).
5. Optionally run `.\scripts\package-release.ps1 -Version <version>` for the legacy rollback.
6. Inspect `dist\DwmLutModern-x64-portable-selfcontained`, its ZIP, and `dist\installer`.
7. Publish ZIP, MSI, and their SHA-256 files.
8. Attach compatibility matrix row updates.
9. Create release notes with exact Windows builds, GPU, display topology, and SDR/HDR cases tested.

Do not promote a profile from built/experimental to live-validated without preserved test evidence
and sanitized DWM logs.

GitHub Actions artifact documentation is relevant for CI release evidence:

https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
