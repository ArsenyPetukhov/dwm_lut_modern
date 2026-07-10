# Architecture review: v5

## Executive assessment

The v5 control plane is substantially safer than the inherited fork: desired and observed state
are separate, every control surface serializes through one controller, state writes are atomic,
display persistence is adapter-aware, partial multi-DWM injection rolls back, and native LUT
routing fails closed. The application is suitable for serious experimental use, but it is not yet
a conventional production color-management component because it still hooks private DWM internals
inside an always-elevated process chain.

## What is fixed now

- Disabled intent survives restart, wake, unlock, display changes, and startup.
- A cold owner process and a restarted DWM receive bounded recovery while desired state is Enabled.
- Disable is not reported successful until the DLL is verified absent.
- Only one process owns state/injection; later GUI and CLI commands use a current-user pipe.
- Persistent monitor identity is device path first, adapter LUID + target ID second.
- The complete connected topology is staged even when only one output has a LUT.
- Multi-monitor native selection requires exact position and signal domain; duplicates fail closed.
- SDR and HDR never fall back across domains.
- Failed advanced-color telemetry is now `unknown`, not falsely shown as SDR.
- Test/screenshot instances have their own singleton scope and cannot collide with the elevated app.
- Self-contained ZIP/MSI packaging validates that managed/native architectures match.

## Remaining architectural debt

| Priority | Decision/debt | Risk | Current mitigation | Recommended seam |
|---|---|---|---|---|
| P0 | Native initialization and teardown perform substantial work in `DllMain` | Loader-lock deadlock/reentrancy risk inside DWM | Synchronous load gives the injector a simple success signal | Minimal `DllMain`; exported initialize/shutdown entry points plus explicit remote-call handshake |
| P0 | The whole WPF UI runs elevated | Larger privileged attack surface; every file picker/link/parser is admin | Strict CLI, fixed app paths, test-only state override excluded from production | Unelevated UI + small signed elevated broker/service with authenticated IPC |
| P0 | Private DWM functions are pattern/RVA hooked | Cumulative updates can crash or silently stop correction | Exact PDB identities, architecture gates, rollback, live-validation labels | Per-payload compatibility plugin/adapter with startup self-test and kill switch |
| P1 | Native runtime display routing uses exact desktop coordinates | Coordinate drift can produce a missed correction after topology changes | Wrong LUTs are forbidden; unmatched outputs receive none | Discover an identity-bearing DWM output object and map adapter LUID/target ID end-to-end |
| P1 | `Active` means module loaded, not “frames are being transformed” | False confidence if the hook never observes a compatible surface | Native init fails when mandatory hooks/LUTs fail; logs expose selection | Versioned health shared-memory/pipe contract with hook count, selected display/domain, last-frame timestamp |
| P1 | `InjectionService` owns privilege, staging, PE checks, process enumeration, injection, rollback, logging | Hard to reason about/test; changes have wide blast radius | `IInjectionService` boundary and controller tests | Split into privilege broker, package/staging transaction, process injector, status probe |
| P1 | Multiple `dwm.exe` processes are treated as one transaction | Session/secure-desktop behavior can vary | All-or-nothing apply and rollback | Make session ID explicit; report per-process state and define supported session ownership |
| P2 | Modern and legacy GUIs duplicate injector/profile logic | Fixes can diverge | Legacy package is explicitly rollback-only | Freeze legacy code, remove it from default solution after v5 physical validation |
| P2 | Update/download artifacts are not code-signed | Users cannot establish publisher trust | SHA-256 files and GitHub provenance | Authenticode-sign EXE/DLL/MSI; publish SBOM and signed provenance/attestations |
| P2 | LUT validation is deferred until apply | A bad library item remains visible until used | Apply returns a structured failure and never accepts partial injection | Parse/validate on import; cache metadata and validation status in the library model |

## Multi-monitor status

The catastrophic behavior is fixed; the complete feature is not finished.

```text
Persistent assignment: monitor device path → adapter LUID/target → SDR/HDR LUTs
                                      ↓ live topology refresh
Native runtime today: exact (desktop X, desktop Y, signal domain) → LUT
                                      ↓ if no unique match
                                  apply nothing
```

This means rearrangement/reconnect is safe at the control-plane level, and a native ambiguity cannot
bleed one monitor's calibration onto another. It also means a private-DWM coordinate mismatch can
leave the correct monitor uncorrected. Closing that availability gap without reintroducing wrong-LUT
risk is the top v6 display task.

## Recommended v6 tracer bullets

1. Add native health telemetry without changing hook selection; show `loaded`, `hooked`, and
   `transforming` separately.
2. Extract explicit native `Initialize`/`Shutdown` exports and keep the old injector behind a
   compatibility switch until the handshake is proven.
3. Introduce an unelevated shell and one broker operation (`GetStatus`) before moving Apply/Disable.
4. Capture identity-bearing DWM output research on one verified profile; do not generalize until two
   payload families and a multi-adapter topology pass.
5. Add import-time LUT validation and metadata as an independent user-facing improvement.
6. Sign CI artifacts and publish a software bill of materials before calling v6 production-ready.

Each tracer bullet should preserve the current fail-closed policy and add its own automated and live
evidence before the old path is removed.
