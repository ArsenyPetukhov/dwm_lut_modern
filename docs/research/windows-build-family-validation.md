# Windows build-family validation without reinstalling the host

Research date: 2026-07-10. External sources are first-party Microsoft documentation. Statements marked **Project inference** apply those sources to DWM LUT Modern; Microsoft does not make those project-specific claims.

## Executive conclusion

The host does **not** need to be reinstalled for every Windows build.

- Windows Sandbox is useful for clean-machine packaging and UI smoke tests, but it is generated from the host operating system and therefore cannot become an arbitrary old or future Windows build.
- Hyper-V can maintain separate Windows 10/11 guests, checkpoints, and ISO-installed build families on one supported Pro/Enterprise host. This is the practical way to test DWM initialization across representative historical and current payload families.
- Offline code tests can prove that a copied `dwmcore.dll` has the expected exact PDB identity and that configured RVAs are structurally inside the image. They cannot prove that a private DWM function kept the same calling convention, object layout, backbuffer behavior, or frame path.
- An ordinary client Hyper-V VM uses a virtual/remoted display path. It can prove that the hook runs and an obvious diagnostic LUT changes the guest desktop, but not that correction reaches a physical HDR connector or the intended member of a real multi-monitor topology.
- Testing every cumulative build number is neither realistic nor the best unit of coverage. Test each distinct `dwmcore.dll` identity/profile family, plus a small set of lifecycle and hardware boundaries. Record exact identities so community reports can extend that matrix safely.

The proposed public stance is therefore reasonable: keep the in-app warning short, ask users to report failures, and use GitHub reports as additional compatibility evidence. Do not replace precise compatibility labels with an unqualified "works on Windows 10/11" claim.

## Why Windows Sandbox cannot be the cross-build matrix

### Sourced facts

Microsoft says Windows Sandbox's Dynamic Base Image uses the copy of Windows installed on the host. The sandbox runs the same operating-system image as the host and shares immutable OS binary pages through direct map. [Windows Sandbox architecture](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-architecture)

A `.wsb` configuration can control vGPU, networking, mapped folders, logon commands, and other redirection. With vGPU disabled the sandbox uses WARP software rendering. [Use and configure Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)

