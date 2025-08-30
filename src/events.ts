import { emit } from '@tauri-apps/api/event'

/**
 * Emit a UI status breadcrumb; never throws.
 */
export async function emitUiStatus(msg: string, data?: unknown): Promise<void> {
  try {
    await emit('ui-status', { msg, data })
  } catch {
    // swallow for environments without tauri event API
  }
}
