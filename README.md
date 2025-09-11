# wolle

"Untangle the Wolle on your files" - A Windows shell extension that uses Ollama AI to analyze files via right-click context menu.

## Features

- **Windows Shell Integration**: Right-click on any file and select "Untangle the Wolle" to analyze it with AI
- **Multi-modal Support**: Works with images (.png, .jpg, .jpeg), text files (.txt, .md), and code files (.cs, .js, .py)
- **Real-time Streaming**: Watch AI responses stream in real-time as they're generated
- **Modern UI**: Clean, responsive popup window with Fluent UI design
- **Auto-close**: Window automatically closes when clicking outside
- **Configurable Prompts**: Customize AI prompts per file type via JSON settings

## Requirements

- Windows 10 or later
- Ollama (bundled standalone CLI or installed separately)
- .NET 8.0 Runtime (included with installer)

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
  "Prompts": {
    "Image": "Explain this image to me? {0}",
    "Text": "Summarize this text for me? {0}", 
    "Code": "Analyze this code and explain what it does? {0}"
  }
}
```

- `OllamaPath`: Optional path to Ollama executable (auto-detected if empty)
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

- **UI Framework**: WPF with Fluent UI WPF
- **AI Model**: Gemma3:4b (multimodal)
- **Configuration**: JSON settings in AppData
- **Process Management**: Streaming Ollama CLI integration

## License

GPL v3 - see LICENSE file for details.