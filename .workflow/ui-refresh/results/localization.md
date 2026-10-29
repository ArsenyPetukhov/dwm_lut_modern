# Localization implementation

## Delivered

- Replaced the hardcoded Add/Er table with complete embedded JSON catalogs for `en`, `ru`, `es`, `fr`, `de`, `pt`, `it`, `zh`, `ja`, and `ko`.
- Each language has **235 keys**: **2,350 localized values** total. All **74 original keys** remain. All **113 authoritative English additions/revisions** match `.workflow/ui-refresh/english-additions.json` exactly.
- Translated every long help paragraph and all eight documentation bodies. Supported languages do not use an English fallback.
- Retained `Languages`, `Current`, `SetLanguage`, and `T`. Added `F(string key, params object?[] args)`, `LanguageChanged`, and a stable `Source` with a string indexer and `INotifyPropertyChanged`. Language changes raise `Binding.IndexerName` (`Item[]`) before notifying subscribers.
- `LanguageOption.ToString()` returns the native language name for accessible combo-box items.
- Empty language settings follow `CultureInfo.CurrentUICulture` when supported. Regional codes normalize to one of the existing ten languages; unsupported languages use English.
- `TranslateMessage` maps exact English catalog values, old English revisions, legacy status labels, and the two known incomplete-disable messages. Unknown system/diagnostic text remains unchanged.
- Resource loading is safe before window startup; a missing or unreadable resource is logged to Debug and does not throw from static initialization. An unknown key returns its key so missing coverage remains visible.

## Integration contract

The app project must embed `Localization/*.json` with resource names `DwmLutModern.Localization.{code}.json`. The root worker owns that project-file change and all XAML, binding, view-model, and runtime-caller changes.

The copy uses the established product language and neutral technical wording. Technical identifiers, paths, numeric values, units, composite-format placeholders, and file-filter masks are preserved. The revised English source controls technical claims, including the narrow playback confirmation and the lack of measured laptop performance.

## Bounded checks

The final inline Python checks loaded all ten JSON files and checked:

| Check | Result |
|---|---|
| Identical complete key sets | 235 per language |
| Duplicate or blank entries | 0 |
| Composite-format placeholder mismatches | 0 |
| Authoritative English changes | 0 |
| Original keys missing | 0 |
| Technical identifier/path mismatches | 0 |
| Changed file-picker masks | 0 |
| Literal UI keys missing at handoff | 0 of 151 found references |
| Long English help/documentation copied into other languages | 0 |

The Impeccable detector returned `[]` for the changed service/catalog targets. These are C#/JSON files in a WPF app; this result is not a rendered-layout or native-platform acceptance check. No broad suite or Windows runtime was run by this worker.

### Identical values reviewed

All languages intentionally retain `Lut.ToSdr` (`→ SDR`), `Lut.ToHdr` (`→ HDR`), and `Monitor.AssignedLut` (`{0}: {1}`). These are technical labels or a value-only format.

Other exact English matches are valid words shared by the languages:

- German: `Theme.System`, `Common.Version`, `Common.Status`.
- French: `Nav.Documentation`, `Docs.Title`, `Common.Version`.
- Portuguese: `Common.Status`.
- Spanish and Portuguese: `Compat.Canary` (`Canary 29617 (experimental)`).

No suspicious unchanged English sentences remain in the catalogs.

## Runtime-string handoff inventory

Exact-match translation cannot recover variables from concatenated English strings. The following catalog keys are available for caller conversion; the root worker has been notified and owns the remaining integration:

- `DwmProfileService`: `Dwm.LiveValidated`, `Dwm.ExactProfile`, `Dwm.NoProfileFallback`, and `Dwm.NoProfileUnsupported` accept the profile identity/build details. Root conversion was visible during the last read.
- `InjectionService`: `Runtime.NativeMissing`, `Runtime.ApplyFailed`, `Runtime.ApplyHint`, `Runtime.ApplyProfileHint`, and `Runtime.DisableFailed` keep paths and diagnostics as structured parameters. All fixed operation messages also have `Runtime.*` keys.
- `LutConverter`: `Runtime.UnsupportedFormat`, `Runtime.UnsupportedTxt`, and `Runtime.UnsupportedTxtRow` accept the format, row counts, or row index.
- `UpdateService`: all `Update.*` keys are present, including HTTP status and installed/available version formats. Root is converting callers.
- Onboarding: use `Lut.FileFilter`, `Runtime.SetupChooseDisplay`, `Runtime.SetupDisplayChanged`, `Runtime.SetupFailed`, and `Runtime.StartupChangeFailed` for the equivalent raw strings.
- `App.xaml.cs`: the startup-change toast has an equivalent key. Use `Runtime.StartupRepairFailed`, `Runtime.UnexpectedError`, and `Runtime.FatalStartup` for the startup-repair toast, unexpected-error toast, and fatal-startup dialog. All three were added and translated in the final pass; root owns conversion of the callers.
- `SingleInstanceCommandChannel` and `CliOptions` contain CLI/IPC and diagnostic English. Unknown OS exception messages, PE-header details, native API names/codes, and logs intentionally remain raw under the task boundary.

Transient strings already stored in a view model need to be recomputed or cleared on `LanguageChanged`; the binding source refreshes localized bindings but cannot re-create a previously formatted view-model error. Root owns that behavior.

## Impeccable workflow and proof boundary

Read `/Users/arseny/.agents/skills/impeccable/SKILL.md`, ran `impeccable context` once with the target project as cwd, read the Windows surface brief by slug, and used the clarify and craft-floor references before edits. Context loading succeeded but warned that the WPF platform text is unrecognized and treated it as web. Product/surface context was read directly; no context files were changed. This scoped subagent used the explicit parent brief for direction.

Applied the requested Humanizer source-prose guidance from https://raw.githubusercontent.com/blader/humanizer/main/SKILL.md. Windows rendering, font coverage, minimum-width layout, live controls and DWM playback remain the lead agent’s verification responsibility. This worker did not claim a visual or playback pass, commit, or push.
