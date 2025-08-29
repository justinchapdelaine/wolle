import { describe, it, expect, vi, beforeEach } from 'vitest'

vi.mock('@tauri-apps/api/core', () => {
  return {
    invoke: vi.fn(),
  }
})

import { actions, runAction, healthCheck, closeApp, getStartOnBoot, setStartOnBoot } from '../tauri'
import { invoke } from '@tauri-apps/api/core'

describe('tauri wrappers', () => {
  const invokeMock = vi.mocked(invoke)
  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('exposes the expected actions', () => {
    expect(actions).toEqual(['summarize', 'rewrite', 'translate'])
  })

  it('runAction calls invoke with correct args', async () => {
    invokeMock.mockResolvedValue('ok')
    const res = await runAction('summarize', 'text')
    expect(res).toBe('ok')
    expect(invoke).toHaveBeenCalledWith('run_action', { action: 'summarize', input: 'text' })
  })

  it('healthCheck calls invoke with correct command', async () => {
    invokeMock.mockResolvedValue('healthy')
    const res = await healthCheck()
    expect(res).toBe('healthy')
    expect(invoke).toHaveBeenCalledWith('health_check')
  })

  it('closeApp calls invoke with correct command', async () => {
    invokeMock.mockResolvedValue(undefined)
    await closeApp()
    expect(invoke).toHaveBeenCalledWith('close_app')
  })

  it('getStartOnBoot calls invoke with correct command', async () => {
    invokeMock.mockResolvedValue(true)
    const v = await getStartOnBoot()
    expect(v).toBe(true)
    expect(invoke).toHaveBeenCalledWith('get_start_on_boot')
  })

  it('setStartOnBoot calls invoke with correct args', async () => {
    invokeMock.mockResolvedValue(undefined)
    await setStartOnBoot(true)
    expect(invoke).toHaveBeenCalledWith('set_start_on_boot', { enable: true })
  })
})
