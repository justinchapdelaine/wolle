import { closeApp } from './tauri'
import {
  provideFluentDesignSystem,
  allComponents,
  density,
  baseLayerLuminance,
  StandardLuminance,
  controlCornerRadius,
  bodyFont,
} from '@fluentui/web-components'

/**
 * Initialize Fluent UI web components and set base design tokens.
 * - Registers components once
 * - Applies compact density
 * - Sets initial luminance from OS theme to avoid token flip
 * - Applies brand hooks like accent color, corner radius, and body font
 */
export function setupFluentBase(): void {
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
  // Keep dynamic theme sync in page code if needed; this is the base setup.
}

/**
 * Wire Escape key to close/hide the current window via backend command.
 * Uses capture phase so inner components can't swallow it.
 */
export function wireEscToClose(): void {
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
}
