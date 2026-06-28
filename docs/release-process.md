# Release Process

1. Update `CHANGELOG.md`.
2. Run `.\scripts\build-release.ps1`.
3. Run `.\scripts\test-build.ps1`.
4. Run `.\scripts\package-release.ps1 -Version <version>`.
5. Inspect `dist\<version>`.
6. Publish zip and `SHA256SUMS.txt`.
7. Attach compatibility matrix row updates.
8. Create release notes with exact Windows builds tested.

GitHub Actions artifact documentation is relevant for CI release evidence:

https://docs.github.com/actions/using-workflows/storing-workflow-data-as-artifacts
