param(
    [string]$DumpbinPath = 'C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\VC\\Tools\\MSVC\\14.29.30133\\bin\\HostX64\\x64\\dumpbin.exe',
    [string]$TargetDir = 'src-tauri\\target\\release',
    [string]$OutDir = 'tools\\dumpbin_artifact_outputs',
    [string]$SummaryFile = 'tools\\dumpbin_artifact_summary.txt'
)

if(-not (Test-Path $DumpbinPath)){
    Write-Error "dumpbin not found at path: $DumpbinPath"
    exit 2
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

$candidates = @()
$candidates += Get-ChildItem -Path $TargetDir -File -Recurse -Include *.exe,*.dll,*.lib -ErrorAction SilentlyContinue
$depsDir = Join-Path $TargetDir 'deps'
if(Test-Path $depsDir){
    $candidates += Get-ChildItem -Path $depsDir -File -Recurse -Include *.dll,*.lib,*.rlib -ErrorAction SilentlyContinue
}

$summary = @()

foreach($f in $candidates){
    try{
        $outPath = Join-Path $OutDir ($f.Name + '.dump.txt')
        & "$DumpbinPath" /imports "$($f.FullName)" > $outPath 2>&1
        Write-Host "Scanned: $($f.FullName) -> $outPath"

        $content = Get-Content -Raw -LiteralPath $outPath -ErrorAction SilentlyContinue
        $found = @()
        if($content -match 'comctl32\.dll'){
            $found += 'comctl32.dll'
            foreach($sym in @('SetWindowSubclass','DefSubclassProc','RemoveWindowSubclass','TaskDialogIndirect','TaskDialog')){
                if($content -match [regex]::Escape($sym)){
                    $found += $sym
                }
            }
        }
        $summary += [pscustomobject]@{ Artifact = $f.FullName; Dump = $outPath; Found = ($found -join ', ') }
    } catch {
        Write-Warning "Failed to scan $($f.FullName): $_"
        $summary += [pscustomobject]@{ Artifact = $f.FullName; Dump = ''; Found = 'error' }
    }
}

# write summary
$summary | Sort-Object Artifact | ForEach-Object { "{0}`t{1}`t{2}" -f $_.Artifact, $_.Dump, $_.Found } | Out-File -FilePath $SummaryFile -Encoding utf8
Write-Host "Wrote summary to $SummaryFile"

Write-Host 'Done'
