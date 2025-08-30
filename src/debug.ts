import { invoke } from '@tauri-apps/api/core'
import { listen, type Event } from '@tauri-apps/api/event'
import { el } from './dom'

function init() {
  const app = document.getElementById('app')!
  const title = el('h2', {}, 'Debug')
  const refreshBtn = el('button', { id: 'refresh' }, 'Refresh')
  const reemitBtn = el('button', { id: 'reemit' }, 'Re-emit Payload')
  const showMainBtn = el('button', { id: 'show-main' }, 'Show Main')
  const testOllamaBtn = el('button', { id: 'test-ollama' }, 'Test Ollama')
  const pullModelBtn = el('button', { id: 'pull-model' }, 'Pull Model')
  const pre = el('pre', { id: 'log' })
  app.append(title, refreshBtn, reemitBtn, showMainBtn, testOllamaBtn, pullModelBtn, pre)

  async function refresh() {
    try {
      const snap: unknown = await invoke('dbg_snapshot')
      pre.textContent = JSON.stringify(snap, null, 2)
    } catch (e) {
      pre.textContent = 'dbg_snapshot failed: ' + (e instanceof Error ? e.message : String(e))
    }
  }
  refreshBtn.onclick = () => void refresh()
  reemitBtn.onclick = async () => {
    try {
      await invoke('dbg_reemit')
      await refresh()
    } catch (e) {
      console.error(e)
    }
  }
  showMainBtn.onclick = async () => {
    try {
      await invoke('show_main')
    } catch (e) {
      console.error(e)
    }
  }

  testOllamaBtn.onclick = async () => {
    try {
      const resp = await invoke<string>('test_ollama')
      pre.textContent = 'test_ollama: ' + resp + '\n\n' + pre.textContent
    } catch (e) {
      pre.textContent =
        'test_ollama failed: ' +
        (e instanceof Error ? e.message : String(e)) +
        '\n\n' +
        pre.textContent
    }
  }

  pullModelBtn.onclick = async () => {
    try {
      // Allow the model name to be pasted via prompt for now; default used if empty
      const name = window.prompt('Model to pull (default gemma3:4b):', 'gemma3:4b') ?? undefined
      const resp = await invoke<string>('pull_ollama_model', { model: name })
      pre.textContent = 'pull_model: ' + resp + '\n\n' + pre.textContent
    } catch (e) {
      pre.textContent =
        'pull_model failed: ' +
        (e instanceof Error ? e.message : String(e)) +
        '\n\n' +
        pre.textContent
    }
  }

  void refresh()
  // Live UI status stream from Action window
  const logWrap = document.createElement('div')
  logWrap.style.marginTop = '12px'
  const logHeader = document.createElement('h3')
  logHeader.textContent = 'UI status stream'
  logHeader.style.margin = '8px 0 4px'
  const log = document.createElement('pre')
  log.style.whiteSpace = 'pre-wrap'
  log.style.fontSize = '12px'
  log.style.maxHeight = '240px'
  log.style.overflow = 'auto'
  log.textContent = '(waiting)'
  const lines: string[] = []
  const push = (s: string) => {
    const ts = new Date().toLocaleTimeString()
    lines.push(`[${ts}] ${s}`)
    if (lines.length > 200) lines.shift()
    log.textContent = lines.join('\n')
    log.scrollTop = log.scrollHeight
  }
  void listen('ui-status', (ev: Event<unknown>) => {
    try {
      const payload: unknown = ev.payload
      let msg: string
      let data: unknown
      if (typeof payload === 'string') {
        msg = payload
      } else if (
        typeof payload === 'object' &&
        payload !== null &&
        'msg' in payload &&
        typeof (payload as { msg?: unknown }).msg === 'string'
      ) {
        msg = (payload as { msg: string }).msg
        data = (payload as { data?: unknown }).data
      } else {
        msg = JSON.stringify(payload)
      }
      const dataStr = data !== undefined ? JSON.stringify(data) : ''
      push(dataStr ? `${msg} ${dataStr}` : msg)
    } catch (e) {
      push('ui-status parse error: ' + (e instanceof Error ? e.message : String(e)))
    }
  })
    .then(() => push('listener: ui-status ready'))
    .catch((e) =>
      push('failed to listen ui-status: ' + (e instanceof Error ? e.message : String(e)))
    )
  logWrap.append(logHeader, log)
  app.append(logWrap)
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init)
} else {
  init()
}
