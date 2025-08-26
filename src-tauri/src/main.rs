#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
mod ollama;
mod utils;

#[derive(Serialize)]
struct Health {
  ok: bool,
  message: String,
}

// Use Tauri's built-in SystemTray API when the `tray` feature is enabled.
#[cfg(feature = "tray")]
fn main() {
  use std::thread;
  use tauri::Manager;

  // We create the tray during setup so we can use the App as the Manager
  tauri::Builder::default()
    .invoke_handler(tauri::generate_handler![health_check, run_action])
    .setup(|app| {
      // Build a simple menu with a status item and actions
      let menu = tauri::menu::MenuBuilder::new(app)
        .text("status", "Checking Ollama...")
        .separator()
        .text("show", "Show")
        .text("quit", "Quit")
        .build()?;

      // Create the tray icon with the menu. Icon is optional here.
      let tray = tauri::tray::TrayIconBuilder::new()
        .menu(&menu)
        .build(app)?;

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
  tauri::Builder::default()
    .invoke_handler(tauri::generate_handler![health_check, run_action])
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}

#[tauri::command]
fn health_check() -> Result<Health, String> {
  match ollama::health() {
    Ok(msg) => Ok(Health { ok: true, message: msg }),
    Err(e) => Err(format!("{}", e)),
  }
}

#[tauri::command]
fn run_action(action: String, input: String) -> Result<String, String> {
  // Build a prompt using the helper and forward to ollama
  let prompt = utils::format_prompt(&action, &input);
  ollama::query(&prompt).map_err(|e| format!("{}", e))
}
