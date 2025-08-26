<#
Runs two release builds (with and without the local `tray` feature)
and checks the produced EXEs for signs of common-controls / TaskDialogIndirect.

Behaviour:
- If `dumpbin.exe` is available on PATH, it will use `dumpbin /imports` to list imported DLLs
  and search that output for COMCTL32 or TaskDialogIndirect.
- Otherwise it will perform an ASCII substring search of the binary as a fallback.

Run from repository root or anywhere; the script changes directory to `src-tauri`.
#>

Set-StrictMode -Version Latest

function Get-DumpbinCommand {
    $cmd = Get-Command dumpbin.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return $null
}

function Invoke-CheckExe($exePath) {
    if (-not (Test-Path $exePath)) {
        Write-Output "MISSING: $exePath"
        return
    }

    $dumpbin = Get-DumpbinCommand
    if ($dumpbin) {
        Write-Output "Using dumpbin at: $dumpbin"
        $out = & $dumpbin /imports $exePath 2>&1
        if ($LASTEXITCODE -ne 0) { Write-Output "dumpbin failed: $out" }
        $foundComctl = $out -match 'COMCTL32' -or $out -match 'ComCtl32'
        $foundTaskDialog = $out -match 'TaskDialogIndirect' -or $out -match 'TaskDialog'
        Write-Output "Imports: COMCTL32: $foundComctl, TaskDialog*: $foundTaskDialog"
    } else {
        Write-Output "dumpbin not found on PATH; doing ASCII substring fallback scan"
        $bytes = Get-Content -Path $exePath -Encoding Byte -ReadCount 0
        $s = [System.Text.Encoding]::ASCII.GetString($bytes)
        foreach ($pat in @('COMCTL32','ComCtl32','TaskDialogIndirect','TaskDialog','common-controls','commoncontrols')) {
            if ($s -match [regex]::Escape($pat)) { Write-Output ($pat + ': FOUND') } else { Write-Output ($pat + ': not found') }
        }
    }
}

Push-Location -Path (Join-Path $PSScriptRoot '..\src-tauri')
try {
    Write-Output "Building release (tray DISABLED)..."
    cargo build --release
    if ($LASTEXITCODE -ne 0) { throw "Release build (no tray) failed" }
    $exe = Join-Path (Get-Location) 'target\release\wolle-tauri.exe'
    Write-Output "--- Checking release (tray DISABLED) ---"
    Invoke-CheckExe $exe

    Write-Output "\nBuilding release (tray ENABLED)..."
    cargo build --release --features tray
    if ($LASTEXITCODE -ne 0) { throw "Release build (tray) failed" }
    $exe2 = Join-Path (Get-Location) 'target\release\wolle-tauri.exe'
    Write-Output "--- Checking release (tray ENABLED) ---"
    Invoke-CheckExe $exe2
} finally {
    Pop-Location
}

Write-Output "Done."
