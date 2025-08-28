import '../fluent.d.ts'
import { describe, it, expect, beforeEach } from 'vitest'

// Minimal mock of main UI logic's enable/disable behavior
function setup() {
  const input = document.createElement('fluent-text-area')
  const runBtn = document.createElement('fluent-button')

  const updateRunEnabled = () => {
    const val = input.value ?? ''
    runBtn.disabled = val.trim().length === 0
  }

  input.addEventListener('input', updateRunEnabled)
  updateRunEnabled()

  return { input, runBtn, updateRunEnabled }
}

describe('Fluent UI run button enable/disable', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('disables run when input is empty and enables when text is provided', () => {
    const { input, runBtn } = setup()

    // Initially disabled
    expect(runBtn.disabled).toBe(true)

    // Simulate typing
    input.value = 'hello'
    input.dispatchEvent(new Event('input'))
    expect(runBtn.disabled).toBe(false)

    // Clear to whitespace only
    input.value = '   '
    input.dispatchEvent(new Event('input'))
    expect(runBtn.disabled).toBe(true)
  })
})
