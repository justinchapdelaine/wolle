import { invoke } from '@tauri-apps/api/tauri'
import { el } from './dom'

const app = document.getElementById('app')

const status = el('div', { id: 'status' }, 'Checking Ollama...')
const input = el('textarea', { id: 'input', rows: 6, cols: 40 })
const actionSelect = el('select', {},
  el('option', { value: 'summarize' }, 'Summarize'),
  el('option', { value: 'rewrite' }, 'Rewrite'),
  el('option', { value: 'translate' }, 'Translate'),
)
const runBtn = el('button', {}, 'Run')
const output = el('pre', { id: 'output' })

runBtn.addEventListener('click', async () => {
  output.textContent = 'Running...'
  const action = actionSelect.value
  const text = input.value
  try {
    const res = await invoke('run_action', { action, input: text })
    output.textContent = res
  } catch (e) {
    output.textContent = 'Error: ' + e
  }
})

app.append(status, actionSelect, input, runBtn, output)

async function check() {
  try {
    const res = await invoke('health_check')
    status.textContent = typeof res === 'string' ? res : JSON.stringify(res)
  } catch (e) {
    status.textContent = 'Ollama not reachable: ' + e
  }
}

check()
