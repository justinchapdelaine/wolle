# wolle

<div align="center">

![License](https://img.shields.io/badge/license-GPL%20v3-blue.svg)
![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-blue.svg)
![Architecture](https://img.shields.io/badge/architecture-x64-lightgrey.svg)

**"Untangle the Wolle on your files"**

A modern Windows shell extension that leverages Ollama AI to analyze files through an intuitive right-click context menu.

[📖 Features](#-features) • [🚀 Installation](#-installation) • [💻 Usage](#-usage) • [⚙️ Configuration](#️-configuration) • [🛠️ Development](#️-development) • [📄 License](#-license)

</div>

## ✨ Features

- **🖱️ Windows Shell Integration**: Right-click any file and select "Untangle the Wolle" for instant AI analysis
- **🎯 Multi-Modal Support**: Works seamlessly with images (.png, .jpg, .jpeg), text files (.txt, .md), and code files (.cs, .js, .py)
- **⚡ Real-Time Streaming**: Watch AI responses stream in real-time as they're generated
- **🎨 Modern Fluent UI**: Clean, responsive popup window with automatic light/dark theme switching
- **🔄 Auto-Close**: Window automatically closes when clicking outside for smooth workflow
- **📏 Configurable Context Window**: Choose between 32K, 64K, or 128K token context windows for optimal performance
- **🔧 Advanced Configuration**: Comprehensive settings for Ollama integration, file size limits, and timeouts
- **🛡️ Robust Error Handling**: Network resilience with exponential backoff retry logic and comprehensive logging
- **🔒 Security-First**: Input validation, path sanitization, and localhost-only API endpoints

## 🎯 Requirements

- **Windows 10 or later** (version 22621.0 or higher)
- **Ollama** (bundled standalone CLI or installed separately)
- **.NET 9.0 Runtime** (included with installer)

## 🚀 Installation

### Quick Install

1. Download the latest release from the [Releases](../../releases) page
2. Run the installer executable
3. The application will automatically register the context menu
4. Right-click on any supported file and select "Untangle the Wolle"

### Manual Install (Development)

For development or testing purposes:

```bash
# Clone the repository
git clone https://github.com/yourusername/wolle.git
cd wolle

# Build the application
dotnet build

# Register the context menu
dotnet run

# Test with a specific file
dotnet run "path\to\your\file.txt"
```

## 💻 Usage

1. **Right-click** on any supported file (image, text, or code)
2. **Select** "Untangle the Wolle" from the context menu
3. **Watch** as a popup window appears showing "Thinking..."
4. **See** the AI response stream in real-time
5. **Click** anywhere outside the window to close it

## ⚙️ Configuration

The application creates a configuration file at `%APPDATA%\wolle\settings.json`:

```json
{
  "OllamaPath": "",
  "OllamaEndpoint": "http://127.0.0.1:11434",
  "ModelName": "gemma3:4b",
  "ContextWindowSize": 128000,
  "MaxFileSize": 10485760,
  "ApiTimeoutSeconds": 600,
  "MaxLogSizeBytes": 10485760,
  "MaxLogFiles": 5,
  "Prompts": {
    "Image": "Explain this image to me? {0}",
    "Text": "Summarize this text for me? {0}",
    "Code": "Analyze this code and explain what it does? {0}"
  }
}
```

### Configuration Options

| Option | Description | Default | Range |
|--------|-------------|---------|-------|
| `OllamaPath` | Path to Ollama executable | Auto-detected | Valid file path |
| `OllamaEndpoint` | Ollama API endpoint | `http://127.0.0.1:11434` | Localhost only |
| `ModelName` | AI model to use | `gemma3:4b` | Valid Ollama models |
| `ContextWindowSize` | Token context window size | 128000 | 32000, 64000, 128000 |
| `MaxFileSize` | Maximum file size in bytes | 10485760 (10MB) | 1MB - 104857600 (100MB) |
| `ApiTimeoutSeconds` | API timeout in seconds | 600 (10 min) | 60 - 1800 (30 min) |
| `MaxLogSizeBytes` | Maximum log file size in bytes | 10485760 (10MB) | 1MB - 100MB |
| `MaxLogFiles` | Maximum number of log files to keep | 5 | 1 - 20 |
| `Prompts` | Custom AI prompts per file type | See default | Custom templates |

## 📁 Supported File Types

### Images
- **PNG** (.png)
- **JPEG** (.jpg, .jpeg)

### Text Files
- **Plain Text** (.txt)
- **Markdown** (.md)

### Code Files
- **C#** (.cs)
- **JavaScript** (.js)
- **Python** (.py)

## 🏗️ Architecture

The application follows a **service-oriented architecture** with clear separation of concerns:

### Core Services
- **OllamaService**: Handles AI model communication and response streaming
- **FileProcessingService**: Manages file validation, reading, and processing
- **ContextMenuService**: Handles Windows shell integration and context menu registration
- **SettingsManagementService**: Manages configuration loading, validation, and persistence
- **WindowManagementService**: Controls window lifecycle, positioning, and cleanup
- **ErrorManagementService**: Provides centralized error handling and user-friendly messages
- **ProgressManagementService**: Manages progress indicators and status updates
- **StatusManagementService**: Handles status display and time formatting
- **EventManagementService**: Centralizes event subscription and forwarding
- **ResourceManagementService**: Provides consistent resource access and fallback handling

### UI Layer
- **MainWindow**: Primary application window with Fluent UI theming
- **ResponseDisplayCoordinator**: Manages markdown rendering and display coordination
- **ResponseUIService**: Handles UI updates for response streaming
- **UIInteractionService**: Manages user interactions and input handling

### Infrastructure
- **MarkdownConversionService**: Converts markdown to XAML using Markdig
- **ValidationService**: Provides input validation and sanitization
- **PluginManager**: Extensible plugin system for file processors
- **Logging**: Enterprise-grade logging with Serilog and Microsoft.Extensions.Logging

## 🛠️ Development

### Prerequisites
- **.NET 9.0 SDK** or later
- **Windows 10/11** development environment
- **Visual Studio 2022** or **VS Code** with C# extension

### Building

```bash
# Build the project
dotnet build

# Build in release mode
dotnet build -c Release

# Self-contained publish
dotnet publish -c Release -r win-x64 --self-contained true
```

### Testing

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "TestName"

# Run with verbose output
dotnet test --verbosity normal
```

### Code Quality

```bash
# Format code
dotnet format

# Verify formatting
dotnet format --verify-no-changes

# Lint code (if configured)
dotnet lint
```

### Running

```bash
# Run from source
dotnet run

# Run with file argument
dotnet run "path\to\file.txt"

# Unregister context menu
dotnet run --unregister
```

## 📦 Dependencies

### Core Framework
- **.NET 9.0**: Modern, high-performance runtime
- **WPF**: Windows Presentation Foundation with Fluent UI
- **C#**: Modern C# with nullable reference types

### Packages
- **Markdig** (0.42.0): Fast and powerful Markdown processor
- **Neo.Markdig.Xaml** (1.0.10): Markdown to XAML converter
- **Microsoft.Extensions.DependencyInjection** (9.0.0): Dependency injection
- **Microsoft.Extensions.Hosting** (9.0.0): Application hosting infrastructure
- **Microsoft.Extensions.Logging** (9.0.0): Logging abstraction
- **Serilog** (4.2.0): Structured logging framework
- **Serilog.Extensions.Logging** (8.0.0): Serilog integration
- **Serilog.Sinks.File** (6.0.0): File logging with rotation
- **Serilog.Sinks.Console** (6.0.0): Console logging
- **Serilog.Sinks.Debug** (3.0.0): Debug output logging

## 🐛 Troubleshooting

### Common Issues

**Context menu not appearing:**
- Run the application once as administrator to register the context menu
- Check Windows security settings for shell extensions

**Ollama connection issues:**
- Ensure Ollama is running (`ollama serve`)
- Verify the endpoint configuration in settings.json
- Check network connectivity to localhost:11434

**Model not found:**
- Run `ollama pull gemma3:4b` to download the model
- Verify model name in settings.json

### Logging

The application provides comprehensive logging to multiple locations:

- **Console**: Real-time output during development
- **Debug Window**: Visual Studio debug output
- **Files**: `%LOCALAPPDATA%\wolle\logs\wolle_.log` with automatic rotation

Log files include detailed information about:
- API requests and responses
- File processing operations
- Error conditions and stack traces
- Performance metrics

## 🤝 Contributing

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'feat: add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Guidelines
- Follow conventional commit standards
- Maintain code quality with `dotnet format`
- Include XML documentation for public APIs
- Write tests for new functionality
- Update documentation as needed

## 📄 License

This project is licensed under the **GNU General Public License v3.0** - see the [LICENSE](LICENSE) file for details.

### License Summary
- ✅ Commercial use
- ✅ Modification
- ✅ Distribution
- ✅ Private use
- ❗ Liability and warranty disclaimed
- ❗ Must disclose source code
- ❗ Must license derivative works under GPL v3

## 🙏 Acknowledgments

- **Ollama** team for the amazing AI framework
- **.NET** team for the modern development platform
- **Fluent UI** for the beautiful design system
- **Serilog** team for enterprise-grade logging
- **Markdig** team for fast Markdown processing

---

<div align="center">

**Made with ❤️ using .NET 9 and WPF**

[🔝 Back to top](#wolle)

</div>