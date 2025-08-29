import { emit } from '@tauri-apps/api/event'
import { closeApp, healthCheck } from './tauri'
import {
  provideFluentDesignSystem,
  allComponents,
  density,
  baseLayerLuminance,
  StandardLuminance,
  controlCornerRadius,
  bodyFont,
} from '@fluentui/web-components'

function init(): void {
  provideFluentDesignSystem().register(allComponents)
  density.withDefault(-1)
  const media: {
    matches: boolean
    addEventListener?: (type: string, listener: () => void) => void
  } =
    typeof window.matchMedia === 'function'
      ? window.matchMedia('(prefers-color-scheme: dark)')
      : { matches: false }
  const initialMode = media.matches ? StandardLuminance.DarkMode : StandardLuminance.LightMode
  baseLayerLuminance.setValueFor(document.documentElement, initialMode)
  document.documentElement.style.setProperty('--accent-base-color', '#2563eb')
  controlCornerRadius.withDefault(6)
  bodyFont.withDefault(
    "system-ui, -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, 'Apple Color Emoji', 'Segoe UI Emoji', 'Segoe UI Symbol', sans-serif"
  )

  const app = document.getElementById('app')
  if (!app) throw new Error('Missing #app container')

  const title = document.createElement('h2')
  title.textContent = 'Status'
  const status = document.createElement('div')
  status.id = 'status'
  status.setAttribute('role', 'status')
  status.setAttribute('aria-live', 'polite')

  const refresh = document.createElement('fluent-button')
  refresh.textContent = 'Refresh'
  refresh.addEventListener('click', () => void check())

  app.append(title, refresh, status)

  window.addEventListener(
    'keydown',
    (ev: KeyboardEvent) => {
      if (ev.key === 'Escape') {
        ev.preventDefault()
        ev.stopImmediatePropagation()
        void closeApp()
      }
    },
    { capture: true }
  )

  void emit('frontend-ready').catch((err) => {
    if (typeof console !== 'undefined') console.debug('emit(frontend-ready) failed', err)
  })

  async function check() {
    try {
      const res = await healthCheck()
      status.textContent = typeof res === 'string' ? res : JSON.stringify(res)
    } catch (e) {
      status.textContent = 'Ollama not reachable: ' + (e instanceof Error ? e.message : String(e))
    }
  }

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