The Sandbox desktop is remoted through an Indirect Display Driver. GPU paravirtualization can provide a virtual render device, but the displayed desktop remains a remoted/virtual display path rather than a physical monitor attached to the guest. [GPU paravirtualization](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-paravirtualization#windows-sandbox)

### Project inference

Sandbox can validate the current host build's package extraction, dependency closure, first run, UI, UAC behavior, command-line handling, evidence collection, and uninstall hygiene. It cannot select Windows 10 22H2 while the host is Windows 11 25H2, and it cannot preserve a catalog of independent OS installations. Treat any successful DWM injection there as useful virtual-path evidence, not as physical-display acceptance.

## Hyper-V is the no-reinstall route for old and new families

### Sourced facts

Hyper-V can be enabled on Windows 10/11 Pro or Enterprise on hardware with the required virtualization features. [System requirements for Hyper-V](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/host-hardware-requirements)

Microsoft lists Windows 10 and Windows 11 as supported client guest operating systems. Windows 11 uses a Generation 2 VM; Windows 10 can use Generation 1 or 2. [Supported Windows guest operating systems](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/supported-windows-guest-operating-systems-for-hyper-v-on-windows), [Choose a VM generation](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/plan/should-i-create-a-generation-1-or-2-virtual-machine-in-hyper-v)

VMConnect can attach an ISO, start and shut down a VM, change settings, and create checkpoints. [Hyper-V Virtual Machine Connection](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/virtual-machine-connection)

Microsoft currently provides full-featured Windows 10 and Windows 11 Enterprise evaluation software for 90 days. The publicly offered release on an Evaluation Center page is a specific current release, not an archive of every historical fractional build. [Windows 10 Enterprise evaluation](https://www.microsoft.com/en-us/evalcenter/evaluate-windows-10-enterprise), [Windows 11 Enterprise evaluation](https://www.microsoft.com/en-us/evalcenter/evaluate-windows-11-enterprise)

Visual Studio Subscription download availability depends on subscription level. Microsoft says development-relevant products typically remain available long after support ends to enable legacy development and testing, but some products may be absent; downloads and keys require the corresponding subscription entitlement. [Product availability for Visual Studio subscriptions](https://learn.microsoft.com/en-us/visualstudio/subscriptions/product-availability), [Available software downloads](https://learn.microsoft.com/en-us/visualstudio/subscriptions/software-download-list), [Product keys](https://learn.microsoft.com/en-us/visualstudio/subscriptions/product-keys)

Microsoft's release-information pages map Windows versions to OS builds and KB releases, and the Microsoft Update Catalog publishes build-specific cumulative update packages. [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information), [Microsoft Update Catalog](https://www.catalog.update.microsoft.com/)

### Project inference

Use properly licensed/evaluation media from Microsoft or media already owned by the tester. Do not make downloading unofficial historical ISOs part of the project workflow. Where entitled media is available, install one VM per Windows foundation family, update it to the desired servicing point, and checkpoint it. A Visual Studio subscription is the cleanest official route to many older development/test downloads; the free Evaluation Center is useful but is not a complete build archive.

The Update Catalog can help reach a known cumulative-update payload from an appropriate base installation, but it does not guarantee that every arbitrary historical state is easy to reconstruct: servicing prerequisites, supersedence, and expired preview packages can intervene. Preserve a checkpoint or exported VM once a valuable exact build has been created.

## What exact-symbol and code validation proves

### Sourced facts

Microsoft's symbol server selects the symbol file matching the target module. SymStore indexes executables by timestamp/size and PDBs by signature/age. Microsoft also documents that a matching GUID is embedded in a linked binary and PDB so tools can reject a mismatched symbol file. [Symbols for Windows debugging](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/symbols), [Using SymStore](https://learn.microsoft.com/en-us/windows/win32/debug/using-symstore), [Debugging with symbols](https://learn.microsoft.com/en-us/windows/win32/dxtecharts/debugging-with-symbols)

Microsoft distinguishes full/private symbol data from stripped/public symbols. Public symbol files contain less information and can omit static functions, local variables, source lines, and type details needed to understand private implementation state. [Public and private symbols](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/public-and-private-symbols)

### Project inference

DWM LUT Modern's real compatibility key is the loaded `dwmcore.dll` CodeView PDB GUID/age, not only the marketing version or OS build label. Two labeled Windows builds that carry the same exact DWM payload/profile do not need duplicate structural qualification merely because their labels differ.

The repository's offline payload/profile test can establish:

1. The copied file is the expected architecture and exact DWM payload identity.
2. The intended compiled profile is selected rather than a profile for another binary.
3. Configured nonzero RVAs point into plausible PE image sections.
4. Unknown or mismatched payloads fail closed.

Unit and native tests can additionally establish parser behavior, interpolation/routing policy, display-topology joins, state transitions, packaging, and fail-closed behavior.

Those checks cannot execute the target DWM implementation. A structurally valid RVA does not prove the function's ABI, object graph, texture type, timing, or interaction with DirectFlip/MPO still matches the hook's assumptions. Because public PDBs omit some private implementation data, symbol availability also cannot by itself certify the hook. At least one live guest run is required for a new DWM identity before calling it VM-validated.

## Why an ordinary VM cannot certify physical color output

### Sourced facts

With GPU paravirtualization, a VM exposes a Hyper-V/Remote Display adapter and a virtual render device; UI rendering can occur on the virtual render device. [GPU paravirtualization](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-paravirtualization)

Microsoft's supported full GPU passthrough and GPU partitioning guidance targets Windows Server/server-class hardware. Microsoft explicitly states that DDA and GPU-P are not supported on desktop-class hardware or Windows 10/11 Pro hosts. [Troubleshoot Hyper-V GPU assignment and partitioning](https://learn.microsoft.com/en-us/troubleshoot/windows-server/virtualization/troubleshoot-hyper-v-gpu-assignment-partitioning-passthrough-issues), [Deploy devices with Discrete Device Assignment](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/plan/plan-for-deploying-devices-using-discrete-device-assignment)

### Project inference

A normal Windows-client Hyper-V test can establish that DWM accepts the DLL, mandatory hooks initialize, the watchdog recovers, disable/unload works, and an intentionally obvious LUT changes pixels in the guest's virtual desktop. This is strong evidence for the DWM payload family.

It cannot establish physical EDID/connector routing, real panel HDR signaling, vendor-driver scanout behavior, multi-GPU ownership, or measured calibration accuracy. Those remain small, targeted physical tests. Buying server GPU-passthrough hardware only to avoid those tests would add cost and complexity without eliminating the need to test the actual consumer display topology.

## Recommended representative matrix

Validate by **DWM payload family and behavior boundary**, not every UBR:

| Lane | Minimum representative | Evidence goal |
|---|---|---|
| Automated/offline | Every compiled `dwmcore.dll` GUID/age profile and every fallback engine | Exact selection, RVA/image sanity, parsers, routing, fail-closed behavior |
| Windows 10 VM | Current supported 19045/22H2 servicing point used by the project | Windows 10 fallback/profile runtime, install, apply/disable, restart |
| Windows 11 24H2 VM | One 26100 payload representative | Legacy modern-DWM fallback/runtime boundary |
| Windows 11 25H2/current VM | One current stable exact profile | Primary supported flow, recovery, install/upgrade/uninstall |
| Insider VM | One VM for each **distinct** compiled DWM PDB identity, not every channel label sharing it | New-profile probe, obvious LUT, crash/recovery evidence |
| Physical smoke | One supported stable build on a real SDR/HDR system | Real driver/connector transition and obvious LUT |
| Physical multi-monitor | Two displays with different obvious LUTs, mixed SDR/HDR if available | No cross-monitor or cross-domain application; unmatched contexts fail closed |
| Meter acceptance | One reference GPU/display workflow per release family | Quantitative before/after calibration evidence |

When a cumulative update changes the `dwmcore.dll` GUID/age, it creates a new payload candidate even if the Windows version name is unchanged. When two OS labels share the same exact identity and hook profile, one live run can qualify the shared implementation provisionally, with the labels still recorded separately in the compatibility matrix.

## User-facing warning and community validation policy

The release UI does not need a wall of warnings. A compact message is appropriate:

> DWM compatibility varies by Windows update. If correction does not apply or behaves incorrectly, disable it and report the issue to the developer.

Keep the detailed boundaries in the compatibility matrix, validation guide, and issue template. Public statuses should remain distinct:

- **Built**: profile compiles and offline checks pass.
- **VM-validated**: exact payload ran, applied an obvious LUT, disabled, and recovered in a VM.
- **Physical-validated**: exact payload passed a real GPU/display smoke test.
- **Measured**: instrumented before/after verification passed.
- **Community-reported**: sanitized evidence from another system; keep separate from maintainer validation until reproduced.

An issue should capture OS build/UBR, `dwmcore.dll` version/hash/PDB identity, GPU and driver, monitor topology, HDR state, package hash, requested LUT/domain, result, and sanitized logs. Multiple independent matching reports can raise confidence, but lack of reports is not proof of compatibility. A crash, wrong-monitor application, or inability to disable should immediately downgrade that profile and trigger a fail-closed release response.

## Practical decision

Do not reinstall the developer workstation ten times. Maintain a small Hyper-V library and expand it only when the DWM payload identity or a meaningful graphics foundation changes:

1. Obtain entitled Microsoft media for the selected Windows family.
2. Install a Generation 2 VM and use the Basic/console VMConnect session for DWM tests.
3. Update to the target build, record `winver` and the exact `dwmcore.dll` identity, then create/export a clean checkpoint.
4. Run the automated evidence collector and the diagnostic identity/obvious LUT sequence.
5. Preserve the VM/checkpoint only for distinct payload identities or release-family boundaries.
6. Use a small number of real-machine tests for HDR, connectors, multi-monitor routing, and measurement.
7. Let community reports broaden the hardware/build matrix while retaining truthful evidence labels.

This yields substantially better coverage than assuming all profiles work from code alone, without turning compatibility work into repeated host reinstalls.
