import { describe, it, expect, vi, beforeEach } from 'vitest'

vi.mock('@tauri-apps/api/core', () => {
  return {
    invoke: vi.fn(),
  }
})

import { actions, runAction, healthCheck } from '../tauri'
import { invoke } from '@tauri-apps/api/core'

describe('tauri wrappers', () => {
  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('exposes the expected actions', () => {
    expect(actions).toEqual(['summarize', 'rewrite', 'translate'])
  })

  it('runAction calls invoke with correct args', async () => {
    ;(invoke as unknown as ReturnType<typeof vi.fn>).mockResolvedValue('ok')
    const res = await runAction('summarize', 'text')
    expect(res).toBe('ok')
    expect(invoke).toHaveBeenCalledWith('run_action', { action: 'summarize', input: 'text' })
  })

  it('healthCheck calls invoke with correct command', async () => {
    ;(invoke as unknown as ReturnType<typeof vi.fn>).mockResolvedValue('healthy')
    const res = await healthCheck()
    expect(res).toBe('healthy')
    expect(invoke).toHaveBeenCalledWith('health_check')
  })
})
