# Product Requirements Document: Wolle

## 1. Introduction
This document outlines the requirements for a desktop application named **Wolle**. The application will provide AI-powered actions to the user through the Windows right-click context menu. It will be built using Rust and the Tauri framework and will use a local Ollama server running the `gemma3:4b` model to process all AI requests. The application is non-persistent; it will launch only when a context menu item is selected and will exit after the task is completed.

## 2. Goals
The primary goal of this project is to create a seamless and privacy-focused user experience by integrating local AI capabilities directly into the user's daily workflow. The application will enable users to perform AI actions on selected text, files, and images without relying on cloud services. The application will be a lightweight, single-purpose utility that focuses on on-demand, context-based AI actions.

## 3. Target Audience
*   **Developers and power users:** Individuals who frequently interact with text and code and want to automate or augment their work using local AI.
*   **AI enthusiasts:** Users who want to experiment with local AI models and prefer a lightweight, on-demand interface.
*   **Privacy-conscious users:** Individuals who prefer to keep their data and AI interactions local to their machine.

## 4. Technology Stack
*   **Backend:** Rust
*   **Frontend:** Tauri framework (using a web UI framework like Microsoft's Fluent UI Web Components for a native look and feel)
*   **AI Engine:** Local Ollama server (with the `gemma3:4b` model)
*   **Windows Integration:** Windows API via the Rust `windows` crate
*   **Installer:** Rust-based installer (e.g., `cargo-wix` or `msi` crate)

## 5. User Stories
*   A user wants to right-click on selected text and choose "**Summarize this**" to get a concise summary in a popup window.
*   A user wants to right-click on selected text and choose "**Rewrite this**" to get a rewritten version in a popup window.
*   A user wants to right-click on selected text and choose "**Translate this**" to get a translation in a popup window.
*   A user wants to right-click on an image file and choose "**Analyze this**" to get an AI-generated description of the image.
*   The application should be easy to install and should automatically configure the necessary AI components.

## 6. Features

### Installer
*   The installer will be a simple, standard executable.
*   It will detect if the Ollama CLI is installed on the user's system. If not, it will install the Ollama CLI.
*   It will detect if the `gemma3:4b` model is downloaded by checking the output of `ollama list`. If not, it will automatically download the model using `ollama pull gemma3:4b`.

### Windows Context Menu Integration
*   **Text Selection:** Context menu items for "Summarize this," "Rewrite this," and "Translate this" on selected text.
*   **File Selection:** Context menu items for "Analyze this" on files, images, and documents.
*   **Image URLs:** Context menu items for images on webpages, allowing the user to send the image URL to Ollama for analysis.

### AI Actions
*   **Summarize:** Generates a concise summary of selected text or a document.
*   **Rewrite:** Rewrites selected text or a document in a different style or tone.
*   **Translate:** Translates the selected text.
*   **Analyze:** Describes the content of an image or extracts key information from a document.

### Ollama Integration
*   Communicate with the local Ollama server via its REST API.
*   The application will detect if the Ollama server is running. If not, it will attempt to start it, and if that fails, it will display a notification to the user.

### System Tray Icon
*   The app will include a system tray icon that is visible while the application is running in the background.
*   The tray icon will provide a visual indication of the app's status (e.g., active, idle).
*   Clicking the tray icon will display a temporary window showing detailed status information, such as the Ollama server connection status, model loading status, and other relevant messages.
*   The tray icon's context menu will include options to manage or quit the application.

### Performance
*   The application should have a minimal memory footprint and prioritize quick execution for on-demand actions.
*   API requests to Ollama should be handled efficiently to minimize latency.

## 7. Context Menu Behavior
The context menu options will be permanently available in Windows Explorer once the application is installed.
*   When a user selects an action, the context menu will launch a new instance of Wolle with specific command-line arguments.
*   Wolle will perform the requested action, display the result in a temporary window (with a Fluent UI design), and then exit cleanly.
*   **The temporary window will appear near the location where the user right-clicked to trigger the context menu.**

## 8. Non-Goals
*   This application will not be a full-fledged chat interface.
*   This application will not support cloud-based AI models.
*   This application will not be cross-platform; it will be Windows-only.
*   The user will not be able to change the Ollama model to use; it is fixed to `gemma3:4b` at this time.

## 9. Risks and Dependencies
*   **Ollama Configuration:** The application depends on a local Ollama server running the `gemma3:4b` model. Correct user setup is required.
*   **Windows Registry:** Modifying the Windows Registry for context menu integration carries a risk of system instability if not done correctly.
*   **File Access:** The application will need permissions to read the contents of files and clipboard data.
*   **Webview and Shell Extension Compatibility:** There may be compatibility issues with the Tauri webview and the native shell extension code.

## 10. Success Metrics
*   **User Adoption:** Number of users who install and regularly use the application.
*   **Performance:** Average latency for each AI action is within acceptable limits (e.g., < 5 seconds).
*   **Stability:** Low crash rate for the application and the shell extension.
