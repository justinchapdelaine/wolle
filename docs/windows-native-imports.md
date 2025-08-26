Windows native imports verification

Summary

- What we checked: We ran authoritative PE import-table scans (dumpbin /imports) on the release build artifacts produced under `src-tauri/target/release` and `src-tauri/target/release/deps` to verify whether the problematic symbol `TaskDialogIndirect` or similar "common-controls" APIs were present in the default build.
- Outcome: `TaskDialogIndirect` was not present in either the tray-disabled or tray-enabled builds. The per-artifact scan shows the final EXE imports `comctl32.dll` with the symbols `SetWindowSubclass`, `DefSubclassProc`, and `RemoveWindowSubclass`.

Why this is normal for Rust + Tauri apps on Windows

- Tauri uses the `wry`/`tao` windowing stack and WebView2 on Windows. Those runtimes and the WebView2 loader commonly use Windows common-controls APIs and subclassing helpers to integrate properly with the Windows message loop and to support modern control behavior.
- The presence of `comctl32.dll` and subclassing symbols (`SetWindowSubclass`, `DefSubclassProc`, `RemoveWindowSubclass`) in the EXE import table is expected for apps that use these windowing/webview toolkits. It does not imply `TaskDialogIndirect` or the old task dialog APIs are present.

Artifacts and reproduction

- Per-artifact dump outputs: `tools/dumpbin_artifact_outputs/` (many files). The summary is at `tools/dumpbin_artifact_summary.txt`.
- Authoritative full EXE dumps used earlier: `dumpbin_disabled.txt` and `dumpbin_enabled.txt` (kept at the repo root from earlier runs).
- To reproduce locally (requires Microsoft dumpbin.exe from Visual Studio):

```powershell
# Example (adjust dumpbin path if needed):
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\MSVC\<version>\bin\HostX64\x64\dumpbin.exe' /imports src-tauri\target\release\wolle-tauri.exe
```

Decision and next steps

- Decision: We accept the `comctl32.dll` import for now. The key risk we wanted to eliminate — `TaskDialogIndirect` — is not present in default builds.
- Next steps (optional):
  - If you want to remove every reference to `comctl32.dll`, we can attempt a deeper trace to link-time object files and find which crate/object pulls it in (more work and likely to implicate `wry`/`tauri` runtime). This is generally unnecessary unless you have a strict policy against any comctl usage.
  - Otherwise, we keep the `tray` feature opt-in (as implemented) and consider this investigation complete.

Notes

- This document and the dump outputs were generated automatically by the scripts in `tools/` and retained in the repo for reproducible verification.

