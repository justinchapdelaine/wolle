import { el } from './dom'
import { actions, type Action, runAction, healthCheck } from './tauri'
import { emit } from '@tauri-apps/api/event'
import { getCurrentWindow } from '@tauri-apps/api/window'
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
  const outputLabel = el(
    'div',
    { id: 'output-label', role: 'heading', 'aria-level': '2' },
    'Result'
  )
  const output = el('pre', { id: 'output', role: 'region' })
  output.setAttribute('aria-labelledby', 'output-label')

  function isAction(value: string): value is Action {
    return (actions as readonly string[]).includes(value)
  }

  async function handleRunClick(): Promise<void> {
    output.textContent = 'Running...'
    runBtn.disabled = true
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

  app.append(status, inputLabel, input, actionLabel, controlsRow, outputLabel, output)

  // Disable Run when input is empty
  const updateRunEnabled = () => {
    const val = input.value ?? ''
    runBtn.disabled = val.trim().length === 0
  }
  input.addEventListener('input', updateRunEnabled)
  updateRunEnabled()

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

  // Close affordance: Esc closes the window unless focused element is in a context where
  // the user expects Escape to be handled differently. Here we allow Esc globally, but
  // we ignore cases where a native popup could be open (none in our UI) to keep it simple.
  document.addEventListener('keydown', (ev: KeyboardEvent) => {
    if (ev.key === 'Escape') {
      ev.preventDefault()
      // Fire-and-forget; handle errors without making the handler async
      void getCurrentWindow()
        .close()
        .catch((e) => console.warn('Failed to close window:', e))
    }
  })
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
