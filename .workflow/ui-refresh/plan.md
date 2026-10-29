# Neutral glass UI refresh

Goal: explain the rendering model plainly, replace the cockpit styling with the
user's white/dark-gray glass direction, complete translations, fix shortcut
registration, and deliver a compiled preview with rendered Windows evidence.

Ownership:
- Parent: WPF shell/pages/themes, documentation, viewmodels, build/capture pipeline.
- Icon producer: generated PNG/ICO assets only (complete; parent review pending).
- Shortcut worker: HotkeyService, HotkeyBinding, HotkeyCaptureWindow and focused tests.
- Translation worker: LocalizationService and Localization/*.json only.

The human explicitly authorized the two implementation workers. Shared-tree
changes must be staged by scope. Preserve native LUT rendering from v5.0.5.
Review real Windows captures in both themes; verify language key coverage and
shortcut transactions. No claim of GPU playback acceptance on this Mac.

Copy passes: remove slogans/filler; check every technical claim against source;
then review phrasing and translations for clarity and consistency. Independent
Impeccable finish review and design documentation follow rendered inspection.
