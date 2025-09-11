# Build and Release

## Prerequisites

- .NET 8.0 SDK
- Windows 10 or later

## Building

### Development Build

```bash
dotnet build
```

### Release Build

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The published application will be in `bin\Release\net8.0-windows\win-x64\publish\`

## Testing

### Register Context Menu
```bash
dotnet run
```

### Test with File
```bash
dotnet run "path\to\test\file.txt"
```

### Unregister Context Menu
The context menu is automatically unregistered when the application exits.

## Creating Installer

See [INSTALL.md](INSTALL.md) for detailed installer creation instructions.

## Release Checklist

- [ ] Build Release configuration
- [ ] Test on clean Windows machine
- [ ] Verify Ollama auto-detection works
- [ ] Test all supported file types
- [ ] Verify streaming responses work
- [ ] Test context menu registration/unregistration
- [ ] Create installer package
- [ ] Update documentation
- [ ] Tag release