import { invoke } from '@tauri-apps/api/core'
import { el } from './dom'

const app = document.getElementById('app')
if (!app) {
  throw new Error('#app container not found')
}

const status = el('div', { id: 'status' })
status.textContent = 'Checking Ollama...'
const input = el('textarea', { id: 'input', rows: 6, cols: 40 })
const actionSelect = el('select', {})
actionSelect.append(
  el('option', { value: 'summarize' }, 'Summarize'),
  el('option', { value: 'rewrite' }, 'Rewrite'),
  el('option', { value: 'translate' }, 'Translate')
)
const runBtn = el('button', {}, 'Run')
const output = el('pre', { id: 'output' })

runBtn.addEventListener('click', () => {
  output.textContent = 'Running...'
  const action = actionSelect.value as 'summarize' | 'rewrite' | 'translate'
  const text = input.value
  void (async () => {
    try {
      const res = await invoke<string>('run_action', { action, input: text })
      output.textContent = res
    } catch (e) {
      output.textContent = 'Error: ' + (e instanceof Error ? e.message : String(e))
    }
  })()
})

app.append(status, actionSelect, input, runBtn, output)

async function check() {
  try {
    const res = await invoke('health_check')
    status.textContent = typeof res === 'string' ? res : JSON.stringify(res)
  } catch (e) {
    status.textContent = 'Ollama not reachable: ' + (e instanceof Error ? e.message : String(e))
  }
}

void check()
