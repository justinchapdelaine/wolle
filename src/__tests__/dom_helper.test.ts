import { describe, it, expect, beforeEach } from 'vitest'
import { el } from '../dom'

describe('dom helper', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('creates elements with props and children', () => {
    const child = document.createTextNode('child')
    const d = el('div', { id: 'x' }, 'hello', child)
    expect(d.tagName.toLowerCase()).toBe('div')
    expect(d.id).toBe('x')
    expect(d.textContent).toContain('hello')
    expect(d.contains(child)).toBe(true)
  })
})
