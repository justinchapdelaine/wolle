declare global {
  interface HTMLElementTagNameMap {
    'fluent-button': HTMLElement & { disabled: boolean }
    'fluent-text-area': HTMLElement & { value: string }
    'fluent-select': HTMLElement & { value: string }
    'fluent-option': HTMLElement
    'fluent-progress-ring': HTMLElement
  }
}

export {}
