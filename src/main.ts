import { el } from './dom'
import { actions, type Action, runAction, healthCheck } from './tauri'

function init(): void {
  const app = document.getElementById('app')
  if (!app) {
    throw new Error(
      'Application container element with id "app" not found in DOM. Ensure index.html contains <div id="app"></div>.'
    )
  }

  const status = el('div', { id: 'status' }, 'Checking Ollama...')
  const input = el('textarea', { id: 'input', rows: 6, cols: 40 })
  const actionSelect = el('select', {})
  actions.forEach((a) =>
    actionSelect.append(el('option', { value: a }, a[0].toUpperCase() + a.slice(1)))
  )
  const runBtn = el('button', {}, 'Run')
  const output = el('pre', { id: 'output' })

  function isAction(value: string): value is Action {
    return (actions as readonly string[]).includes(value)
  }

  async function handleRunClick(): Promise<void> {
    output.textContent = 'Running...'
    runBtn.disabled = true
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
    }
  }

  runBtn.addEventListener('click', () => {
    void handleRunClick()
  })

  app.append(status, actionSelect, input, runBtn, output)

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
