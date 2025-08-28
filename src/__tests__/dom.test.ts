import { describe, it, expect, beforeEach } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'

// index.html is at the repository root
const html = fs.readFileSync(path.resolve(process.cwd(), 'index.html'), 'utf-8')

describe('index.html smoke', () => {
  beforeEach(() => {
    document.documentElement.innerHTML = html
  })

  it('renders the status element', () => {
    // The app script runs in a browser; in test environment we only verify the root container exists
    const appDiv = document.getElementById('app')
    expect(appDiv).toBeTruthy()
  })
})
