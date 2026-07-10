# Tests

The automated suite treats Windows-specific operations as boundaries and tests product behavior
through `AppController`, `AppStateStore`, CLI parsing, per-user named-pipe IPC, display identity,
hotkey registration, and DWM profile validation labels. Run it directly with:

```powershell
dotnet test .\tests\DwmLutModern.App.Tests\DwmLutModern.App.Tests.csproj -c Release -p:Platform=x64
```

`scripts/test-build.ps1` is the complete repository gate: it rebuilds native/managed Release,
runs these tests, checks package inputs, and statically rejects the removed unsafe native routing
fallbacks.

DWM injection, GPU composition, HDR domain transitions, Task Scheduler elevation, and calibration
accuracy remain machine/session-specific. Validate those with `manual-test-plan.md` and preserve a
completed release-evidence template before promoting a profile.
