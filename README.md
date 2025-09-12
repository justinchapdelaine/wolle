# wolle

"Untangle the Wolle on your files" - A Windows shell extension that uses Ollama AI to analyze files via right-click context menu.

## Features

- **Windows Shell Integration**: Right-click on any file and select "Untangle the Wolle" to analyze it with AI
- **Multi-modal Support**: Works with images (.png, .jpg, .jpeg), text files (.txt, .md), and code files (.cs, .js, .py)
- **Real-time Streaming**: Watch AI responses stream in real-time as they're generated
- **Modern UI**: Clean, responsive popup window with Fluent UI design and automatic light/dark theme switching
- **Auto-close**: Window automatically closes when clicking outside
- **Configurable Context Window**: Choose between 32K, 64K, or 128K token context windows for optimal performance
- **Advanced Configuration**: Comprehensive settings for Ollama integration, file size limits, and timeouts
- **Robust Error Handling**: Network resilience with retry logic and comprehensive logging
- **Security**: Input validation, path sanitization, and localhost-only API endpoints

## Requirements

- Windows 10 or later (version 22621.0 or higher)
- Ollama (bundled standalone CLI or installed separately)
- .NET 9.0 Runtime (included with installer)

## Installation

1. Download and run the installer
2. The application will automatically register the context menu
3. Right-click on any supported file and select "Untangle the Wolle"

## Usage

1. Right-click on a file (image, text, or code)
2. Select "Untangle the Wolle" from the context menu
3. A popup window will appear showing "Thinking..." 
4. The AI response will stream in real-time
5. Click anywhere outside the window to close it

## Configuration

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

- `OllamaPath`: Optional path to Ollama executable (auto-detected if empty)
- `OllamaEndpoint`: Ollama API endpoint (default: http://127.0.0.1:11434)
- `ModelName`: AI model to use (default: gemma3:4b)
- `ContextWindowSize`: Token context window size (32000, 64000, or 128000)
- `MaxFileSize`: Maximum file size in bytes (default: 10MB, max: 100MB)
- `ApiTimeoutSeconds`: API timeout in seconds (default: 10 minutes, max: 30 minutes)
- `MaxLogSizeBytes`: Maximum log file size in bytes (default: 10MB)
- `MaxLogFiles`: Maximum number of log files to keep (default: 5)
- `Prompts`: Customize AI prompts for different file types (`{0}` is replaced with file path)

## Supported File Types

### Images
- PNG (.png)
- JPEG (.jpg, .jpeg)

### Text Files  
- Plain Text (.txt)
- Markdown (.md)

### Code Files
- C# (.cs)
- JavaScript (.js)
- Python (.py)

## Technology Stack

- **UI Framework**: WPF with .NET 9 built-in Fluent UI
- **AI Model**: Gemma3:4b (multimodal)
- **Configuration**: JSON settings in AppData
- **Process Management**: Streaming Ollama API integration
- **Markdown Rendering**: Markdig with Neo.Markdig.Xaml
- **Logging**: Structured logging with rotation
- **Security**: Input validation and path sanitization

## Development

### Building

```bash
dotnet build
```

### Running

```bash
dotnet run
```

### Testing

```bash
dotnet test
```

### Code Formatting

```bash
dotnet format
```

## Architecture

The application follows a service-oriented architecture with clear separation of concerns:

- **Services**: Core business logic (OllamaService, SettingsService, LoggerService, etc.)
- **Views**: UI layer with XAML and code-behind
- **Extensions**: Utility extensions for WPF controls
- **Configuration**: JSON-based settings management
- **Security**: Input validation and sanitization services

## License

GPL v3 - see LICENSE file for details.