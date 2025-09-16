# wolle Installer Script

This PowerShell script creates an installer for the wolle application.

```powershell
# Build the application first
dotnet publish -c Release -r win-x64 --self-contained true

# Create installer directory
New-Item -ItemType Directory -Force -Path "installer"
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\*" -Destination "installer" -Recurse

# Create installer script
$installerScript = @'
# wolle Installer
Write-Host "Installing wolle..." -ForegroundColor Green

# Stop any running instances
Get-Process | Where-Object { $_.ProcessName -eq "wolle" } | Stop-Process -Force

# Create installation directory
$installPath = "$env:ProgramFiles\wolle"
New-Item -ItemType Directory -Force -Path $installPath

# Copy application files
Copy-Item "*.dll" -Destination $installPath
Copy-Item "*.exe" -Destination $installPath
Copy-Item "*.json" -Destination $installPath

# Create desktop shortcut
$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut("$env:USERPROFILE\Desktop\wolle.lnk")
$Shortcut.TargetPath = "$installPath\wolle.exe"
$Shortcut.Save()

# Register context menu
& "$installPath\wolle.exe"

Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Right-click on any file and select 'Untangle the Wolle'" -ForegroundColor Yellow
'@

$installerScript | Out-File -FilePath "installer\install.ps1" -Encoding UTF8

Write-Host "Installer created in 'installer' directory" -ForegroundColor Green
```

## Manual Installation

For development/testing, you can run the application directly:

1. Build the project:
   ```bash
   dotnet build
   ```

2. Register the context menu:
   ```bash
   dotnet run
   ```

3. Test with a file:
   ```bash
   dotnet run "path\to\your\file.txt"
   ```