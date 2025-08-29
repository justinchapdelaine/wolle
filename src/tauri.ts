import { invoke } from '@tauri-apps/api/core'

export const actions = ['summarize', 'rewrite', 'translate'] as const
export type Action = (typeof actions)[number]

export async function runAction(action: Action, input: string): Promise<string> {
  return invoke<string>('run_action', { action, input })
}

export async function healthCheck(): Promise<unknown> {
  return invoke('health_check')
}

export async function closeApp(): Promise<void> {
  return invoke('close_app')
}

export async function getStartOnBoot(): Promise<boolean> {
  return invoke<boolean>('get_start_on_boot')
}

export async function setStartOnBoot(enable: boolean): Promise<void> {
  return invoke('set_start_on_boot', { enable })
}
