import { defineConfig } from 'vite-plus'

// A Vite+ repo consolidates Vite / Vitest / Oxlint / Oxfmt / task config here.
// rig never reads this file — it keys off the `vite-plus` dependency in
// package.json — but it's what makes this a *real* Vite+ project rather than a
// plain Vite one.
export default defineConfig({
  // vite, test, lint, fmt, tasks … all live in one place
})
