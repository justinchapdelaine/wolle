# wolle Development Guidelines

## Project Overview
"wolle" - Untangle the Wolle on your files
License: GPL v3
Tech Stack: WPF with .NET 9 built-in Fluent UI
Model: Gemma3:4b (multimodal)
Window: 350px width, auto-height up to 600px, modeless, closes on outside click
Theme: Automatic light/dark mode using Fluent ThemeMode APIs

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Single test: `dotnet test --filter "TestName"`
- Lint: `dotnet format --verify-no-changes`
- Format: `dotnet format`
- Run: `dotnet run`

## Logging
The application uses **Microsoft.Extensions.Logging with Serilog** for enterprise-grade logging:
- **Console Output**: Logs to console with configurable levels and formatting
- **Debug Output**: Integrated with Visual Studio/debug output
- **File Logging**: Serilog file sink with rotation and cleanup
- **Structured Logging**: Full support for structured logging with scopes and properties
- **Dependency Injection**: Native integration with Microsoft.Extensions.DependencyInjection
- **Configuration**: Configurable log levels through Serilog configuration

### Logging Features:
- **File Rotation**: Automatic log file rotation when size limits are reached
- **Cleanup**: Automatic cleanup of old log files (configurable retention)
- **Performance**: Optimized async logging with minimal allocations
- **Extensibility**: Easy to add additional sinks (Application Insights, Seq, etc.)
- **Templates**: Customizable output templates for different sinks
- **Enrichment**: Automatic enrichment with log context and source information

### Log Output Locations:
- **Console**: Real-time console output during development
- **Debug Window**: Visual Studio debug output window
- **Files**: `%LOCALAPPDATA%\wolle\logs\wolle_.log` with rotation
- **Format**: `[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}`

### Configuration:
- **Minimum Level**: Debug (can be changed in SerilogConfig.cs)
- **File Size Limit**: Configurable via settings (default 10MB)
- **File Retention**: Configurable via settings (default 5 files)
- **Flush Interval**: 1 second for timely file writes

## Commit Standards
- **Format**: Use Conventional Commit standards: `<type>[optional scope]: <description>`
- **Types**: 
  - `feat`: New feature
  - `fix`: Bug fix
  - `refactor`: Code restructuring without functional changes
  - `chore`: Maintenance tasks, build process, dependencies
  - `docs`: Documentation changes
  - `style`: Code formatting, styling
  - `test`: Test-related changes
  - `perf`: Performance improvements
- **Examples**: 
  - `feat: add markdown rendering support`
  - `fix: resolve scrollbar text overlap issue`
  - `refactor: rename development-notes to Docs`
  - `chore: update dependencies and add .gitignore`

## Code Style Guidelines
- Language: C#
- Formatting: Follow .NET coding conventions, use `dotnet format`
- Imports: Organize using System.* first, then third-party, then project-specific
- Naming: Use PascalCase for classes/methods, camelCase for parameters/fields
- Error handling: Use try-catch blocks, prefer specific exceptions
- Comments: Use XML documentation for public APIs
- License: All code must comply with GPL v3 terms
- UI Themes: Use Fluent theme resources (SystemControl* brushes) for proper light/dark mode support

## Development Notes
- This is a new C# project starting from scratch
- Use .NET SDK for development
- Update this file as the codebase develops with additional tooling
- Consider adding xUnit/NUnit for testing as project grows

## Research Guidelines
- Always research current .NET/C# best practices online before implementing
- Verify API documentation and package versions are up-to-date
- Check for newer framework features and recommended patterns
- Cross-reference multiple sources for complex implementations
- Note that model training data may be outdated - verify current practices

## Project Requirements
### Core Functionality
- Windows shell context menu integration: "Untangle the Wolle"
- Single popup window that closes when clicking outside
- Stream responses from Ollama in real-time
- Auto-pull Gemma3:4b model on startup
- Bundle Ollama standalone CLI (ollama-windows-amd64.zip + ROCm for AMD)

### File Type Support (Initial)
- Images: .png, .jpg, .jpeg
- Text: .txt, .md
- Code: .cs, .js, .py

### User Experience
- 300 device-independent units width, auto-adjusting height
- Friendly error messages ("Ollama not found", "Network error", "Model not downloaded")
- Streaming text responses as Ollama generates them
- File size limits with configurable defaults (10MB default, 100MB maximum)
- Secure file path handling with validation and sanitization

### Configuration
- JSON settings file in %APPDATA%\wolle\
- Configurable prompts per file type
- Ollama executable path configuration
- Configurable API endpoint with localhost-only validation
- Configurable file size limits (default 10MB, max 100MB)
- Configurable API timeouts (default 5 minutes, max 30 minutes)

## Development Documentation
All development debugging notes and research documentation are located in `Docs/` folder. These files document various debugging sessions, fixes, and research findings during development, including the logging system implementation.

## Important Architecture Notes
### .NET 9 Theming System
**CRITICAL:** This application uses .NET 9's built-in Fluent theming system with `ThemeMode="System"` in App.xaml.

**Key Points:**
- All DynamicResource references (e.g., `SolidBackgroundFillColorBaseBrush`, `TextFillColorPrimaryBrush`) are **official .NET 9 Fluent theme resources**
- `ThemeMode="System"` automatically loads and manages Fluent theme resources
- Resources automatically adapt to Windows light/dark theme changes
- Built-in fallbacks and accessibility support are included
- **DO NOT** attempt to add manual fallback values or replace DynamicResource with StaticResource
- This is a strength, not an issue - provides modern, adaptive theming out of the box

