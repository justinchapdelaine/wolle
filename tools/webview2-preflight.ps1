param(
  [string]$TargetDir = "$PSScriptRoot\..\target\debug",
  [string]$NugetVersion = "1.0.3405.78"
)

Set-StrictMode -Version Latest

function Write-Log($m) { Write-Host "[webview2-preflight] $m" }

$x64Path = 'C:\Program Files\Microsoft\EdgeWebView\Application'
$x86Path = 'C:\Program Files (x86)\Microsoft\EdgeWebView\Application'

if (Test-Path "$x64Path") {
  Write-Log "Found WebView2 runtime in Program Files (x64). Using system runtime."
  exit 0
}

if (Test-Path "$x86Path") {
  Write-Log "Found WebView2 runtime in Program Files (x86). Checking for loader..."
  # Continue: loader may still be missing, we'll attempt to provide WebView2Loader.dll for dev
}

Write-Log "WebView2 runtime not found in Program Files x64; preparing to fetch WebView2Loader.dll from NuGet package v$NugetVersion"

if (-not (Test-Path $TargetDir)) {
  Write-Log "Creating target dir: $TargetDir"
  New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

$nugetUrl = "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2/$NugetVersion"
$tmp = Join-Path $env:TEMP "webview2_nuget_$NugetVersion.zip"

Write-Log "Downloading $nugetUrl to $tmp"
Invoke-WebRequest -Uri $nugetUrl -OutFile $tmp -UseBasicParsing


Write-Log "Extracting WebView2Loader.dll (win-x64) from package"
$extractDir = Join-Path $env:TEMP "webview2_nupkg"
if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
Expand-Archive -Path $tmp -DestinationPath $extractDir -Force

$search = Get-ChildItem $extractDir -Recurse -Filter 'WebView2Loader.dll' -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match 'win-x64|x64|x86_64' } | Select-Object -First 1

if ($null -eq $search) {
  Write-Log "Could not find WebView2Loader.dll in extracted package. Listing extracted folders:"
  Get-ChildItem "$env:TEMP\webview2_nupkg" -Recurse -Directory | Select-Object -First 20 | ForEach-Object { Write-Log $_.FullName }
  exit 2
}

$loaderSource = $search.FullName
$loaderTarget = Join-Path $TargetDir 'WebView2Loader.dll'
Copy-Item -Path $loaderSource -Destination $loaderTarget -Force

if (Test-Path $loaderTarget) {
  Write-Log "Copied WebView2Loader.dll to $loaderTarget"
  exit 0
} else {
  Write-Log "Failed to copy WebView2Loader.dll"
  exit 3
}
