param(
  [Parameter(Mandatory=$true, Position=0)]
  [ValidateSet('files','images')]
  [string]$Kind,

  [Parameter(Mandatory=$true, Position=1)]
  [string[]]$Paths,

  [Parameter(Mandatory=$false)]
  [int]$X = 800,

  [Parameter(Mandatory=$false)]
  [int]$Y = 500,

  [switch]$DebugBuild,

  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Resolve-Exe {
  param([switch]$DebugBuild)
  $repoRoot = (Resolve-Path "$PSScriptRoot\..\").Path
  $debugExe = Join-Path $repoRoot 'src-tauri\target\debug\wolle-tauri.exe'
  $releaseExe = Join-Path $repoRoot 'src-tauri\target\release\wolle-tauri.exe'
  if ($DebugBuild) {
    if (Test-Path $debugExe) { return $debugExe }
    throw "Debug exe not found: $debugExe"
  } else {
    if (Test-Path $releaseExe) { return $releaseExe }
    if (Test-Path $debugExe) { return $debugExe }
    throw "No built executable found. Build first (npm run tauri:build or tauri build)."
  }
}

# Normalize and validate paths
$normPaths = @()
foreach ($p in $Paths) {
  $full = Resolve-Path -Path $p -ErrorAction Stop | Select-Object -ExpandProperty Path
  $normPaths += $full
}

# Build payload object
$payload = @{ kind = $Kind; coords = @{ x = $X; y = $Y } }
if ($Kind -eq 'files') {
  $payload.files = $normPaths
} else {
  $payload.images = $normPaths
}

# Serialize JSON and write to a temp file so we can pass an @file arg reliably on Windows
$json = $payload | ConvertTo-Json -Compress
$tempDir = [System.IO.Path]::GetTempPath()
$jsonPath = Join-Path $tempDir ("wolle-payload-" + [Guid]::NewGuid().ToString() + ".json")
# Write as UTF-8 without BOM to avoid BOM-related parse failures (PowerShell 5-compatible syntax)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($jsonPath, $json, $utf8NoBom)

Write-Host "Payload file:" -ForegroundColor Cyan
Write-Host $jsonPath
Write-Host $json

if ($DryRun) {
  Write-Host "DryRun: not launching executable." -ForegroundColor Yellow
  exit 0
}

$exe = Resolve-Exe -DebugBuild:$DebugBuild
Write-Host "Launching: $exe" -ForegroundColor Green
# Pass the @file reference to avoid quoting issues
& $exe ("@" + $jsonPath)
$code = $LASTEXITCODE
if ($null -ne $code -and $code -ne 0) {
  throw "Executable exited with code $code"
}
