#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use serde::Serialize;
use std::sync::Mutex;
use std::collections::VecDeque;
use std::sync::atomic::{AtomicBool, Ordering};
mod ollama;
mod utils;
mod ingest;
use tracing_subscriber::{fmt, EnvFilter};
use tauri::{window::Color, WebviewUrl, WebviewWindowBuilder, Manager, Emitter, Listener};
#[cfg(feature = "tray")]
use tauri::WindowEvent;
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

//

/// Compute a native-looking background color for the window based on OS theme (Windows only).
#[cfg(target_os = "windows")]
fn resolve_bg() -> Color {
    if is_light_theme() {
        Color(0xFF, 0xFF, 0xFF, 0xFF)
    } else {
        Color(0x11, 0x11, 0x11, 0xFF)
    }
}

#[cfg(not(feature = "tray"))]
use std::sync::Arc;
use std::io::Cursor;

fn icon_from_ico_best_fit(desired: u32) -> Option<tauri::image::Image<'static>> {
    let ico_bytes = include_bytes!("../../icons/icon.ico");
    let dir = ico::IconDir::read(Cursor::new(ico_bytes)).ok()?;
    let entry = dir
        .entries()
        .iter()
        .min_by_key(|e| {
            let size = u32::from(e.width().max(e.height()));
            if size >= desired { size - desired } else { desired - size }
        })?;
    let img = entry.decode().ok()?;
    let (w, h) = (img.width() as u32, img.height() as u32);
    let boxed: Box<[u8]> = img.rgba_data().to_vec().into_boxed_slice();
    let leaked: &'static mut [u8] = Box::leak(boxed);
    Some(tauri::image::Image::new(leaked, w, h))
}

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone)]
#[serde(tag = "kind", rename_all = "lowercase")]
enum CliContext {
    Files { files: Vec<String> },
    Images { images: Vec<String> },
}

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone, Default)]
struct Coords {
    x: i32,
    y: i32,
}

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone)]
struct LaunchPayload {
    #[serde(flatten)]
    context: CliContext,
    #[serde(default)]
    coords: Option<Coords>,
}

fn parse_cli_json_inner(args: &[String]) -> Result<LaunchPayload, String> {
    // Be tolerant of how Windows/PowerShell may pass the JSON. Try, in order:
    // 1) Any arg that starts with @file => read and parse
    // 2) Any arg that looks like a full JSON blob {...} => parse
    // 3) Join all args and extract the first {...} span => parse
    if args.is_empty() { return Err("no args".into()); }
    fn normalize(s: &str) -> String {
        s.trim().trim_matches('"').trim_matches('\'').to_string()
    }
    // 1) @file in any arg (robust to UTF-8 BOM)
    for a in args {
        let s = normalize(a);
        if let Some(rest) = s.strip_prefix('@') {
            if !rest.trim().is_empty() {
                if let Ok(bytes) = std::fs::read(rest) {
                    let slice = if bytes.starts_with(&[0xEF, 0xBB, 0xBF]) { &bytes[3..] } else { &bytes[..] };
                    if let Ok(txt) = std::str::from_utf8(slice) {
                        if let Ok(v) = serde_json::from_str::<LaunchPayload>(txt.trim()) { return Ok(v); }
                        else { return Err(format!("failed to parse JSON from file {}", rest)); }
                    }
                    else { return Err(format!("file not utf8: {}", rest)); }
                }
                else { return Err(format!("failed to read file: {}", rest)); }
            }
        }
    }
    // 2) Inline JSON in any arg
    for a in args {
        let s = normalize(a);
        if s.starts_with('{') && s.ends_with('}') {
            if let Ok(v) = serde_json::from_str::<LaunchPayload>(&s) { return Ok(v); }
        }
    }
    // 3) Brace scan across joined args
    let joined = args.iter().map(|a| normalize(a)).collect::<Vec<_>>().join(" ");
    if let (Some(b), Some(e)) = (joined.find('{'), joined.rfind('}')) {
        if e > b {
            let slice = &joined[b..=e];
            if let Ok(v) = serde_json::from_str::<LaunchPayload>(slice) { return Ok(v); }
        }
    }
    Err("no JSON payload found".into())
}

