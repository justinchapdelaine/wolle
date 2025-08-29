#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
mod ollama;
mod utils;
use tracing_subscriber::{fmt, EnvFilter};
use tauri::{window::Color, WebviewUrl, WebviewWindowBuilder, Manager, Listener};
/// Returns true if Windows is using light app mode. Defaults to light on lookup failure.
#[cfg(target_os = "windows")]
fn is_light_theme() -> bool {
    use winreg::enums::HKEY_CURRENT_USER;
    use winreg::RegKey;
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let personalize = hkcu
        .open_subkey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
    if let Ok(key) = personalize {
        if let Ok(val) = key.get_value::<u32, _>("AppsUseLightTheme") {
            return val != 0;
        }
    }
    // Default to light on failure
    true
}

/// Decide which URL the webview should load:
/// - Dev: Vite dev server
/// - Release: bundled `index.html`
fn resolve_url() -> WebviewUrl {
    if cfg!(debug_assertions) {
        WebviewUrl::External("http://localhost:5173".parse().unwrap())
    } else {
        WebviewUrl::App("index.html".into())
    }
}

/// Compute a native-looking background color for the window based on OS theme (Windows only).
#[cfg(target_os = "windows")]
fn resolve_bg() -> Color {
    if is_light_theme() {
        Color(0xFF, 0xFF, 0xFF, 0xFF)
    } else {
        Color(0x11, 0x11, 0x11, 0xFF)
    }
}

use std::sync::{atomic::{AtomicBool, Ordering}, Arc};

/// Wire "hide-until-ready" behavior:
/// - Listens for the frontend "frontend-ready" event and shows/focuses once.
/// - Adds a 250ms safety timer to reveal if the event is missed.
/// This should be called before creating the window to avoid races.
fn wire_hide_until_ready(app: &tauri::AppHandle<tauri::Wry>, shown: &Arc<AtomicBool>) {
    let shown_for_event = Arc::clone(shown);
    let app_for_event = app.clone();
    app.listen("frontend-ready", move |_e| {
        if !shown_for_event.swap(true, Ordering::SeqCst) {
            if let Some(window) = app_for_event.get_webview_window("main") {
                let _ = window.show();
                let _ = window.set_focus();
            }
        }
    });

    // Safety timeout: ensure the window appears even if the event is missed
    let shown_for_timer = Arc::clone(shown);
    let app_for_timer = app.clone();
    std::thread::spawn(move || {
        std::thread::sleep(std::time::Duration::from_millis(250));
        if !shown_for_timer.swap(true, Ordering::SeqCst) {
            if let Some(window) = app_for_timer.get_webview_window("main") {
                let _ = window.show();
                let _ = window.set_focus();
            }
        }
    });
}

/// Create the primary "main" window with title, visibility and native background color.
/// The window contents are determined by `resolve_url()`.
fn create_main_window(app: &tauri::AppHandle<tauri::Wry>, visible: bool) -> tauri::Result<()> {
    let url = resolve_url();
    let mut builder = WebviewWindowBuilder::new(app, "main", url)
        .title("Wolle")
        .visible(visible);
    #[cfg(target_os = "windows")]
    {
        builder = builder.background_color(resolve_bg());
    }
    builder.build().map(|_| ())
}

// Use the concrete runtime type from the wry runtime crate.
// tauri_runtime_wry's Webview type adapts to the platform; on Windows it uses webview2-com
// under the hood when the webview2-com feature is enabled in `tauri`.
// Use the re-exported runtime type alias from `tauri` to get a concrete, correctly
// instantiated runtime type (includes the required UserEvent generic).
// We'll reference it as `tauri::Wry` below.

#[derive(Serialize)]
struct Health {
    ok: bool,
    message: String,
}

