/**
 * Helpers for working with ARIA live regions and status text.
 */
export function setStatusText(el: Element | null | undefined, text: string): void {
  const target = (el as HTMLElement | null) ?? document.getElementById('status')
  if (!target) return
  target.textContent = text
}

export function getStatusText(el?: Element | null): string {
  const target = (el as HTMLElement | null) ?? document.getElementById('status')
  return (target?.textContent ?? '').toString()
}