**Resources Used:**
- Background brushes: `SolidBackgroundFillColorBaseBrush`, `CardBackgroundFillColorDefaultBrush`, etc.
- Text brushes: `TextFillColorPrimaryBrush`, `TextFillColorSecondaryBrush`, etc.
- Accent brushes: `AccentFillColorDefaultBrush`, etc.

**Reference:** See `Docs/WPF_ADAPTIVE_BRUSHES.md` for complete list of available Fluent theme brushes.

## Recent Improvements and Fixes
### Critical Issues Fixed:
1. **Grid Layout Issue**: Fixed XAML Grid.Row conflicts where ResponseScrollViewer and ErrorPanel were incorrectly placed in Row="0" instead of Row="1"
2. **Network Error Handling**: Added retry logic with exponential backoff for Ollama API calls to handle network timeouts and connection failures
3. **Memory Leak Prevention**: Enhanced process cleanup with proper locks, force kill with process tree, and timeout handling
4. **Race Condition Fix**: Added thread-safe locks to window closing logic to prevent race conditions between `_isProcessingComplete` and `_isClosing` flags
5. **Configuration Validation**: Added validation for Ollama executable path to ensure it's valid and accessible
6. **Input Security**: Added file path validation and sanitization in context menu service to prevent security vulnerabilities
7. **Model Configuration**: Centralized model name configuration using `_modelName` field instead of hardcoded strings
8. **Null Reference Prevention**: Fixed nullable logger references throughout the codebase

### Code Quality Improvements:
- **Network Resilience**: 3 retry attempts with exponential backoff for API calls
- **Process Management**: Force kill with process tree cleanup and 5-second timeout
- **Thread Safety**: Lock statements to prevent race conditions
- **Input Validation**: Path validation against injection attacks and suspicious characters
- **Maintainability**: Configurable model name system

### Best Practices Implemented:
1. **XML Documentation**: Added comprehensive XML documentation for all public methods and classes
2. **Resource Management**: Proper disposal of HttpClient, SemaphoreSlim, and Process objects
3. **Error Handling**: Enhanced try-catch blocks with proper logging and fallback mechanisms
4. **Thread Safety**: Implemented SemaphoreSlim for thread-safe API calls and lock statements for critical sections
5. **Configuration Management**: Added validation for configuration settings with fallback to defaults
6. **Security**: Input validation and sanitization to prevent security vulnerabilities
7. **Logging**: Consistent logging throughout the application with different log levels
8. **Async/Await Pattern**: Proper async/await usage throughout the codebase
9. **Dependency Injection**: Optional logger parameter in constructor for better testability
10. **Code Organization**: Proper separation of concerns with dedicated service classes

### Architecture Improvements:
- **Service Layer**: Well-defined service classes with single responsibilities
- **Error Boundaries**: Clear error handling boundaries with proper exception propagation
- **Resource Cleanup**: Comprehensive resource cleanup in Dispose methods
- **Configuration**: Centralized configuration management with validation
- **Logging**: Structured logging with different severity levels
- **Security**: Input validation and secure file path handling
- **Performance**: Shared HttpClient instances and proper async patterns
- **Maintainability**: XML documentation and clear method signatures

### Build Quality:
- **Zero Warnings**: All build warnings have been resolved
- **Clean Code**: Consistent code formatting and naming conventions
- **Type Safety**: Proper nullability annotations and type checking
- **Documentation**: Comprehensive XML documentation for IntelliSense
- **Error Handling**: Robust error handling with graceful degradation

### UI/UX Improvements:
1. **Automatic Theme Switching**: Implemented Fluent ThemeMode APIs for automatic light/dark mode based on system settings
2. **Proper Layout Management**: Fixed Grid layout issues and element overlap with proper row definitions
3. **Dynamic Visibility**: Progress section automatically hides when response content starts showing
4. **Window Size Constraints**: Added MaxHeight="600px" to prevent window from growing off-screen
5. **Scrollable Content**: Response area wrapped in ScrollViewer with MaxHeight="400px" for long responses
6. **Fluent Theme Resources**: Replaced SystemColors with proper Fluent theme resources (SystemControl* brushes)
7. **Accent Color Integration**: Progress bars and interactive elements use system accent colors
8. **Improved Spacing**: Optimized margins and padding for better visual balance
9. **Auto-scrolling**: Response area automatically scrolls to bottom when new content is added

### Security Enhancements Implemented:
1. **Configurable API Endpoint**: Made Ollama API endpoint configurable with validation to ensure only localhost/loopback endpoints are allowed
2. **Process Execution Security**: Added validation and sanitization for Ollama executable path and process arguments to prevent command injection
3. **File Path Validation**: Added comprehensive file path validation and sanitization throughout application to prevent path traversal attacks
4. **Registry Operation Security**: Added privilege checks for registry operations and enhanced error handling
5. **File Size Limits**: Implemented configurable file size limits (default 10MB, max 100MB) to prevent memory exhaustion
6. **Input Sanitization**: Added input sanitization for prompts and log messages to prevent injection attacks
7. **Log Security**: Enhanced logging with log rotation (10MB max, 5 files), sanitization, and proper cleanup
8. **Timeout Management**: Added configurable timeouts for API operations (default 5 minutes, max 30 minutes)
9. **Elevation Validation**: Added checks for sufficient privileges when performing registry operations
10. **Error Message Sanitization**: Implemented proper error message handling to prevent information disclosure