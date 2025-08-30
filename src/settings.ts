import { getStartOnBoot, setStartOnBoot } from './tauri'

export type Result<T> = { ok: true; value: T } | { ok: false; error: string }

export async function getStartOnBootSafe(): Promise<Result<boolean>> {
  try {
    const value = await getStartOnBoot()
    return { ok: true, value }
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e)
    return { ok: false, error: `Failed to read setting: ${msg}` }
  }
}

export async function setStartOnBootSafe(enable: boolean): Promise<Result<void>> {
  try {
    await setStartOnBoot(enable)
    return { ok: true, value: undefined }
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e)
    return { ok: false, error: `Failed to save setting: ${msg}` }
  }
}
