# Wolle (MVP scaffold)

This repository contains a minimal scaffold for the Wolle MVP: a Tauri + Rust backend and a Vite frontend.

How to run (dev):

1. Install Node and Rust toolchain.
2. From the repo root:

```powershell
npm install
cargo build --manifest-path src-tauri/Cargo.toml
npm run tauri:dev
```

Notes:

- This is a minimal scaffold for Phase 1. It includes a simple Ollama helper that attempts an HTTP health check at http://127.0.0.1:11434 and falls back to checking the `ollama` CLI.
- For the full PRD features (context menus, installer, model provisioning, robust positioning), further work is required.

## WebView2 preflight

This repo includes a PowerShell preflight script `tools/webview2-preflight.ps1` which checks for the WebView2 runtime and will download the `Microsoft.Web.WebView2` NuGet package (version 1.0.3405.78) and extract a matching `WebView2Loader.dll` into `target\debug` for dev runs if a system x64 runtime is not detected.

Run the preflight script before `npm run tauri:dev` when testing on a machine where WebView2 may be missing:

```powershell
npm run preflight:webview2
npm run tauri:dev
```

For installer usage, prefer installing the Evergreen WebView2 runtime system-wide; as a fallback the installer can bundle a Fixed Version WebView2 redistributable and copy `WebView2Loader.dll` into the app folder during install.

# wolle

It's a bunch of wolle

## Quick check for tray-related imports

There's a small helper script at `tools/check_tray_imports.ps1` that builds both release variants (tray enabled/disabled) and scans the produced EXEs for common-controls / TaskDialog imports. Run it from PowerShell in the repo root:

```powershell
tools\check_tray_imports.ps1
```

If `dumpbin.exe` is available on your PATH the script will use it to list imported DLLs; otherwise it falls back to an ASCII substring scan of the EXE.

## Dependency updates

This repo uses two mechanisms to keep dependencies fresh and safe:

- Automated update PRs via Dependabot
	- Configured in `.github/dependabot.yml`
	- Covers: npm (root), Cargo (src-tauri), and GitHub Actions
	- Runs weekly (Mondays, UTC) with PR labels and limits to avoid noise
- Continuous Integration checks
	- Workflow at `.github/workflows/ci.yml`
	- On each PR and push to `main`, CI runs: Prettier check, ESLint, Vitest, Vite build, and Rust unit tests

Typical flow: Dependabot opens a PR → CI validates the change → you review/merge. If you prefer more/less frequent updates or grouping (e.g., group all Vite/Vitest bumps), adjust `.github/dependabot.yml` accordingly.
