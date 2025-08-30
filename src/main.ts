import { el } from './dom'
import {
  actions,
  type Action,
  runAction,
  healthCheck,
  type CliContext,
  type NormalizedPreview,
  ingestPayload,
  // quickAnalyze,
  quickAnalyzeStream,
  takeLastPayload,
} from './tauri'
import { emit, listen } from '@tauri-apps/api/event'
import { setStatusText } from './a11y'
import { copyToClipboard } from './clipboard'
import { emitUiStatus } from './events'
import { setupFluentBase, wireEscToClose } from './ui'

function init(): void {
  // Centralized Fluent base setup
  setupFluentBase()
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

  // Global Esc to close
  wireEscToClose()

  // Emit readiness early: tokens and container are set, so reveal is safe to avoid flicker.
  void emit('frontend-ready').catch((err) => {
    // Swallow in tests/jsdom, but leave a breadcrumb for devs
    if (typeof console !== 'undefined') console.debug('emit(frontend-ready) failed', err)
  })
  const ui = emitUiStatus
  void ui('init')

  const status = el(
    'div',
    { id: 'status', role: 'status', 'aria-live': 'polite' },
    'Checking Ollama...'
  )
  // Collapsible analysis panel (replaces plain source preview)
  const anaDetails = document.createElement('details')
  anaDetails.id = 'analysis'
  const anaSummary = document.createElement('summary')
  anaSummary.textContent = 'Analysis'
  const anaSpinner = document.createElement('fluent-progress-ring')
  anaSpinner.id = 'analysis-spinner'
  anaSpinner.style.marginLeft = '8px'
  anaSpinner.style.verticalAlign = 'middle'
  anaSpinner.style.visibility = 'hidden'
  anaSummary.appendChild(anaSpinner)
  const anaPre = document.createElement('pre')
  anaPre.id = 'analysis-preview'
  anaDetails.append(anaSummary, anaPre)
  // Open by default so users can see analysis status/content
  anaDetails.open = true
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

  app.append(
    status,
    anaDetails,
    inputLabel,
    input,
    actionLabel,
    controlsRow,
    outputHeader,
    output,
    live
  )

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

  const copyOutput = async () => {
    const text = output.textContent ?? ''
    const ok = await copyToClipboard(text)
    live.textContent = ok ? 'Copied to clipboard' : text.trim() ? 'Copy failed' : 'Nothing to copy'
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

  // Optional: page-level theme responsiveness can be added here if needed

  async function check() {
    try {
      const res = await healthCheck()
      setStatusText(status, res.message)
    } catch (e) {
      setStatusText(status, 'Ollama not reachable: ' + (e instanceof Error ? e.message : String(e)))
    }
  }

  // Fire-and-forget status probe. `check()` handles its own errors and does not reject,
  // so `.catch(...)` here would be redundant. Using `void` makes the intent explicit
  // and satisfies no-floating-promises. If console logging is desired, move logging into `check()`.
  void check()

  // (ready emitted earlier to make reveal faster)

  // (Esc handled natively in Rust; no frontend handler here)

  // Unified payload handler to avoid duplicated logic across sources
  const processPayload = async (
    ctx: CliContext,
    source: 'event' | 'pending' | 'poll'
  ): Promise<void> => {
    try {
      setStatusText(
        status,
        `${
          source === 'event'
            ? 'Context received'
            : source === 'pending'
              ? 'Pending payload found'
              : 'Pending payload (polled)'
        } → ingesting…`
      )
      void ui('ingest-start')
      const preview: NormalizedPreview = await ingestPayload(ctx)
      setStatusText(status, 'Ingested → analyzing…')
      void ui('ingest-done', { kind: preview.kind, count: preview.file_count })
      anaPre.textContent = '(working…)'
      anaSpinner.style.visibility = 'visible'
      anaDetails.open = true
      void ui('analyze-start')
      // Streamed analysis via IPC Channel: append chunks as received
      let initial = true
      await quickAnalyzeStream(ctx, ({ chunk, done }) => {
        if (chunk) {
          if (initial && anaPre.textContent === '(working…)') anaPre.textContent = ''
          anaPre.textContent += chunk
          initial = false
        }
        if (done) {
          anaSpinner.style.visibility = 'hidden'
        }
      })
      setStatusText(
        status,
        `${preview.kind === 'text' ? 'Text' : 'Images'} • ${preview.file_count} item(s)`
      )
      void ui('analyze-done')
      input.focus()
    } catch (e) {
      console.warn('Failed to process payload', e)
      const msg = e instanceof Error ? e.message : String(e)
      setStatusText(status, 'Failed during ingest/analyze: ' + msg)
      void ui('error', msg)
      anaSpinner.style.visibility = 'hidden'
    }
  }

  // Accept load-context events from single-instance launches.
  // Wrap in try/catch so tests (no Tauri internals) don't hard-fail.
  try {
    console.debug('registering load-context listener')
    void ui('listener-registered')
    void listen<CliContext>('load-context', (ev) => {
      // Wrap async work to avoid returning a Promise from the listener callback
      void (async () => {
        console.debug('load-context event received', ev)
        void ui('event-received')
        const ctx = ev.payload
        await processPayload(ctx, 'event')
      })()
    }).catch((err) => {
      if (typeof console !== 'undefined') console.debug('listen(load-context) failed', err)
    })
  } catch (err) {
    // In tests/jsdom, @tauri-apps/api/event may not be available; ignore.
    if (typeof console !== 'undefined') console.debug('listen(load-context) skipped', err)
  }

  // Fallback: if a payload was stored before our listener registered, pull and handle it once.
  void (async () => {
    try {
      const pending = await takeLastPayload()
      if (pending) {
        await processPayload(pending, 'pending')
      }
    } catch (e) {
      console.warn('Failed to fetch pending payload', e)
      const msg = e instanceof Error ? e.message : String(e)
      void ui('error', msg)
    }
  })()

  // Additional safety: poll for pending payloads periodically since front-end event listening
  // may be restricted by ACL in some environments. Stop after handling once or after a timeout.
  let pollHandled = false
  let pollTries = 0
  const handlePendingOnce = async () => {
    if (pollHandled) return
    try {
      const pending = await takeLastPayload()
      if (pending) {
        pollHandled = true
        await processPayload(pending, 'poll')
        return true
      }
    } catch (e) {
      console.warn('Polling takeLastPayload failed', e)
    }
    return false
  }
  const pollId = setInterval(() => {
    pollTries++
    void (async () => {
      const done = await handlePendingOnce()
      if (done || pollTries > 120) {
        clearInterval(pollId)
      }
    })()
  }, 1000)
  // Opportunistic check on focus/visibility changes
  window.addEventListener('focus', () => {
    void handlePendingOnce()
  })
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') void handlePendingOnce()
  })

  // If neither event nor pending payload arrives, leave a hint status
  setTimeout(() => {
    if ((status.textContent ?? '').toLowerCase().includes('reachable')) {
      setStatusText(status, 'Waiting for payload…')
      void ui('waiting-for-payload')
    }
  }, 250)
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