// Use Tauri's built-in SystemTray API when the `tray` feature is enabled.
#[cfg(feature = "tray")]
fn main() {
    // init logging (to stdout/stderr; Tauri captures in dev console)
    let _ = fmt()
        .with_env_filter(EnvFilter::from_default_env())
        .try_init();
    use std::thread;
    use tauri::Manager;

    // We create the window and the tray during setup so we can use the App as the Manager
    tauri::Builder::<tauri::Wry>::new()
        .invoke_handler(tauri::generate_handler![health_check, run_action, close_app])
        .setup(|app| {
            // Prepare hide-until-ready signaling BEFORE building the webview
            let shown = Arc::new(AtomicBool::new(false));
            wire_hide_until_ready(&app.handle(), &shown);
            // Create the window hidden to avoid any flash
            create_main_window(&app.handle(), false)?;
            // No window-level menu; Esc close is handled by the frontend invoking `close_app`.
            // Build a simple tray menu with a status item and actions
            let menu = tauri::menu::MenuBuilder::new(app)
                .text("status", "Checking Ollama...")
                .separator()
                .text("show", "Show")
                .text("quit", "Quit")
                .build()?;

            // Create the tray icon with the menu. Icon is optional here.
            let tray = tauri::tray::TrayIconBuilder::new().menu(&menu).build(app)?;

            // Register menu event handler
            tray.on_menu_event(|app_handle, event| match event.id().as_ref() {
                "quit" => {
                    std::process::exit(0);
                }
                "show" => {
                    if let Some(window) = app_handle.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    } else {
                        // Re-create the main window if it was closed.
                        let _ = create_main_window(app_handle, true);
                    }
                }
                _ => {}
            });

            // Spawn background thread to poll Ollama and update tray tooltip (debounced)
            let tray_clone = tray.clone();
            thread::spawn(move || {
                let mut last = String::new();
                loop {
                    let status_text = match ollama::health() {
                        Ok(_) => "Ollama: OK".to_string(),
                        Err(e) => format!("Ollama: {}", e),
                    };
                    if status_text != last {
                        let _ = tray_clone.set_tooltip(Some(status_text.clone()));
                        last = status_text;
                    }
                    thread::sleep(std::time::Duration::from_secs(30));
                }
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

#[cfg(not(feature = "tray"))]
fn main() {
    // init logging
    let _ = fmt()
        .with_env_filter(EnvFilter::from_default_env())
        .try_init();
    tauri::Builder::<tauri::Wry>::new()
        .invoke_handler(tauri::generate_handler![health_check, run_action, close_app])
        .setup(|app| {
            // Prepare hide-until-ready signaling BEFORE building the webview
            let shown = Arc::new(AtomicBool::new(false));
            wire_hide_until_ready(&app.handle(), &shown);
            // Create the window hidden to avoid any flash
            create_main_window(&app.handle(), false)?;
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

#[tauri::command]
fn health_check() -> Result<Health, String> {
    match ollama::health() {
        Ok(msg) => Ok(Health {
            ok: true,
            message: msg,
        }),
        Err(e) => Err(format!("{}", e)),
    }
}

#[tauri::command]
fn run_action(action: String, input: String) -> Result<String, String> {
    // Build a prompt using the helper and forward to ollama
    let prompt = utils::format_prompt(&action, &input);
    ollama::query(&prompt)
        .map_err(|e| format!("Action '{}' failed: {}", action, e))
}

// When the tray feature is enabled, Esc should hide the window so it can be re-shown from the tray.
#[cfg(feature = "tray")]
#[tauri::command]
fn close_app(app: tauri::AppHandle<tauri::Wry>) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("main") {
        window.hide().map_err(|e| e.to_string())
    } else {
        // No window to hide; nothing to do.
        Ok(())
    }
}

// Without the tray, Esc fully closes the window; if missing, exit the app as a fallback.
#[cfg(not(feature = "tray"))]
#[tauri::command]
fn close_app(app: tauri::AppHandle<tauri::Wry>) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("main") {
        window.close().map_err(|e| e.to_string())
    } else {
        app.exit(0);
        Ok(())
    }
}
