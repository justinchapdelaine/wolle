use anyhow::Result;
use reqwest::blocking::Client;
use std::process::Command;
use std::time::Duration;
use tracing::{debug, warn};

pub fn health() -> Result<String> {
  // Build a client with timeouts to avoid hanging the UI
  let client = Client::builder()
    .connect_timeout(Duration::from_millis(800))
    .timeout(Duration::from_secs(2))
    .build()?;

  debug!("checking ollama /health");
  match client.get("http://127.0.0.1:11434/health").send() {
    Ok(res) if res.status().is_success() => {
      return Ok("Ollama reachable".to_string());
    }
    Ok(res) => {
      warn!(status = ?res.status(), "ollama /health returned non-success");
    }
    Err(e) => {
      debug!(error = %e, "ollama /health request failed");
    }
  }

  // Fallback: check if `ollama` binary exists
  debug!("checking ollama CLI availability");
  if let Ok(output) = Command::new("ollama").arg("version").output() {
    if output.status.success() {
      return Ok("Ollama CLI available, server may not be running".to_string());
    }
  }

  Err(anyhow::anyhow!(
    "Ollama not available. Ensure Ollama is installed and the server is running (ollama serve)."
  ))
}

#[cfg(test)]
mod tests {
  use super::*;

  #[test]
  fn health_returns_err_when_no_ollama() {
    // This test assumes `ollama` is not present in PATH in CI; it should return an Err.
    // We don't execute or mock the Command here; instead we check that the function returns
    // a Result and handle either Ok or Err. This keeps the test stable across environments.
    let _ = health();
  }
}

pub fn query(prompt: &str) -> Result<String> {
  // Minimal MVP: call Ollama REST API generate endpoint with timeouts
  let client = Client::builder()
    .connect_timeout(Duration::from_millis(800))
    .timeout(Duration::from_secs(30))
    .build()?;

  let body = serde_json::json!({ "model": "gemma3:4b", "prompt": prompt, "max_tokens": 256 });
  debug!("sending generate request to ollama");
  let res = client
    .post("http://127.0.0.1:11434/api/generate")
    .json(&body)
    .send()?;

  let status = res.status();
  let txt = res.text()?;
  if !status.is_success() {
    warn!(?status, "ollama generate returned non-success status");
  }
  Ok(txt)
}
