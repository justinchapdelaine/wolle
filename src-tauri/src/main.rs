#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
mod ollama;
mod utils;
use tracing_subscriber::{fmt, EnvFilter};
use tauri::{window::Color, WebviewUrl, WebviewWindowBuilder, Manager, Listener};
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
            // Determine URL (Vite dev server in debug; bundled index.html in release)
            let url = if cfg!(debug_assertions) {
                WebviewUrl::External("http://localhost:5173".parse().unwrap())
            } else {
                WebviewUrl::App("index.html".into())
            };
            let mut builder = WebviewWindowBuilder::new(app, "main", url)
                .title("Wolle")
                // Start hidden to avoid any flash; will show on frontend-ready or fallback timer
                .visible(false);
            // Pre-paint background color to eliminate white flash
            #[cfg(target_os = "windows")]
            {
                let bg = if is_light_theme() {
                    Color(0xFF, 0xFF, 0xFF, 0xFF)
                } else {
                    Color(0x11, 0x11, 0x11, 0xFF)
                };
                builder = builder.background_color(bg);
            }
            // Prepare hide-until-ready signaling BEFORE the webview loads to avoid races
            use std::sync::{atomic::{AtomicBool, Ordering}, Arc};
            let shown = Arc::new(AtomicBool::new(false));
            let shown_for_event = shown.clone();
            let app_for_event = app.handle().clone();
            app.listen("frontend-ready", move |_e| {
                if !shown_for_event.swap(true, Ordering::SeqCst) {
                    if let Some(window) = app_for_event.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            });

            // Esc close is handled via the `close_app` Tauri command invoked by the frontend.

            // Create the window after listener is registered
            builder.build()?;

            // No window-level menu; Esc close is handled by the frontend invoking `close_app`.



            // Safety timeout: ensure the window appears even if the event is missed
            let shown_for_timer = shown.clone();
            let app_for_timer = app.handle().clone();
            std::thread::spawn(move || {
                std::thread::sleep(std::time::Duration::from_millis(250));
                if !shown_for_timer.swap(true, Ordering::SeqCst) {
                    if let Some(window) = app_for_timer.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            });
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
                    }
                }
                _ => {}
            });

            // Spawn background thread to poll Ollama and update tray tooltip
            let tray_clone = tray.clone();
            thread::spawn(move || loop {
                let status_text = match ollama::health() {
                    Ok(_) => "Ollama: OK".to_string(),
                    Err(e) => format!("Ollama: {}", e),
                };
                // Update the tray tooltip with the status (works cross-platform)
                let _ = tray_clone.set_tooltip(Some(status_text));
                thread::sleep(std::time::Duration::from_secs(30));
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
            let url = if cfg!(debug_assertions) {
                WebviewUrl::External("http://localhost:5173".parse().unwrap())
            } else {
                WebviewUrl::App("index.html".into())
            };
            let mut builder = WebviewWindowBuilder::new(app, "main", url)
                .title("Wolle")
                // Start hidden to avoid any flash; will show on frontend-ready or fallback timer
                .visible(false);
            #[cfg(target_os = "windows")]
            {
                let bg = if is_light_theme() {
                    Color(0xFF, 0xFF, 0xFF, 0xFF)
                } else {
                    Color(0x11, 0x11, 0x11, 0xFF)
                };
                builder = builder.background_color(bg);
            }
            // Prepare hide-until-ready signaling BEFORE the webview loads to avoid races
            use std::sync::{atomic::{AtomicBool, Ordering}, Arc};
            let shown = Arc::new(AtomicBool::new(false));
            let shown_for_event = shown.clone();
            let app_for_event = app.handle().clone();
            app.listen("frontend-ready", move |_e| {
                if !shown_for_event.swap(true, Ordering::SeqCst) {
                    if let Some(window) = app_for_event.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            });

            // Esc close is handled via the `close_app` Tauri command invoked by the frontend.

            // Create the window after listener is registered
            builder.build()?;

            // No window-level menu; Esc close is handled by the frontend invoking `close_app`.



            // Safety timeout: ensure the window appears even if the event is missed
            let shown_for_timer = shown.clone();
            let app_for_timer = app.handle().clone();
            std::thread::spawn(move || {
                std::thread::sleep(std::time::Duration::from_millis(250));
                if !shown_for_timer.swap(true, Ordering::SeqCst) {
                    if let Some(window) = app_for_timer.get_webview_window("main") {
                        let _ = window.show();
                        let _ = window.set_focus();
                    }
                }
            });
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
    ollama::query(&prompt).map_err(|e| format!("{}", e))
}

#[tauri::command]
fn close_app(app: tauri::AppHandle<tauri::Wry>) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("main") {
        window.close().map_err(|e| e.to_string())
    } else {
        // If the window isn't found, exit the app as a fallback
        app.exit(0);
        Ok(())
    }
}
