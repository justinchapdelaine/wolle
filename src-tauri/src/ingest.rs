use anyhow::{anyhow, Result};
use base64::Engine;
use std::fs;
use std::path::Path;

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone)]
#[serde(tag = "kind", rename_all = "lowercase")]
pub enum CliContext {
    Files { files: Vec<String> },
    Images { images: Vec<String> },
}

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone, Default)]
pub struct Coords {
    pub x: i32,
    pub y: i32,
}

#[derive(serde::Deserialize, serde::Serialize, Debug, Clone)]
pub struct LaunchPayload {
    #[serde(flatten)]
    pub context: CliContext,
    #[serde(default)]
    pub coords: Option<Coords>,
}

#[derive(serde::Serialize, Debug, Clone)]
pub struct NormalizedPreview {
    pub kind: String,            // "text" | "images"
    pub preview: String,         // small snippet for UI
    pub total_bytes: usize,      // total text bytes or total image bytes
    pub file_count: usize,
    pub names: Vec<String>,
}

const MAX_TEXT_BYTES: usize = 1_000_000; // ~1MB pre-ingest soft cap
const PREVIEW_CHARS: usize = 800;        // preview snippet length
const MAX_IMAGES: usize = 6;             // image cap per request

fn is_text_like(path: &Path) -> bool {
    let ext = path.extension().and_then(|s| s.to_str()).unwrap_or("").to_lowercase();
    matches!(
        ext.as_str(),
        "txt" | "md" | "log" | "json" | "csv" | "toml" | "yaml" | "yml" | "ini"
    )
}

fn read_text_file(path: &Path) -> Result<String> {
    let data = fs::read(path)?;
    // naive UTF-8 with BOM strip
    let s = if data.starts_with(&[0xEF, 0xBB, 0xBF]) {
        String::from_utf8(data[3..].to_vec())?
    } else {
        String::from_utf8(data)?
    };
    Ok(s)
}

fn read_docx_text(path: &Path) -> Result<String> {
    // Minimal DOCX text extraction: find word/document.xml and strip tags.
    let file = fs::File::open(path)?;
    let mut zip = zip::ZipArchive::new(file)?;
    let mut doc = zip.by_name("word/document.xml").map_err(|_| anyhow!("DOCX missing document.xml"))?;
    let mut xml = String::new();
    use std::io::Read;
    doc.read_to_string(&mut xml)?;
    // Strip very coarsely: remove <...> tags
    let mut out = String::with_capacity(xml.len());
    let mut in_tag = false;
    for ch in xml.chars() {
        match ch {
            '<' => in_tag = true,
            '>' => in_tag = false,
            _ if !in_tag => out.push(ch),
            _ => {}
        }
    }
    Ok(out)
}

fn read_pdf_text(path: &Path) -> Result<String> {
    // Best-effort extractor using lopdf + pdf-extract-lite approach could be added later.
    // For now, use pdf_extract crate as a simple path (text layer only).
    let text = pdf_extract::extract_text(path).map_err(|e| anyhow!("pdf extract failed: {}", e))?;
    Ok(text)
}

fn is_docx(path: &Path) -> bool {
    path.extension().and_then(|s| s.to_str()).unwrap_or("").eq_ignore_ascii_case("docx")
}

fn is_pdf(path: &Path) -> bool {
    path.extension().and_then(|s| s.to_str()).unwrap_or("").eq_ignore_ascii_case("pdf")
}

pub fn ingest(payload: LaunchPayload) -> Result<NormalizedPreview> {
    match payload.context {
        CliContext::Images { images } => {
            let mut names = Vec::new();
            let mut total = 0usize;
            for (i, p) in images.iter().enumerate() {
                if i >= MAX_IMAGES { break; }
                let path = Path::new(p);
                names.push(path.file_name().and_then(|s| s.to_str()).unwrap_or(p).to_string());
                total += fs::metadata(path).map(|m| m.len() as usize).unwrap_or(0);
            }
            let preview = format!("{} image(s): {}", names.len(), names.join(", "));
            Ok(NormalizedPreview { kind: "images".into(), preview, total_bytes: total, file_count: names.len(), names })
        }
        CliContext::Files { files } => {
            let mut buf = String::new();
            let mut names = Vec::new();
            let mut total = 0usize;
            let mut count = 0usize;
            for p in files {
                let path = Path::new(&p);
                names.push(path.file_name().and_then(|s| s.to_str()).unwrap_or(&p).to_string());
                let text = if is_docx(path) {
                    read_docx_text(path)?
                } else if is_pdf(path) {
                    read_pdf_text(path)?
                } else if is_text_like(path) {
                    read_text_file(path)?
                } else {
                    // Skip unknown types in this phase
                    continue;
                };
                total += text.len();
                if buf.len() + text.len() <= MAX_TEXT_BYTES {
                    if !buf.is_empty() { buf.push_str("\n\n---\n\n"); }
                    buf.push_str(&text);
                }
                count += 1;
            }
            let mut preview = buf.chars().take(PREVIEW_CHARS).collect::<String>();
            if buf.len() > PREVIEW_CHARS { preview.push_str("\n…"); }
            Ok(NormalizedPreview { kind: "text".into(), preview, total_bytes: total, file_count: count, names })
        }
    }
}

#[derive(serde::Serialize, Debug, Clone)]
pub enum AnalysisSource {
    Text { text: String, names: Vec<String> },
    Images { images_b64: Vec<String>, names: Vec<String> },
}

const ANALYSIS_TEXT_LIMIT: usize = 200_000; // 200KB of text for quick analysis
const ANALYSIS_IMAGE_CAP: usize = 3;

pub fn prepare_analysis(payload: LaunchPayload) -> Result<AnalysisSource> {
    match payload.context {
        CliContext::Files { files } => {
            let mut buf = String::new();
            let mut names = Vec::new();
            for p in files {
                let path = Path::new(&p);
                names.push(path.file_name().and_then(|s| s.to_str()).unwrap_or(&p).to_string());
                let text = if is_docx(path) {
                    read_docx_text(path)?
                } else if is_pdf(path) {
                    read_pdf_text(path)?
                } else if is_text_like(path) {
                    read_text_file(path)?
                } else {
                    continue;
                };
                if buf.len() < ANALYSIS_TEXT_LIMIT {
                    if !buf.is_empty() { buf.push_str("\n\n---\n\n"); }
                    let remaining = ANALYSIS_TEXT_LIMIT - buf.len();
                    buf.push_str(&text.chars().take(remaining).collect::<String>());
                }
            }
            Ok(AnalysisSource::Text { text: buf, names })
        }
        CliContext::Images { images } => {
            let mut names = Vec::new();
            let mut out = Vec::new();
            for (i, p) in images.iter().enumerate() {
                if i >= ANALYSIS_IMAGE_CAP { break; }
                let path = Path::new(p);
                names.push(path.file_name().and_then(|s| s.to_str()).unwrap_or(p).to_string());
                let bytes = fs::read(path)?;
                let b64 = base64::engine::general_purpose::STANDARD.encode(bytes);
                out.push(b64);
            }
            Ok(AnalysisSource::Images { images_b64: out, names })
        }
    }
}
