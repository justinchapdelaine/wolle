import { emit } from '@tauri-apps/api/event'
import { healthCheck, getStartOnBoot, setStartOnBoot } from './tauri'
import { setupFluentBase, wireEscToClose } from './ui'

function init(): void {
  setupFluentBase()

  const app = document.getElementById('app')
  if (!app) throw new Error('Missing #app container')

  const title = document.createElement('h2')
  title.textContent = 'Settings'
  const status = document.createElement('div')
  status.id = 'status'
  status.setAttribute('role', 'status')
  status.setAttribute('aria-live', 'polite')

  // Settings section: Run on Windows startup
  const startRow = document.createElement('div')
  const startLabel = document.createElement('label')
  startLabel.id = 'start-label'
  startLabel.textContent = 'Run on Windows startup'
  const startSwitch = document.createElement('fluent-switch') as HTMLElement & { checked: boolean }
  startSwitch.setAttribute('id', 'start-on-boot')
  startSwitch.setAttribute('aria-labelledby', 'start-label')
  startSwitch.addEventListener('change', () => {
    void (async () => {
      try {
        await setStartOnBoot(startSwitch.checked)
        status.textContent = 'Saved'
      } catch {
        status.textContent = 'Failed to save'
      }
    })()
  })
  startRow.append(startLabel, startSwitch)

  const refresh = document.createElement('fluent-button')
  refresh.textContent = 'Refresh status'
  refresh.addEventListener('click', () => void check())

  app.append(title, startRow, refresh, status)

  wireEscToClose()

  void emit('frontend-ready').catch((err) => {
    if (typeof console !== 'undefined') console.debug('emit(frontend-ready) failed', err)
  })

  async function check() {
    try {
      const res = await healthCheck()
      status.textContent = res.message
    } catch (e) {
      status.textContent = 'Ollama not reachable: ' + (e instanceof Error ? e.message : String(e))
    }
  }

  // Initialize settings
  void (async () => {
    try {
      startSwitch.checked = await getStartOnBoot()
    } catch {
      // ignore; leave unchecked
    }
  })()

  void check()
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => {
    try {
      init()
    } catch (e) {
      console.error(e)
    }
  })
} else {
  init()
}
