// ESLint Flat Config for ESLint v9+
// Mirrors previous .eslintrc.cjs settings and adds sensible ignores

const pluginImport = require('eslint-plugin-import')
const pluginN = require('eslint-plugin-n')
const pluginPromise = require('eslint-plugin-promise')
const tseslint = require('@typescript-eslint/eslint-plugin')
const tsParser = require('@typescript-eslint/parser')
const prettierPlugin = require('eslint-plugin-prettier')

/** @type {import('eslint').Linter.FlatConfig[]} */
module.exports = [
  {
    ignores: [
      'node_modules/**',
      'dist/**',
      'coverage/**',
      'src-tauri/**',
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
      'n/prefer-node-protocol': 'error',
      ...pluginImport.configs.recommended.rules,
      ...pluginN.configs.recommended.rules,
      ...pluginPromise.configs.recommended.rules,
    },
  },
  {
    files: ['**/*.ts', '**/*.tsx'],
    ignores: ['src/**/*.test.ts', 'src/**/*.spec.ts'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        project: ['./tsconfig.json'],
        sourceType: 'module',
        ecmaVersion: 'latest',
      },
    },
    plugins: {
      '@typescript-eslint': tseslint,
      import: pluginImport,
      n: pluginN,
      promise: pluginPromise,
      prettier: prettierPlugin,
    },
    rules: {
      ...tseslint.configs['recommended-type-checked'].rules,
      ...tseslint.configs['stylistic-type-checked'].rules,
      'no-unused-vars': 'off',
      '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
      'import/no-unresolved': 'off',
  // Note: import/no-unused-modules is incompatible with ESLint flat config without an .eslintrc ignorePatterns.
  // For unused export detection, use the ts-prune script added in package.json.
      'n/prefer-node-protocol': 'error',
      'prettier/prettier': 'error',
    },
  },
  {
    files: ['src/**/*.test.ts', 'src/**/*.spec.ts'],
    languageOptions: {
      parser: tsParser,
      parserOptions: {
        project: ['./tsconfig.test.json'],
        sourceType: 'module',
        ecmaVersion: 'latest',
      },
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
    plugins: {
      '@typescript-eslint': tseslint,
      import: pluginImport,
      n: pluginN,
      promise: pluginPromise,
      prettier: prettierPlugin,
    },
    rules: {
      ...tseslint.configs['recommended-type-checked'].rules,
      ...tseslint.configs['stylistic-type-checked'].rules,
      'no-unused-vars': 'off',
      // In tests, enforce unused vars as errors to avoid dead code in specs/helpers
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      'import/no-unresolved': 'off',
      'n/prefer-node-protocol': 'error',
      'prettier/prettier': 'error',
    },
  },
]
