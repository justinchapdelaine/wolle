import { invoke } from '@tauri-apps/api/core'

export const actions = ['summarize', 'rewrite', 'translate'] as const
export type Action = 'summarize' | 'rewrite' | 'translate' | 'analyze'

export async function runAction(action: Action, input: string): Promise<string> {
  return invoke<string>('run_action', { action, input })
}

export async function healthCheck(): Promise<unknown> {
  return invoke('health_check')
}

export async function closeApp(): Promise<void> {
  return invoke('close_app')
}

// Ingestion: payload from Explorer and preview
export type CliContext =
  | { kind: 'files'; files: string[]; coords?: { x: number; y: number } }
  | { kind: 'images'; images: string[]; coords?: { x: number; y: number } }

export interface NormalizedPreview {
  kind: 'text' | 'images'
  preview: string
  total_bytes: number
  file_count: number
  names: string[]
}

export async function ingestPayload(payload: CliContext): Promise<NormalizedPreview> {
  return await invoke('ingest_payload', { payload })
}

export async function quickAnalyze(payload: CliContext): Promise<string> {
  return await invoke('quick_analyze', { payload })
}

export async function getStartOnBoot(): Promise<boolean> {
  return invoke<boolean>('get_start_on_boot')
}

export async function setStartOnBoot(enable: boolean): Promise<void> {
  return invoke('set_start_on_boot', { enable })
}

// Optional: retrieve a pending payload if backend stored it
export async function takeLastPayload(): Promise<CliContext | null> {
  try {
    const res = await invoke<CliContext | null>('take_last_payload')
    return (res as unknown as CliContext) ?? null
  } catch {
    return null
  }
}
