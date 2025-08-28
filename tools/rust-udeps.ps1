param(
  [string]$Manifest = 'src-tauri/Cargo.toml'
)

Write-Host "[rust-udeps] Installing cargo-udeps if missing..."
if (-not (Get-Command cargo-udeps -ErrorAction SilentlyContinue)) {
  cargo install cargo-udeps --locked
  if ($LASTEXITCODE -ne 0) { throw "Failed to install cargo-udeps" }
}

Write-Host "[rust-udeps] Running cargo udeps on $Manifest"
$env:RUSTFLAGS="-Awarnings"
cargo udeps --manifest-path $Manifest
