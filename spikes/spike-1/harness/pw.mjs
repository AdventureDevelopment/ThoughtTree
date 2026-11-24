// Playwright is pre-installed globally in the agent environment; ESM ignores NODE_PATH, so load it through require.
// Elsewhere: npm install playwright (any recent version) next to this folder.
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
export const { chromium, firefox, devices } = require('playwright');
