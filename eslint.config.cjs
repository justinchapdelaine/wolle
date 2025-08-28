// ESLint Flat Config for ESLint v9+
// Mirrors previous .eslintrc.cjs settings and adds sensible ignores

const pluginImport = require('eslint-plugin-import')
const pluginN = require('eslint-plugin-n')
const pluginPromise = require('eslint-plugin-promise')

/** @type {import('eslint').Linter.FlatConfig[]} */
module.exports = [
  {
    ignores: [
      'node_modules/**',
      'dist/**',
      'coverage/**',
      'src-tauri/**',
      'src/**/*.js',
      'tools/dumpbin_artifact_outputs/**',
      'dumpbin_*.txt',
      'docs/dumpbin_*.txt',
    ],
  },
  {
    files: ['**/*.js'],
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: {
        // Browser + Node globals
        window: 'readonly',
        document: 'readonly',
        console: 'readonly',
        module: 'readonly',
        __dirname: 'readonly',
        require: 'readonly',
      },
    },
    plugins: {
      import: pluginImport,
      n: pluginN,
      promise: pluginPromise,
    },
    rules: {
      'no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
      'import/no-unresolved': 'off',
      ...pluginImport.configs.recommended.rules,
      ...pluginN.configs.recommended.rules,
      ...pluginPromise.configs.recommended.rules,
    },
  },
  {
    files: ['src/**/*.test.js', 'src/**/*.spec.js'],
    languageOptions: {
      globals: {
        // Vitest-like globals (works for Vitest and JSDOM tests)
        describe: 'readonly',
        it: 'readonly',
        test: 'readonly',
        expect: 'readonly',
        beforeAll: 'readonly',
        beforeEach: 'readonly',
        afterAll: 'readonly',
        afterEach: 'readonly',
        vi: 'readonly',
      },
    },
  },
]
