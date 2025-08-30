/**
 * Copy plain text to clipboard with a safe fallback for environments
 * where navigator.clipboard is unavailable.
 * Returns true on success, false on failure.
 */
export async function copyToClipboard(text: string): Promise<boolean> {
  const trimmed = text?.toString() ?? ''
  if (!trimmed.trim()) return false
  try {
    if (
      typeof navigator !== 'undefined' &&
      navigator.clipboard &&
      typeof (navigator.clipboard as unknown as { writeText?: unknown }).writeText === 'function'
    ) {
      await navigator.clipboard.writeText(trimmed)
      return true
    }
  } catch {
    // fall through to fallback
  }

  // Fallback to hidden textarea + execCommand
  try {
    const ta = document.createElement('textarea')
    ta.value = trimmed
    ta.setAttribute('readonly', '')
    ta.style.position = 'absolute'
    ta.style.left = '-9999px'
    document.body.appendChild(ta)
    ta.select()
    const ok = document.execCommand('copy')
    document.body.removeChild(ta)
    return ok
  } catch {
    return false
  }
}
