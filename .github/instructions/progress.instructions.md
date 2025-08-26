## Wolle — Progress (auto-generated)

```instructions
## Wolle — Progress (auto-generated)

Date: 2025-08-25

### Summary
This document tracks the current progress of the Wolle desktop utility (Tauri + Vite + Ollama). It is auto-updated by the development assistant to reflect recent work: fixes, builds, runtime checks, and outstanding issues.

---

### Done
- Implemented Phase 1 MVP: Tauri backend commands (`health_check`, `run_action`) and a Vite frontend skeleton.
- Added `tools/webview2-preflight.ps1` to fetch/copy `WebView2Loader.dll` for developer machines.
- Added unit tests and CI for the frontend; non-tray builds pass in CI.
- Implemented tray support behind a crate feature named `tray` (uses optional external crate for tray operations).
- Fixed Tauri `run` usage in `src-tauri/src/main.rs` (build path uses `.build(...).run(...)`) and refactored tray updates to use an mpsc channel to ensure TrayIcon is updated on the main thread.

### Additional diagnostics completed
- Ran Process Monitor (ProcMon) capture during process start and analyzed DLL load/open events.
- Ran `dumpbin` on the built executable and on system `comctl32.dll` exports to correlate imports/exports.

### Current work (in-progress)
- Building and running the tray-enabled app locally (`cargo build --features tray` succeeded).
- Running and verifying the app with tray support to observe tray creation and background Ollama polling.
- Investigating a native loader-time crash (STATUS_ENTRYPOINT_NOT_FOUND) that prevents the exe from starting in debug runs.

### Blocker / Issue
- When attempting to run the tray-enabled executable, the process exits immediately with Windows error STATUS_ENTRYPOINT_NOT_FOUND (0xC0000139). Symptoms:
	- The build completes successfully (warnings only).
	- Running `cargo run --features tray` results in the executable exiting with the above error.
	- A local copy of `WebView2Loader.dll` was created from the NuGet package during preflight as a developer workaround; we removed it during diagnostics to force use of the system runtime.

### Key diagnostic finding (ProcMon + dumpbin)
- ProcMon capture (provided by developer) shows the process loading `comctl32.dll` from the system/WinSxS locations and then exiting with STATUS_ENTRYPOINT_NOT_FOUND.
- `dumpbin /IMPORTS` on `wolle-tauri.exe` shows the executable statically imports `TaskDialogIndirect` from `comctl32.dll`.
- `dumpbin /EXPORTS` against the local `C:\Windows\System32\comctl32.dll` on the machine did NOT show `TaskDialogIndirect` in the export table (i.e., the system DLL observed by the loader lacks that export in this environment).

This combination explains the immediate ENTRYPOINT error: the exe expects `TaskDialogIndirect` but the resolved `comctl32.dll` does not provide it, so Windows fails process startup with STATUS_ENTRYPOINT_NOT_FOUND.

### Root cause hypotheses (prioritized)
- System `comctl32.dll` in use is an older/stripped/corrupt version that does not export `TaskDialogIndirect`. This most directly matches the procmon + dumpbin evidence.
- Running in Safe Mode or a minimal/repair environment caused the OS to load a reduced set of system libs that lack the symbol.
- A mismatched DLL (wrong bitness or side-by-side replacement) was loaded instead of the expected system comctl32; however ProcMon shows the loader used the WinSxS/System32 path.

Note: while WebView2 loader / architecture mismatches were a plausible earlier hypothesis, the immediate failure is specifically a missing `TaskDialogIndirect` import from `comctl32.dll` when the process starts.

### Actions taken to unblock
- Ran `tools/webview2-preflight.ps1` to extract `WebView2Loader.dll` from NuGet and copy it to `target/debug` for developers (a temporary dev workaround).
- Attempted to run the system WebView2 installer from the repo session; the installer required admin elevation. The user ran the installer as Admin locally and confirmed x64 runtime installation.
- Removed the local `WebView2Loader.dll` to allow the system-installed runtime to be used.
- Captured Process Monitor trace during exe startup and inspected the sequence of Load Image/CreateFile events.
- Ran `dumpbin /IMPORTS` against the exe and `dumpbin /EXPORTS` against system `comctl32.dll` to validate the missing export.

### Next steps (planned)
1. Immediate: reboot into normal Windows mode if currently in Safe Mode and re-test. Safe Mode can load a reduced set of system libraries which may lack `TaskDialogIndirect`.
2. If not in Safe Mode, run System File Checker (SFC) and DISM to repair system DLLs (admin):
   - `sfc /scannow`
   - `DISM /Online /Cleanup-Image /RestoreHealth` then `sfc /scannow` and reboot.
3. If SFC/DISM do not restore `TaskDialogIndirect`, collect the following and escalate:
   - The ProcMon CSV lines showing Load Image/CreateFile for `comctl32.dll` and any non-SYSTEM path used.
   - The output of `dumpbin /EXPORTS C:\Windows\System32\comctl32.dll` for direct inspection.
4. As a development workaround, consider changing code that forces a static import of `TaskDialogIndirect` to use LoadLibrary/GetProcAddress with a graceful fallback (MessageBox) so the app can start on systems missing the newer API. This requires locating the crate that introduces the `comctl32` import and updating it.
5. After the system DLL issue is resolved and the app runs, finish the remaining tasks: tray verification, Ollama polling validation, minor code cleanups, and installer work.

---

If anything here is incorrect or you want more detail in any section, update this file or ask the dev assistant to expand a section.
```
