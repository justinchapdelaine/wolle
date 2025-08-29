#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
mod ollama;
mod utils;
use tracing_subscriber::{fmt, EnvFilter};
use tauri::{window::Color, WebviewUrl, WebviewWindowBuilder, Manager, Listener, WindowEvent};
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
    // In dev, load the explicit multipage entry to avoid redirect logic
    let url = if cfg!(debug_assertions) {
        WebviewUrl::External("http://localhost:5173/action.html".parse().unwrap())
    } else {
        WebviewUrl::App("action.html".into())
    };
    let mut builder = WebviewWindowBuilder::new(app, "main", url)
        .title("Wolle — Action")
        .visible(visible);
    #[cfg(target_os = "windows")]
    {
        builder = builder.background_color(resolve_bg());
    }
    builder.build().map(|_| ())
}

/// Create the secondary "status" window.
fn create_status_window(app: &tauri::AppHandle<tauri::Wry>, visible: bool) -> tauri::Result<()> {
    let url = if cfg!(debug_assertions) {
        WebviewUrl::External("http://localhost:5173/status.html".parse().unwrap())
    } else {
        WebviewUrl::App("status.html".into())
    };
    let mut builder = WebviewWindowBuilder::new(app, "status", url)
        .title("Wolle — Settings")
        .visible(visible)
        .inner_size(480.0, 360.0);
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
        .on_window_event(|window, event| {
            if let WindowEvent::CloseRequested { api, .. } = event {
                // In tray builds, hide instead of closing when user clicks X
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(win) = app.get_webview_window("main") {
                let _ = win.show();
                let _ = win.set_focus();
            }
        }))
        .invoke_handler(tauri::generate_handler![
            health_check,
            run_action,
            close_app,
            get_start_on_boot,
            set_start_on_boot
        ])
        .setup(|app| {
            // In tray builds, keep the window hidden at startup (start minimized)
            create_main_window(&app.handle(), false)?;
            // No window-level menu; Esc close is handled by the frontend invoking `close_app`.
            // Build a simple tray menu with a status item and actions
            let menu = tauri::menu::MenuBuilder::new(app)
                .text("status", "Checking Ollama...")
                .separator()
                .text("open_status", "Settings")
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
                "open_status" => {
                    if let Some(win) = app_handle.get_webview_window("status") {
                        let _ = win.show();
                        let _ = win.set_focus();
                    } else {
                        let _ = create_status_window(app_handle, true);
                    }
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
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(win) = app.get_webview_window("main") {
                let _ = win.show();
                let _ = win.set_focus();
            }
        }))
        .invoke_handler(tauri::generate_handler![
            health_check,
            run_action,
            close_app,
            get_start_on_boot,
            set_start_on_boot
        ])
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
fn close_app(window: tauri::WebviewWindow) -> Result<(), String> {
    window.hide().map_err(|e| e.to_string())
}

// Without the tray, Esc fully closes the window; if missing, exit the app as a fallback.
#[cfg(not(feature = "tray"))]
#[tauri::command]
fn close_app(window: tauri::WebviewWindow, app: tauri::AppHandle<tauri::Wry>) -> Result<(), String> {
    let _ = window.close();
    app.exit(0);
    Ok(())
}

// Settings: Run on Windows startup (HKCU\...\Run)
#[cfg(target_os = "windows")]
#[tauri::command]
fn get_start_on_boot() -> Result<bool, String> {
    use winreg::enums::HKEY_CURRENT_USER;
    use winreg::RegKey;
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let path = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    match hkcu.open_subkey(path) {
        Ok(key) => match key.get_value::<String, _>("Wolle") {
            Ok(val) => Ok(!val.trim().is_empty()),
            Err(ref e) if e.kind() == std::io::ErrorKind::NotFound => Ok(false),
            Err(e) => Err(e.to_string()),
        },
        Err(ref e) if e.kind() == std::io::ErrorKind::NotFound => Ok(false),
        Err(e) => Err(e.to_string()),
    }
}

#[cfg(not(target_os = "windows"))]
#[tauri::command]
fn get_start_on_boot() -> Result<bool, String> {
    Ok(false)
}

#[cfg(target_os = "windows")]
#[tauri::command]
fn set_start_on_boot(enable: bool) -> Result<(), String> {
    use winreg::enums::HKEY_CURRENT_USER;
    use winreg::RegKey;
    let hkcu = RegKey::predef(HKEY_CURRENT_USER);
    let path = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    let (key, _) = hkcu.create_subkey(path).map_err(|e| e.to_string())?;
    if enable {
        let exe = std::env::current_exe().map_err(|e| e.to_string())?;
        let exe_str = format!("\"{}\"", exe.display());
        key.set_value("Wolle", &exe_str).map_err(|e| e.to_string())
    } else {
        match key.delete_value("Wolle") {
            Ok(_) => Ok(()),
            Err(ref e) if e.kind() == std::io::ErrorKind::NotFound => Ok(()),
            Err(e) => Err(e.to_string()),
        }
    }
}

#[cfg(not(target_os = "windows"))]
#[tauri::command]
fn set_start_on_boot(_enable: bool) -> Result<(), String> {
    Ok(())
}
