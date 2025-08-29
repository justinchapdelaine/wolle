import { el } from './dom'
import { actions, type Action, runAction, healthCheck, closeApp } from './tauri'
import { emit } from '@tauri-apps/api/event'
import {
  provideFluentDesignSystem,
  allComponents,
  density,
  baseLayerLuminance,
  StandardLuminance,
  controlCornerRadius,
  bodyFont,
  // foregroundOnAccentRest,
  // neutralForegroundRest,
  // neutralFillRest,
  // neutralStrokeRest,
} from '@fluentui/web-components'

function init(): void {
  // Register Fluent components once and set core design tokens
  provideFluentDesignSystem().register(allComponents)
  // Compact layout: negative density yields smaller controls; adjust to taste (-1, -2)
  density.withDefault(-1)
  // Set initial luminance based on OS theme BEFORE building UI to avoid token flip
  const media = window.matchMedia('(prefers-color-scheme: dark)')
  const initialMode = media.matches ? StandardLuminance.DarkMode : StandardLuminance.LightMode
  baseLayerLuminance.setValueFor(document.documentElement, initialMode)
  // Brand hooks: subtle corner radius tuning; accent color can be provided by CSS var
  document.documentElement.style.setProperty('--accent-base-color', '#2563eb')
  controlCornerRadius.withDefault(6)
  // Typography and surface/foreground tokens
  bodyFont.withDefault(
    "system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', sans-serif"
  )
  // Foreground & neutrals can be left to Fluent defaults; leave hooks here if needed later
  // Examples of reading/writing CSS vars to tokens if desired:
  // neutralForegroundRest.withDefault('#111')
  // neutralFillRest.withDefault('#fff')
  // neutralStrokeRest.withDefault('#ddd')

  // No global stylesheet: rely on Fluent tokens (density, luminance) and semantic structure for layout
  const app = document.getElementById('app')
  if (!app) {
    throw new Error(
      'Application container element with id "app" not found in DOM. Ensure index.html contains <div id="app"></div>.'
    )
  }

  // Ensure Escape closes the window from the frontend as well (belt-and-suspenders)
  // Listen at capture phase so inner components can't swallow it.
  window.addEventListener(
    'keydown',
    (ev: KeyboardEvent) => {
      if (ev.key === 'Escape') {
        ev.preventDefault()
        ev.stopImmediatePropagation()
        void closeApp().catch((e) => console.warn('Failed to close app:', e))
      }
    },
    { capture: true }
  )

  // Emit readiness early: tokens and container are set, so reveal is safe to avoid flicker.
  void emit('frontend-ready')

  const status = el(
    'div',
    { id: 'status', role: 'status', 'aria-live': 'polite' },
    'Checking Ollama...'
  )
  const inputLabel = el('label', { id: 'input-label' }, 'Prompt')
  const input = document.createElement('fluent-text-area')
  input.setAttribute('id', 'input')
  input.setAttribute('aria-labelledby', 'input-label')
  input.setAttribute('rows', '6')

  const actionLabel = el('label', { id: 'action-label' }, 'Action')
  const actionSelect = document.createElement('fluent-select')
  actionSelect.setAttribute('id', 'action')
  actionSelect.setAttribute('aria-labelledby', 'action-label')
  actions.forEach((a) => {
    const opt = document.createElement('fluent-option')
    opt.setAttribute('value', a)
    opt.textContent = a[0].toUpperCase() + a.slice(1)
    actionSelect.appendChild(opt)
  })

  const runBtn = document.createElement('fluent-button')
  runBtn.textContent = 'Run'
  const spinner = document.createElement('fluent-progress-ring')
  spinner.setAttribute('aria-hidden', 'true')
  spinner.style.visibility = 'hidden'
  const outputLabel = el(
    'div',
    { id: 'output-label', role: 'heading', 'aria-level': '2' },
    'Result'
  )
  const output = el('pre', { id: 'output', role: 'region' })
  output.setAttribute('aria-labelledby', 'output-label')

  // Copy affordance and live region for announcements
  const copyBtn = document.createElement('fluent-button')
  copyBtn.textContent = 'Copy'
  copyBtn.setAttribute('id', 'copy')
  copyBtn.disabled = true
  const live = document.createElement('div')
  live.id = 'live'
  live.setAttribute('role', 'status')
  live.setAttribute('aria-live', 'polite')
  live.setAttribute('aria-atomic', 'true')
  // Visually hide but keep accessible
  live.style.position = 'absolute'
  live.style.left = '-9999px'
  live.style.top = 'auto'
  live.style.width = '1px'
  live.style.height = '1px'
  live.style.overflow = 'hidden'

  function isAction(value: string): value is Action {
    return (actions as readonly string[]).includes(value)
  }

  async function handleRunClick(): Promise<void> {
    output.textContent = 'Running...'
    runBtn.disabled = true
    copyBtn.disabled = true
    spinner.style.visibility = 'visible'
    const selected = actionSelect.value
    if (!isAction(selected)) {
      output.textContent = 'Error: Unknown action selected.'
      runBtn.disabled = false
      return
    }
    const action: Action = selected
    const text = input.value
    try {
      const res = await runAction(action, text)
      output.textContent = res
    } catch (e) {
      output.textContent = 'Error: ' + (e instanceof Error ? e.message : String(e))
    } finally {
      runBtn.disabled = false
      spinner.style.visibility = 'hidden'
      updateCopyEnabled()
    }
  }

  runBtn.addEventListener('click', () => {
    void handleRunClick()
  })

  // Focus prompt on open for quick typing
  input.focus()

  // Keyboard affordances:
  // - Enter to run when enabled (common for quick actions)
  // - Shift+Enter inserts a newline
  // - Ctrl/Cmd+Enter also runs
  input.addEventListener('keydown', (ev: KeyboardEvent) => {
    if (ev.key !== 'Enter') return
    if (ev.shiftKey) return // allow newline
    // If plain Enter or with Ctrl/Cmd pressed, run the action when enabled
    ev.preventDefault()
    if (!runBtn.disabled) void handleRunClick()
  })

  const controlsRow = el('div', { id: 'controls' })
  controlsRow.append(actionSelect, runBtn, spinner)

  // Layout spacing intentionally minimal; Fluent density token provides compact controls.

  // Output header row: label + copy button
  const outputHeader = el('div', { id: 'output-header' })
  outputHeader.append(outputLabel, copyBtn)

  app.append(status, inputLabel, input, actionLabel, controlsRow, outputHeader, output, live)

  // Disable Run when input is empty
  const updateRunEnabled = () => {
    const val = input.value ?? ''
    runBtn.disabled = val.trim().length === 0
  }
  input.addEventListener('input', updateRunEnabled)
  updateRunEnabled()

  // Enable/disable Copy based on output content
  const updateCopyEnabled = () => {
    const text = (output.textContent ?? '').trim()
    copyBtn.disabled = text.length === 0 || text === 'Running...'
  }
  updateCopyEnabled()

  // Clipboard copy with graceful fallbacks and announcement
  const copyOutput = async () => {
    const text = output.textContent ?? ''
    if (!text.trim()) {
      live.textContent = 'Nothing to copy'
      return
    }
    try {
      if (navigator.clipboard && (window.isSecureContext ?? false)) {
        await navigator.clipboard.writeText(text)
      } else {
        // Fallback to hidden textarea
        const ta = document.createElement('textarea')
        ta.value = text
        ta.setAttribute('readonly', '')
        ta.style.position = 'absolute'
        ta.style.left = '-9999px'
        document.body.appendChild(ta)
        ta.select()
        const ok = document.execCommand('copy')
        document.body.removeChild(ta)
        if (!ok) throw new Error('execCommand copy failed')
      }
      live.textContent = 'Copied to clipboard'
    } catch {
      live.textContent = 'Copy failed'
    }
  }
  copyBtn.addEventListener('click', () => {
    void copyOutput()
  })

  // Make sure the document/input gains focus when the window is focused or made visible
  window.addEventListener('focus', () => {
    input.focus()
  })
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') input.focus()
  })

  // Theme responsiveness: follow OS light/dark and update luminance token dynamically
  const applyLuminance = () => {
    const mode = media.matches ? StandardLuminance.DarkMode : StandardLuminance.LightMode
    // Set on the document element to apply globally
    baseLayerLuminance.setValueFor(document.documentElement, mode)
  }
  // Listen for OS theme changes
  media.addEventListener?.('change', applyLuminance)

  async function check() {
    try {
      const res = await healthCheck()
      status.textContent = typeof res === 'string' ? res : JSON.stringify(res)
    } catch (e) {
      status.textContent = 'Ollama not reachable: ' + (e instanceof Error ? e.message : String(e))
    }
  }

  // Fire-and-forget status probe. `check()` handles its own errors and does not reject,
  // so `.catch(...)` here would be redundant. Using `void` makes the intent explicit
  // and satisfies no-floating-promises. If console logging is desired, move logging into `check()`.
  void check()

  // (ready emitted earlier to make reveal faster)

  // (Esc handled natively in Rust; no frontend handler here)
}

// Minimal DOM-ready fallback: if #app is missing while the document is still loading,
// delay initialization until DOMContentLoaded. Otherwise, run immediately.
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    try {
      init()
    } catch (e) {
      // Surface init error to console; in this phase we don't have app container for UI output
      // and this should fail fast for developers.
      console.error(e)
    }
  })
} else {
  init()
}
