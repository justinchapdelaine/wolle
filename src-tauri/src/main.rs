#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
mod ollama;
mod utils;

#[derive(Serialize)]
struct Health {
  ok: bool,
  message: String,
}

// When the `tray` feature is enabled, include tray support. Otherwise use a simple main so
// CI and tests that don't require the system tray can still build.
#[cfg(feature = "tray")]
mod tray_main {
  use super::*;
  use std::sync::{Arc, Mutex};
  use tray_icon::{menu::Menu, menu::MenuItem, TrayIconBuilder};

  pub fn run() {
    // Create a simple menu with a status (disabled), show and quit
    // Build initial menu correctly using the tray-icon (muda) API.
    let status = MenuItem::with_id("status", "Checking Ollama...", false, None);
    let show = MenuItem::new("Show", true, None);
    let quit = MenuItem::new("Quit", true, None);

    let menu = {
      let m = Menu::new();
      // append returns Result<(), Error>
      let _ = m.append(&status);
      let _ = m.append(&show);
      let _ = m.append(&quit);
      m
    };

    // Build the tray icon. The icon file is optional; omit to use default.
    let tray = TrayIconBuilder::new()
      .with_menu(Box::new(menu))
      .build()
      .expect("failed to build tray icon");

    // Wrap tray in an Arc<Mutex<...>> so we can access it from the run event loop.
    let tray_handle = Arc::new(Mutex::new(tray));

    // Create a channel: background thread will send status strings to the main
    // thread where Tauri's event loop runs. The TrayIcon is not Send, so we
    // must update it on the main thread.
    let (tx, rx) = std::sync::mpsc::channel::<String>();

    // Spawn a background thread that periodically checks Ollama and sends
    // status updates over the channel.
    std::thread::spawn(move || {
      loop {
        let status_text = match ollama::health() {
          Ok(_) => "Ollama: OK".to_string(),
          Err(e) => format!("Ollama: {}", e),
        };

        let _ = tx.send(status_text);
        std::thread::sleep(std::time::Duration::from_secs(30));
      }
    });

    // Run the Tauri app and process events. The run callback executes on the
    // main thread so it's safe to lock and update the TrayIcon here.
    use tauri::RunEvent;

    let context = tauri::generate_context!();
    let mut app = tauri::Builder::default()
      .invoke_handler(tauri::generate_handler![health_check, run_action])
      .build(context)
      .expect("error while building tauri application");

    app.run(move |_app_handle, event| {
      // On every event, attempt to read a status update and apply it to the tray.
      if let Ok(status_text) = rx.try_recv() {
        if let Ok(t) = tray_handle.lock() {
          // Rebuild menu with updated status
          let status = MenuItem::with_id("status", status_text.as_str(), false, None);
          let show = MenuItem::new("Show", true, None);
          let quit = MenuItem::new("Quit", true, None);

          let menu = {
            let m = Menu::new();
            let _ = m.append(&status);
            let _ = m.append(&show);
            let _ = m.append(&quit);
            m
          };

          let _ = t.set_menu(Some(Box::new(menu)));
        }
      }

      match event {
        RunEvent::ExitRequested { api, .. } => {
          // Do nothing to allow the exit to proceed. If you want to prevent
          // exit, call `api.prevent_exit()` here instead.
        }
        _ => {}
      }
    });
  }
}

#[cfg(not(feature = "tray"))]
fn main() {
  tauri::Builder::default()
    .invoke_handler(tauri::generate_handler![health_check, run_action])
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}

#[cfg(feature = "tray")]
fn main() {
  tray_main::run()
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
