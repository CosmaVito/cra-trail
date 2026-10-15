import { defineConfig, devices } from '@playwright/test';
import { port } from './e2e/settings.mjs';

export default defineConfig({
  testDir: './e2e',
  workers: 1,
  forbidOnly: !!process.env['CI'],
  reporter: process.env['CI'] ? [['list'], ['junit', { outputFile: 'test-results/e2e-junit.xml' }]] : 'list',
  use: { baseURL: `http://localhost:${port}`, trace: 'retain-on-failure' },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'node e2e/server.mjs',
    url: `http://localhost:${port}/.well-known/security.txt`,
    reuseExistingServer: false,
    timeout: 180_000,
  },
});
