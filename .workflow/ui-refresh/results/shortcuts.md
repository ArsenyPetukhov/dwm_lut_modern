# Keyboard shortcuts

## Implemented

- Shortcut changes apply as a pair. Duplicate or invalid combinations are rejected before touching the active registrations. A native failure removes attempted registrations and restores the previous pair where Windows permits it.
- `Success` describes the whole transaction. `ToggleRegistered` and `CycleRegistered` describe whether each requested assignment matches what remains active. `PreviousBindingsRestored` tells the caller whether the native state from before the call is intact.
- Capture temporarily releases the active shortcuts. Existing combinations can be recorded without triggering correction. Queued shortcut messages are ignored during capture; messages for removed IDs or replaced combinations are ignored afterward.
- The dialog previews the candidate and requires Save or unmodified Enter to accept it. Escape cancels; Tab and Shift+Tab traverse the capture field, Cancel and Save. Invalid candidates disable Save. Enter or Space on a focused button keeps normal WPF behavior.
- Capture and registration share the same validation: Ctrl, Alt or Win plus a non-modifier keyboard key, or unmodified F1–F24. Plain typing, Shift-only combinations, modifier-only combinations and persisted registration-only flags are rejected.
- Key labels cover F1–F24 and use Windows key names for mapped keys, including numeric-keypad and OEM keys. Scan-code handling distinguishes navigation keys and handles Pause's E1 sequence.
- Labels and errors use the shared localization API. The dialog uses the parent's neutral theme brushes and content height, allowing translated text to wrap without the old fixed-height dialog.

## Parent integration

### Applying settings

Gate both setters on `!result.Success`, rather than the individual action flag. Restore the previous saved model on any failure. When `result.PreviousBindingsRestored` is true, do not call `Apply` again: the service has already retained or restored the previous native pair. When false, retain the failure message; a deliberate retry can attempt the previous pair again.

The service snapshots actual registered bindings, with private copies that survive changes to the caller's mutable models. After a prior incomplete recovery, that snapshot may itself be partial; `PreviousBindingsRestored` does not claim the full saved model was registered.

### Recording a shortcut

1. Call `HotkeyService.SuspendForCapture(out string error)` through the Settings view model. It returns `HotkeyCaptureLease?`.
2. If null, show the localized error and do not open the dialog. A release failure has already attempted rollback.
3. Keep the lease alive while calling `HotkeyCaptureWindow.Capture(owner)`.
4. Dispose the lease in `finally`, **before** applying a returned candidate.
5. Inspect `lease.RestoreResult` after disposal, including after Save. It is nullable before disposal and a `HotkeyRegistrationResult` afterward. Repeated Dispose calls have no further native effects.
6. On cancellation, leave the model unchanged and show any restore error. On Save, either stop on a restore failure or allow the candidate application to recover the intended full pair; surface any failure. Do not silently assume a partial restore equals the saved pair.

The service blocks nested capture and `Apply` while a lease is active. Disposing the service during capture prevents a later lease disposal from recreating registrations.

### Localization dependencies

All dialog keys exist in `.workflow/ui-refresh/english-additions.json`. In addition to its original Hotkey entries, the localization worker is providing:

- `Hotkey.UnregisterFailed`: `Could not release {0}. Try changing the shortcut again.`
- `Hotkey.CaptureInProgress`: `Finish recording the current shortcut first.`

`LocalizationService.F(key, args)` is used for native registration and removal errors. Native error codes appear only as supporting detail; the message explains that Windows or another app may own the chord.

## UI audit and refinement

Impeccable was read from `/Users/arseny/.agents/skills/impeccable/SKILL.md`, and its context command ran once for the capture dialog. The existing windows-app brief was read directly after the surface selection command returned its candidate list. The native/general audit guidance, clarify playbook, craft floor immediately before XAML edits, and Emil design engineering guidance were read. This worker proceeded from the parent's explicit approved direction without a separate design interview.

The context loader reports PRODUCT.md's WPF Platform value as unsupported and treats it as web. PRODUCT.md is outside this worker's ownership; that warning was reported to the parent. There is no WPF-specific Impeccable platform adapter, so the audit uses applicable keyboard, localization, theming and text-expansion principles without importing iOS/Android affordances.

| Before | After | Why |
| --- | --- | --- |
| P1: The first non-modifier key closed the dialog. | Candidate preview with explicit Save/Cancel and Enter confirmation. | Users can review or replace a captured chord before registering it. |
| P1: Every preview key event was handled, including Tab. | Tab/Shift+Tab navigate; focused buttons retain activation keys. | Keyboard users can reach and choose either outcome. |
| P1: A valid-looking plain letter could become a global registration. | Shared safe-chord validation and an inline corrective message. | Ordinary typing remains available to other applications. |
| P1: Re-recording an active shortcut could invoke correction. | Disposable capture lease releases/restores the actual active pair. | Recording is isolated from the global action. |
| P2: Hardcoded English labels and a fixed 230-DIP height. | Localized labels, wrapped text and height from content. | Translated instructions and errors can fit the dialog. |
| P2: Fixed F1–F12 mapping and hex labels for other keys. | F1–F24 plus Win32 key-name lookup. | The preview matches keys users can identify. |

The existing application capture at `../Research/2026-10-29-dithering/build-f870854/artifacts/ui-captures/dithering-narrow.png` was inspected as incumbent evidence. It predates the parent's neutral-world refresh and does not show this dialog; it is not post-edit visual acceptance. No decorative motion was added to this keyboard interaction.

## Verification and remaining checks

- `git diff --check` over owned tracked source/test files: clean.
- XAML parsed as XML; all seven markup localization keys were found in the English contract.
- Impeccable detector run once against both capture files: `[]`. Its web-oriented source detector does not certify WPF XAML or rendered appearance.
- Added 23 service test facts covering pair rollback, duplicate preflight, unchanged-action failure, rollback/removal failure, mutable caller models, swapping, idempotent application, stale message dispatch, capture suspension, cancellation restoration, failed capture release, failed restoration and disposal lifecycle.
- Added 35 binding-policy/display cases: three facts and 32 inline cases. These exercise the validation shared by capture and registration, the full function-key range and Windows key-name fallback.
- No .NET SDK/runtime is available in this Mac worker. **Compilation and test execution remain pending with the parent's authorized targeted Windows run.** The intended filter is `FullyQualifiedName~Hotkey` in `tests/DwmLutModern.App.Tests`.
- **Rendered WPF acceptance remains pending:** light/dark dialog, long translations, keyboard traversal and confirmation, actual RegisterHotKey conflicts, capture of the currently assigned chord, Remove, and saved-state readback. No Windows GUI was launched on this Mac.

Win32 key-name handling follows Microsoft's [MapVirtualKeyW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-mapvirtualkeyw) and [GetKeyNameTextW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getkeynametextw) documentation.

No commit or push was made. Changes are confined to the assigned shortcut service/model/dialog, `Hotkey*.cs` tests and this result file.
