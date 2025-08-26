# Wolle App General Guidelines

## Context

This project is the **Wolle** desktop application, a lightweight, non-persistent Windows utility for AI-powered actions. It uses a local Ollama server running the `gemma3:4b` model. The app should launch and exit quickly, triggered by the Windows right-click context menu.

## Technologies and Frameworks

- **Backend**: Rust, using the Tauri framework for the desktop app shell.
- **API Integration**: The `windows` crate is used for interacting with the Windows API.
- **Frontend**: Vite, using plain JavaScript and **Microsoft's Fluent UI Web Components**.
- **AI Integration**: Communication with the local Ollama REST API.
- **Package Management**: `npm` for frontend dependencies and `cargo` for Rust dependencies.

## Architecture

- The app is a lightweight, non-persistent Windows utility.
- A Rust executable handles command-line arguments and backend logic.
- Tauri is used to create a temporary, Fluent UI-styled webview window for displaying AI responses.
- The app communicates with a local Ollama server (with the `gemma3:4b` model) via its REST API.
- The app exits cleanly after displaying the AI response.
- A system tray icon must be present while the app is active in the background.
- Clicking the tray icon should display a temporary window showing the status of the Ollama server, model loading, and other relevant information.
- **The application must use Tauri's native menu APIs for all context menu implementations. Avoid using JavaScript-based context menu libraries.**

## Installer

- The installer should be created using a Rust-based tool like `cargo-wix` or the `msi` crate.
- It must detect and install the Ollama CLI if missing.
- It must detect and download the `gemma3:4b` model if missing.

## Code Style

- **Rust**: Adhere to Rust's best practices and standard formatting. Use clear, concise comments.
- **JavaScript**: Use `const` and `let` for variable declarations. Use semantic HTML. Maintain a consistent 2-space indentation.
- **General**: Write clean, robust, and error-handling code.

## Naming Conventions

- **Rust**: Use `snake_case` for variables and function names.
- **JavaScript**: Use `camelCase` for variables and functions.

## User Experience

- Prioritize a smooth and fast user experience, especially for startup and exit.
- Ensure all user-facing messages and notifications are clear and helpful.
- The UI should be lightweight and temporary, and its status should be visible via the system tray.

## Important Note

The app's behavior and features are fully detailed in the PRD, located at `.github/instructions/prd.instructions.md`. Always refer to this document for the complete specifications. Project progress is tracked in the `progress.instructions.md` file, located at `.github/instructions/progress.instructions.md`.
