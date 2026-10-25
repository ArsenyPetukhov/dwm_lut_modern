## Summary

Explain what this PR changes.

## Windows Builds Tested

- [ ] 25H2
- [ ] 26H1
- [ ] 26H2 Experimental
- [ ] Canary / Future Platforms

Exact build numbers:

## GPU / Display Tested

GPU:
Driver:
Monitor:
HDR/SDR:

## Checklist

- [ ] Scoped change
- [ ] No unrelated formatting churn
- [ ] `.\scripts\build-release.ps1` passes
- [ ] `.\scripts\test-build.ps1` passes
- [ ] `.\scripts\capture-modern-ui.ps1` passes (when UI changed)
- [ ] Sandbox/VM/physical validation tier is stated honestly
- [ ] Multi-monitor/HDR changes include fail-closed cases
- [ ] Changelog updated
- [ ] Docs updated if needed

## Validation level

- [ ] Automated only
- [ ] Windows Sandbox package/UI smoke
- [ ] Hyper-V exact-build DWM injection
- [ ] Physical SDR/HDR and multi-monitor
- [ ] Instrumented color measurement

Evidence path/link and known gaps:
