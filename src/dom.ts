export function el<K extends keyof HTMLElementTagNameMap>(
  tag: K,
  props: Partial<HTMLElementTagNameMap[K]> & Record<string, unknown> = {},
  ...children: (string | Node)[]
): HTMLElementTagNameMap[K] {
  const e = document.createElement(tag)
  // Assign known properties directly; set unknown/dashed keys as attributes (e.g., aria-* , data-*, role)
  for (const [key, value] of Object.entries(props)) {
    if (value === undefined || value === null) continue
    if (key in e) {
      try {
        const rec = e as unknown as Record<string, unknown>
        rec[key] = value
      } catch {
        if (typeof value === 'string') e.setAttribute(key, value)
        else if (typeof value === 'number' || typeof value === 'boolean')
          e.setAttribute(key, String(value))
      }
    } else {
      if (typeof value === 'string') e.setAttribute(key, value)
      else if (typeof value === 'number' || typeof value === 'boolean')
        e.setAttribute(key, String(value))
    }
  }
  children.forEach((c) => e.append(typeof c === 'string' ? document.createTextNode(c) : c))
  return e
}
