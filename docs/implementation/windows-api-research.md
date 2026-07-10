# Windows API Research and Engineering Decisions

This document records primary-source guidance used by the v5 implementation so future work does
not have to rediscover the reasoning.

## Display identity and topology

Microsoft defines a display path as a source/target pair. A target ID is meaningful on the adapter
identified by `DISPLAYCONFIG_PATH_TARGET_INFO.adapterId`; it is not a globally stable monitor key.

- [`DISPLAYCONFIG_PATH_INFO`](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_info)
- [`DISPLAYCONFIG_PATH_TARGET_INFO`](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info)
- [`QueryDisplayConfig`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)

`QueryDisplayConfig` can return `ERROR_INSUFFICIENT_BUFFER` when topology changes between sizing
and querying. Microsoft directs callers to repeat the sizing call and retry. The modern monitor
provider therefore must retain adapter LUIDs and use a bounded retry loop.

Decision: persistent identity starts with monitor device path; adapter LUID + target ID identifies
the current display-config target; source ID and desktop coordinates are transient metadata and
must never independently claim an existing calibration assignment.

## Single-instance command control

.NET named pipes provide local duplex inter-process communication and support asynchronous
request/response operation:

- [`NamedPipeServerStream`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream?view=net-8.0)
- [Pipe operations in .NET](https://learn.microsoft.com/en-us/dotnet/standard/io/pipe-operations)

Decision: the interactive owner hosts a per-user pipe. Secondary launches send a serialized
command and receive a serialized result. They do not independently load, mutate, and save state.

## Global hotkeys

`RegisterHotKey` returns zero on failure and requires `GetLastError` for extended information.
Windows-logo combinations may be reserved by the operating system:

- [`RegisterHotKey`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)

Decision: registration returns a result for each binding. Failed bindings are not persisted as
active and the UI displays the Win32 error. `MOD_NOREPEAT` is used for user actions.

## Autostart

Task Scheduler supports create-or-update registration and interactive-token logon tasks:

- [`TaskFolder.RegisterTaskDefinition`](https://learn.microsoft.com/en-us/windows/win32/taskschd/taskfolder-registertaskdefinition)

Decision: task mutation is followed by a query/verification step. App state changes only after the
desired task state is confirmed. The registered executable path is surfaced diagnostically so a
moved portable folder can be repaired rather than failing silently.

## Installer format

Microsoft notes that apps which always require elevation and load modules into external processes
are poor fits for normal MSIX isolation/Store distribution. Unpackaged Win32 apps can use MSI/EXE
installers:

- [Prepare a desktop app for MSIX](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-prepare)
- [Windows packaging overview](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)
- [WiX Toolset documentation](https://docs.firegiant.com/wix/)

Decision: ship portable ZIP plus a conventional WiX MSI/EXE path. Do not represent MSIX/Store as
the primary distribution route for an elevated DWM injector.

The installer is pinned to WiX Toolset 6.0.2. WiX v7 requires explicit acceptance of its OSMF
EULA during build (`WIX7015`); automated builds must not accept a legal agreement on a maintainer's
behalf. Any future v7 migration is a deliberate maintainer/legal decision, not an automatic update.

## Immediate desktop redraw

`RedrawWindow` can invalidate and synchronously update a window tree with `RDW_UPDATENOW`, while
`DwmFlush` waits for the calling process's queued DirectX surface changes:

- [`RedrawWindow`](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow)
- [`DwmFlush`](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)

Decision: use desktop invalidation plus `DwmFlush` after a verified changed operation. Do not create
a temporary transparent top-most WPF window, which adds window creation/composition latency.
