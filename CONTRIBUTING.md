# Contributing

Contributions are welcome, especially for Windows build compatibility, DWM symbol updates, HDR behavior, multi-monitor reliability, documentation, and validation reports.

## Development Rules

1. Keep changes scoped.
2. Link PRs to issues.
3. Include exact Windows build numbers for compatibility changes.
4. Do not submit release binaries in pull requests.
5. Update `CHANGELOG.md` for user-visible changes.
6. Include logs when touching DWM hook or LUT selection behavior.

## Pull Request Checklist

- [ ] Linked issue or report
- [ ] Built with `.\scripts\build-release.ps1`
- [ ] Ran `.\scripts\test-build.ps1`
- [ ] Tested on at least one exact Windows build
- [ ] Updated docs if behavior changed
- [ ] Added changelog entry
