param(
  [string]$Manifest = 'src-tauri/Cargo.toml'
)

Write-Host "[rust-udeps] Installing cargo-udeps if missing..."
if (-not (Get-Command cargo-udeps -ErrorAction SilentlyContinue)) {
  cargo install cargo-udeps --locked
  if ($LASTEXITCODE -ne 0) { throw "Failed to install cargo-udeps" }
}

Write-Host "[rust-udeps] Ensuring nightly toolchain is available..."
if (-not (Get-Command rustup -ErrorAction SilentlyContinue)) {
  throw "rustup not found. Install Rust from https://rustup.rs/ to proceed."
}

# Check if nightly is available; if not, install it (minimal profile to keep it light)
# Capture output explicitly so we don't hide error information entirely.
$nightlyCheck = & cargo +nightly -V 2>&1
if ($LASTEXITCODE -ne 0) {
  Write-Host "[rust-udeps] Nightly toolchain not found; installing..."
  rustup toolchain install nightly --profile minimal
  if ($LASTEXITCODE -ne 0) { throw "Failed to install nightly toolchain" }
}

Write-Host "[rust-udeps] Running cargo +nightly udeps on $Manifest"
cargo +nightly udeps --manifest-path $Manifest
