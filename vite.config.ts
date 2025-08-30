import { defineConfig } from 'vite'

export default defineConfig({
  build: {
    rollupOptions: {
      input: {
        index: 'index.html',
        action: 'action.html',
        status: 'status.html',
        debug: 'debug.html',
      },
    },
  },
})
