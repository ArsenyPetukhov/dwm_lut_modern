# Roadmap

## v5 release gate

- [x] Desired/observed state separation and bounded recovery
- [x] Verified disable and partial-apply rollback
- [x] Current-user singleton/CLI ownership
- [x] Adapter-aware monitor persistence and exact signal-domain routing
- [x] Managed/native tests, UI capture, portable ZIP, and MSI
- [x] Liquid-glass-inspired WPF interface
- [x] Sandbox/evidence tooling and physical validation matrix
- [ ] Exact-build elevated SDR/HDR evidence
- [ ] Negative-coordinate, multi-adapter, and mixed-HDR physical evidence
- [ ] Meter-backed calibration verification
- [ ] Signed release artifacts

## v6 candidates

- Native hook/frame health telemetry
- Minimal `DllMain` plus explicit initialize/shutdown handshake
- Unelevated shell with authenticated elevated broker
- Identity-bearing native DWM output mapping
- Import-time LUT validation and metadata
- Authenticode, SBOM, and signed build provenance
- Retire the legacy GUI from the default build after v5 rollback criteria are met

Priorities and architectural rationale are in `docs/architecture-review-v5.md`. A candidate becomes
scheduled only when it has an issue/PR with its own automated and live validation story.
