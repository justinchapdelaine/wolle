import { el } from './dom'
import { actions, type Action, runAction, healthCheck } from './tauri'

const app = document.getElementById('app')
if (!app) {
  throw new Error('#app container not found')
}

const status = el('div', { id: 'status' })
status.textContent = 'Checking Ollama...'
const input = el('textarea', { id: 'input', rows: 6, cols: 40 })
const actionSelect = el('select', {})
actions.forEach((a) =>
  actionSelect.append(el('option', { value: a }, a[0].toUpperCase() + a.slice(1)))
)
const runBtn = el('button', {}, 'Run')
const output = el('pre', { id: 'output' })

runBtn.addEventListener('click', () => {
  output.textContent = 'Running...'
  const action = actionSelect.value as Action
  const text = input.value
  void (async () => {
    try {
      const res = await runAction(action, text)
      output.textContent = res
    } catch (e) {
      output.textContent = 'Error: ' + (e instanceof Error ? e.message : String(e))
    }
  })()
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

void check()