fn parse_cli_json(args: &[String]) -> Option<LaunchPayload> {
    parse_cli_json_inner(args).ok()
}

fn clamp_position(app: &tauri::AppHandle<tauri::Wry>, x: i32, y: i32) -> (i32, i32) {
    // Best-effort: clamp to primary monitor work area; if API not available, return as-is.
    // Tauri v2 exposes monitor info via app path APIs; keep it simple here.
    let Ok(Some(primary)) = app.primary_monitor() else { return (x, y) };
    let pos = primary.position(); // PhysicalPosition<i32>
    let size = primary.size();    // PhysicalSize<u32>
    let max_x = pos.x + size.width as i32 - 100; // keep some margin
    let max_y = pos.y + size.height as i32 - 100;
    (x.clamp(pos.x, max_x), y.clamp(pos.y, max_y))
}

fn show_main_with_payload(app: &tauri::AppHandle<tauri::Wry>, payload: LaunchPayload) {
    // Create or fetch the main window
    let win = if let Some(w) = app.get_webview_window("main") {
        w
    } else {
        // Create visible; tray builds start hidden by default but when launched via CLI we should show
        if let Err(e) = create_main_window(app, true) {
            eprintln!("failed to create main window: {e}");
            return;
        }
        if let Some(w) = app.get_webview_window("main") { w } else { return }
    };

    // Position near coordinates if provided
    if let Some(c) = &payload.coords {
        let (cx, cy) = clamp_position(app, c.x, c.y);
        let _ = win.set_position(tauri::PhysicalPosition::new(cx, cy));
    }
    let _ = win.show();
    let _ = win.set_focus();

    // Emit to frontend for hydration
    // Store the last payload so we can re-emit after frontend-ready to avoid races
    if let Some(store) = app.try_state::<PayloadStore>() {
        let mut guard = store.last.lock().unwrap();
        *guard = Some(payload.clone());
    }
    push_log(app, format!(
        "emit load-context; kind={}, files={}, images={}",
        match &payload.context { CliContext::Files{..} => "files", CliContext::Images{..} => "images" },
        match &payload.context { CliContext::Files{files} => files.len(), _ => 0 },
        match &payload.context { CliContext::Images{images} => images.len(), _ => 0 },
    ));
    let _ = win.emit("load-context", payload);
}

#[derive(Default)]
struct PayloadStore {
    last: Mutex<Option<LaunchPayload>>,
    logs: Mutex<VecDeque<String>>,
    activation_args: Mutex<Vec<String>>,
}

fn now_ts() -> u64 {
    use std::time::{SystemTime, UNIX_EPOCH};
    SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_secs()
}

fn push_log(app: &tauri::AppHandle<tauri::Wry>, msg: impl Into<String>) {
    if let Some(store) = app.try_state::<PayloadStore>() {
        let mut q = store.logs.lock().unwrap();
        q.push_back(format!("[{}] {}", now_ts(), msg.into()));
        if q.len() > 200 { let _ = q.pop_front(); }
    }
}

/// Wire "hide-until-ready" behavior:
/// - Listens for the frontend "frontend-ready" event and shows/focuses once.
/// - Adds a 250ms safety timer to reveal if the event is missed.
/// This should be called before creating the window to avoid races.
#[cfg(not(feature = "tray"))]
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
    let win = builder.build()?;
    // Prefer a crisp small icon for the title bar (Windows uses 16px there)
    if let Some(img) = icon_from_ico_best_fit(16) {
        let _ = win.set_icon(img);
    }
    Ok(())
}

