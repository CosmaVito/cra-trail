// Starts CVD Inbox for the end-to-end tests: a fresh data folder, one operator, the built client in wwwroot.
// Node instead of bash, so it runs the same way on Windows and Linux.
import { spawn, spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { operator, port } from './settings.mjs';

const client = resolve(fileURLToPath(new URL('..', import.meta.url)));
const project = resolve(client, '../src/CvdInbox');
const built = join(client, 'dist/cvd-inbox/browser');

if (!existsSync(built)) {
  console.error('Build the client first: npm run build');
  process.exit(1);
}
rmSync(join(project, 'wwwroot'), { recursive: true, force: true });
cpSync(built, join(project, 'wwwroot'), { recursive: true });

const env = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Production',
  ASPNETCORE_URLS: `http://localhost:${port}`,
  Inbox__DataPath: mkdtempSync(join(tmpdir(), 'cvd-inbox-e2e-')),
  Disclosure__PublicOrigin: 'https://inbox.example.test',
  Disclosure__Contact__0: 'mailto:security@example.test',
  Disclosure__Expires: new Date(Date.now() + 180 * 24 * 60 * 60 * 1000).toISOString(),
  Logging__Console__FormatterName: 'simple',
};
const run = ['run', '--project', project, '-c', 'Release', '--no-launch-profile', '--'];

const created = spawnSync('dotnet', [...run, 'create-operator', operator.name, '--password-stdin'], {
  env,
  input: `${operator.password}\n`,
  stdio: ['pipe', 'inherit', 'inherit'],
});
if (created.status !== 0) process.exit(created.status ?? 1);

const server = spawn('dotnet', run, { env, stdio: 'inherit' });
server.on('exit', (code) => process.exit(code ?? 0));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.kill(signal));
