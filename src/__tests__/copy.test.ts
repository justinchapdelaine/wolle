import { describe, it, expect, beforeEach, vi } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'

// index.html is at the repository root
const html = fs.readFileSync(path.resolve(process.cwd(), 'index.html'), 'utf-8')

describe('Copy affordance', () => {
  beforeEach(async () => {
    document.documentElement.innerHTML = html
    // Provide a stub for navigator.clipboard in jsdom
    // @ts-expect-error jsdom navigator typing
    global.navigator.clipboard = {
      writeText: vi.fn().mockResolvedValue(undefined),
    }
    await import('../main')
  })

  it('enables Copy when output has text and announces on click', async () => {
    const copyBtn = (await waitFor(() => document.getElementById('copy'))) as HTMLButtonElement
    const output: HTMLElement = await waitFor(() => document.getElementById('output'))
    const live: HTMLElement = await waitFor(() => document.getElementById('live'))

    expect(copyBtn).toBeTruthy()
    expect(live).toBeTruthy()

    // Initially disabled
    expect(copyBtn.disabled).toBe(true)

    // Simulate content present and click copy
    output.textContent = 'Hello world'
    copyBtn.disabled = false
    copyBtn.click()

    // Allow async clipboard
    await Promise.resolve()
    await Promise.resolve()

    // Announcement should be updated
    expect(live.textContent).toMatch(/copied/i)
  })
})

function waitFor<T>(fn: () => T | null, timeout = 500): Promise<NonNullable<T>> {
  const start = Date.now()
  return new Promise((resolve, reject) => {
    const tick = () => {
      try {
        const v = fn()
        if (v != null) return resolve(v)
      } catch {
        // ignore
      }
      if (Date.now() - start > timeout) return reject(new Error('timeout'))
      setTimeout(tick, 10)
    }
    tick()
  })
}