/// Create the secondary "status" window.
#[cfg(feature = "tray")]
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
    let win = builder.build()?;
    if let Some(img) = icon_from_ico_best_fit(16) {
        let _ = win.set_icon(img);
    }
    Ok(())
}

#[cfg(feature = "tray")]
fn create_debug_window(app: &tauri::AppHandle<tauri::Wry>, visible: bool) -> tauri::Result<()> {
    let url = if cfg!(debug_assertions) {
        WebviewUrl::External("http://localhost:5173/debug.html".parse().unwrap())
    } else {
        WebviewUrl::App("debug.html".into())
    };
    let mut builder = WebviewWindowBuilder::new(app, "debug", url)
        .title("Wolle — Debug")
        .visible(visible)
        .inner_size(720.0, 520.0);
    #[cfg(target_os = "windows")]
    {
        builder = builder.background_color(resolve_bg());
    }
    let win = builder.build()?;
    if let Some(img) = icon_from_ico_best_fit(16) {
        let _ = win.set_icon(img);
    }
    Ok(())
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

#[tauri::command]
fn take_last_payload(state: tauri::State<PayloadStore>) -> Option<LaunchPayload> {
    let mut guard = state.last.lock().ok()?;
    guard.take()
}

#[derive(Serialize)]
struct DebugSnapshot {
    logs: Vec<String>,
    last_payload: Option<LaunchPayload>,
    activation_args: Vec<String>,
}

#[tauri::command]
fn dbg_snapshot(state: tauri::State<PayloadStore>) -> DebugSnapshot {
    let logs = state.logs.lock().unwrap().iter().cloned().collect::<Vec<_>>();
    let last_payload = state.last.lock().unwrap().clone();
    let activation_args = state.activation_args.lock().unwrap().clone();
    DebugSnapshot { logs, last_payload, activation_args }
}

#[tauri::command]
fn dbg_reemit(app: tauri::AppHandle<tauri::Wry>, state: tauri::State<PayloadStore>) -> Result<bool, String> {
    if let Some(p) = state.last.lock().unwrap().clone() {
        push_log(&app, "dbg_reemit: showing main and emitting payload".to_string());
        show_main_with_payload(&app, p);
        Ok(true)
    } else {
        Ok(false)
    }
}

#[tauri::command]
fn show_main(app: tauri::AppHandle<tauri::Wry>) -> Result<(), String> {
    if let Some(window) = app.get_webview_window("main") {
        window.show().map_err(|e| e.to_string())?;
        window.set_focus().map_err(|e| e.to_string())?
    } else {
        create_main_window(&app, true).map_err(|e| e.to_string())?;
    }
    Ok(())
}

// Use Tauri's built-in SystemTray API when the `tray` feature is enabled.
#[cfg(feature = "tray")]
fn main() {
    // init logging (to stdout/stderr; Tauri captures in dev console)
    let _ = fmt()
        .with_env_filter(EnvFilter::from_default_env())
        .try_init();
    use std::thread;
    // Manager trait already imported at module level

    // We create the window and the tray during setup so we can use the App as the Manager
    tauri::Builder::<tauri::Wry>::new()
        .manage(PayloadStore::default())
        .on_window_event(|window, event| {
            if let WindowEvent::CloseRequested { api, .. } = event {
                // In tray builds, hide instead of closing when user clicks X
                api.prevent_close();
                let _ = window.hide();
            }
        })
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            // _args: full command line after executable; Explorer passes one JSON arg
            // Skip the executable path; parse remainder
            if let Some(store) = app.try_state::<PayloadStore>() {
                *store.activation_args.lock().unwrap() = _args.clone();
            }
            push_log(app, format!("single_instance callback; args={:?} cwd={:?}", _args, _cwd));
            let payload = match parse_cli_json_inner(&_args) {
                Ok(v) => Some(v),
                Err(e) => { push_log(app, format!("single_instance parse error: {}", e)); None }
            };
            if let Some(p) = payload {
                push_log(app, "single_instance parsed payload".to_string());
                show_main_with_payload(app, p);
            } else if let Some(win) = app.get_webview_window("main") {
                let _ = win.show();
                let _ = win.set_focus();
            }
        }))
        .invoke_handler(tauri::generate_handler![
            health_check,
            run_action,
            quick_analyze,
            test_ollama,
            pull_ollama_model,
            close_app,
            get_start_on_boot,
            set_start_on_boot,
            ingest_payload,
            take_last_payload,
            dbg_snapshot,
            dbg_reemit,
            show_main
        ])
        .setup(|app| {
            // In tray builds, keep the window hidden at startup (start minimized)
            create_main_window(&app.handle(), false)?;
            // Log process diagnostics for troubleshooting single-instance routing
            if let Ok(exe) = std::env::current_exe() {
                let pid = std::process::id();
                let build = if cfg!(debug_assertions) { "debug" } else { "release" };
                push_log(app.handle(), format!("startup exe={} pid={} build={}", exe.display(), pid, build));
            }

            // If this is the first process with a JSON payload (Explorer launch), handle it now.
            if let Some(ctx) = {
                let args: Vec<String> = std::env::args().skip(1).collect();
                if let Some(store) = app.try_state::<PayloadStore>() { *store.activation_args.lock().unwrap() = args.clone(); }
                push_log(app.handle(), format!("startup args: {:?}", args));
                match parse_cli_json_inner(&args) { Ok(v) => Some(v), Err(e) => { push_log(app.handle(), format!("startup parse error: {}", e)); None } }
            } {
                push_log(app.handle(), "startup parsed payload".to_string());
                show_main_with_payload(&app.handle(), ctx);
            }
            // No window-level menu; Esc close is handled by the frontend invoking `close_app`.
            // Build a simple tray menu with a status item and actions
            let menu = tauri::menu::MenuBuilder::new(app)
                .text("status", "Checking Ollama...")
                .separator()
                .text("open_settings", "Settings")
                .text("open_debug", "Debug")
                .text("show", "Show")
                .text("quit", "Quit")
                .build()?;

            // Create the tray icon with the menu.
            let tray = tauri::tray::TrayIconBuilder::new().menu(&menu).build(app)?;

            // Explicitly set the tray icon from the bundled icon.ico to keep one source of truth.
            // We pick the smallest available image (closest to 16x16) for a crisp tray.
            let ico_bytes = include_bytes!("../../icons/icon.ico");
            if let Ok(icon) = ico::IconDir::read(std::io::Cursor::new(ico_bytes)) {
                // Choose the smallest image available.
                if let Some(entry) = icon.entries().iter().min_by_key(|e| e.width().max(e.height())) {
                    if let Ok(img) = entry.decode() {
                        let (w, h) = (img.width(), img.height());
                        let rgba = img.rgba_data();
                        // Provide a stable reference; this leaks a tiny buffer (<= 16*16*4 bytes) once per run.
                        let boxed: Box<[u8]> = rgba.to_vec().into_boxed_slice();
                        let leaked: &'static mut [u8] = Box::leak(boxed);
                        let _ = tray.set_icon(Some(tauri::image::Image::new(
                            leaked,
                            w as u32,
                            h as u32,
                        )));
                    }
                }
            }

            // Re-emit pending payload after frontend signals readiness to avoid race
            {
                let app_handle = app.handle().clone();
                app.listen("frontend-ready", move |_e| {
                    push_log(&app_handle, "frontend-ready received; checking for pending payload".to_string());
                    if let Some(win) = app_handle.get_webview_window("main") {
                        if let Some(store) = app_handle.try_state::<PayloadStore>() {
                            let guard = store.last.lock().unwrap();
                            if let Some(p) = guard.as_ref() {
                                push_log(&app_handle, "re-emit load-context after frontend-ready".to_string());
                                let _ = win.emit("load-context", p.clone());
                            }
                        }
                    }
                });
                // Fallback timer: re-emit once after 500ms in case the event was missed
                let app_handle2 = app.handle().clone();
                std::thread::spawn(move || {
                    std::thread::sleep(std::time::Duration::from_millis(500));
                    if let Some(win) = app_handle2.get_webview_window("main") {
                        if let Some(store) = app_handle2.try_state::<PayloadStore>() {
                            let guard = store.last.lock().unwrap();
                            if let Some(p) = guard.as_ref() {
                                let _ = win.emit("load-context", p.clone());
                            }
                        }
                    }
                });
            }

            // Capture ui-status events from any window and store in logs for visibility
            let app_for_ui = app.handle().clone();
            app.listen("ui-status", move |e| {
                // The payload is expected to be { msg, data? }
                let payload = e.payload(); // &str
                if payload.is_empty() {
                    push_log(&app_for_ui, "ui-status: <empty>".to_string());
                } else {
                    push_log(&app_for_ui, format!("ui-status: {}", payload));
                }
            });

            // Register menu event handler
            tray.on_menu_event(|app_handle, event| match event.id().as_ref() {
                "quit" => {
                    std::process::exit(0);
                }
                "open_settings" => {
                    if let Some(win) = app_handle.get_webview_window("status") {
                        let _ = win.show();
                        let _ = win.set_focus();
                    } else {
                        let _ = create_status_window(app_handle, true);
                    }
                }
                "open_debug" => {
                    if let Some(win) = app_handle.get_webview_window("debug") {
                        let _ = win.show();
                        let _ = win.set_focus();
                    } else {
                        let _ = create_debug_window(app_handle, true);
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
        .manage(PayloadStore::default())
        .plugin(tauri_plugin_single_instance::init(|app, _args, _cwd| {
            if let Some(win) = app.get_webview_window("main") {
                let _ = win.show();
                let _ = win.set_focus();
            }
        }))
        .invoke_handler(tauri::generate_handler![
            health_check,
            run_action,
            quick_analyze,
            test_ollama,
            close_app,
            get_start_on_boot,
            set_start_on_boot,
            ingest_payload,
            take_last_payload,
            show_main
        ])
        .setup(|app| {
            // Prepare hide-until-ready signaling BEFORE building the webview
            let shown = Arc::new(AtomicBool::new(false));
            wire_hide_until_ready(&app.handle(), &shown);
            // Create the window hidden to avoid any flash
            create_main_window(&app.handle(), false)?;
            // Re-emit pending payload after frontend signals readiness
            {
                let app_handle = app.handle().clone();
                app.listen("frontend-ready", move |_e| {
                    if let Some(win) = app_handle.get_webview_window("main") {
                        if let Some(store) = app_handle.try_state::<PayloadStore>() {
                            let guard = store.last.lock().unwrap();
                            if let Some(p) = guard.as_ref() {
                                let _ = win.emit("load-context", p.clone());
                            }
                        }
                    }
                });
            }
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
async fn run_action(action: String, input: String) -> Result<String, String> {
    // Offload potentially blocking work to a background thread
    let res = tauri::async_runtime::spawn_blocking(move || {
        let prompt = utils::format_prompt(&action, &input);
        ollama::query(&prompt)
            .map_err(|e| format!("Action '{}' failed: {}", action, e))
    })
    .await
    .map_err(|e| format!("join error: {}", e))?;
    res
}

#[tauri::command]
fn ingest_payload(payload: ingest::LaunchPayload) -> Result<ingest::NormalizedPreview, String> {
    // Log basic ingest info for debugging
    // We can't access AppHandle here directly; use a small closure invoked via tauri::async_runtime::spawn if needed.
    // For simplicity, just perform ingest and rely on quick_analyze logs for now.
    ingest::ingest(payload).map_err(|e| e.to_string())
}

#[tauri::command]
async fn quick_analyze(app: tauri::AppHandle<tauri::Wry>, payload: ingest::LaunchPayload) -> Result<String, String> {
    use ingest::{prepare_analysis, AnalysisSource};
    let _guard = AnalysisBusyGuard::try_acquire()?;

    // Offload all potentially blocking work to a background thread
    let app_clone = app.clone();
    let res = tauri::async_runtime::spawn_blocking(move || {
        // Short-circuit if Ollama isn't reachable
        if let Err(e) = crate::ollama::health() {
            push_log(&app_clone, format!("quick_analyze: ollama not reachable: {}", e));
            return Err(e.to_string());
        }

        let src = match prepare_analysis(payload) {
            Ok(s) => s,
            Err(e) => {
                push_log(&app_clone, format!("quick_analyze: prepare_analysis failed: {}", e));
                return Err(e.to_string());
            }
        };
        match &src {
            AnalysisSource::Text { text, names } => {
                push_log(&app_clone, format!("quick_analyze: prepared text; bytes={}, files={}", text.len(), names.len()));
            }
            AnalysisSource::Images { images_b64, names } => {
                push_log(&app_clone, format!("quick_analyze: prepared images; count={}, names={}", images_b64.len(), names.len()));
            }
        }
        // Build a concise analysis prompt that explains what the payload is
        let prompt = match src {
            AnalysisSource::Text { text, names } => {
                let header = if names.is_empty() {
                    "Analyze the following text. Describe briefly what it contains in 1-2 sentences.".to_string()
                } else {
                    format!(
                        "Analyze the following text from files ({}). Describe briefly what it contains in 1-2 sentences.",
                        names.join(", ")
                    )
                };
                format!("{}\n\n---\n\n{}", header, text)
            }
            AnalysisSource::Images { images_b64: _, names } => {
                // For vision input, we'll include a short textual instruction and send images separately in the body.
                // For now, we fall back to a text-only summary indicating which images are present. In step 4 we can call the vision path if required.
                format!(
                    "Analyze the provided images ({}). Briefly describe what they contain in 1-2 sentences.",
                    names.join(", ")
                )
            }
        };
        push_log(&app_clone, "quick_analyze: querying ollama".to_string());
        match ollama::query(&prompt) {
            Ok(s) => {
                push_log(&app_clone, "quick_analyze: success".to_string());
                Ok(s)
            }
            Err(e) => {
                // Include nested error causes when available
                let mut msg = format!("{}", e);
                let mut source = e.source();
                while let Some(cause) = source {
                    msg.push_str(&format!("; caused by: {}", cause));
                    source = cause.source();
                }
                push_log(&app_clone, format!("quick_analyze: error: {}", msg));
                Err(msg)
            }
        }
    })
    .await
    .map_err(|e| format!("join error: {}", e))?;

    res
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

// Global analysis busy guard
static ANALYSIS_BUSY: AtomicBool = AtomicBool::new(false);

struct AnalysisBusyGuard;
impl AnalysisBusyGuard {
    fn try_acquire() -> Result<AnalysisBusyGuard, String> {
        if ANALYSIS_BUSY.swap(true, Ordering::SeqCst) {
            Err("analysis already in progress".into())
        } else {
            Ok(AnalysisBusyGuard)
        }
    }
}
impl Drop for AnalysisBusyGuard {
    fn drop(&mut self) {
        ANALYSIS_BUSY.store(false, Ordering::SeqCst);
    }
}

#[tauri::command]
async fn test_ollama() -> Result<String, String> {
    tauri::async_runtime::spawn_blocking(|| crate::ollama::query("Say OK.").map_err(|e| e.to_string()))
        .await
        .map_err(|e| format!("join error: {}", e))?
}

#[tauri::command]
async fn pull_ollama_model(model: Option<String>) -> Result<String, String> {
    let m = model.unwrap_or_else(|| "gemma3:4b".to_string());
    tauri::async_runtime::spawn_blocking(move || crate::ollama::pull_model(&m).map_err(|e| e.to_string()))
        .await
        .map_err(|e| format!("join error: {}", e))?
}
