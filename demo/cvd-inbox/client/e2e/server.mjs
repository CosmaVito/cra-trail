// Starts CVD Inbox for the end-to-end tests: a fresh data folder, one operator, the built client in wwwroot.
// Node instead of bash, so it runs the same way on Windows and Linux.
import { spawn, spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, rmSync } from 'node:fs';
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

// Playwright force-kills this process on Windows, so exit handlers do not always run. A fixed folder in the
// ignored test-results directory is wiped at every start instead of leaking into the system temp folder.
const dataPath = join(client, 'test-results/e2e-data');
rmSync(dataPath, { recursive: true, force: true });
mkdirSync(dataPath, { recursive: true });
const cleanUp = () => rmSync(dataPath, { recursive: true, force: true });

const env = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Production',
  ASPNETCORE_URLS: `http://localhost:${port}`,
  Inbox__DataPath: dataPath,
  Disclosure__PublicOrigin: 'https://inbox.example.test',
  Disclosure__Contact__0: 'mailto:security@example.test',
  Disclosure__Expires: new Date(Date.now() + 180 * 24 * 60 * 60 * 1000).toISOString(),
  Logging__Console__FormatterName: 'simple',
};

function fail(message, code = 1) {
  console.error(message);
  cleanUp();
  process.exit(code);
}

// Build once and start the app itself, not `dotnet run`: signals then reach the app and not a wrapper process.
const build = spawnSync('dotnet', ['build', project, '-c', 'Release', '--nologo', '-v', 'q'], { stdio: 'inherit' });
if (build.error) fail(`Cannot start dotnet: ${build.error.message}`);
if (build.status !== 0) fail('dotnet build failed', build.status ?? 1);

// The app's content root is its own folder, so the client goes into the build output, next to cvd-inbox.dll.
const output = join(project, 'bin/Release/net10.0');
const dll = join(output, 'cvd-inbox.dll');
if (!existsSync(dll)) fail(`dotnet build did not produce ${dll}`);
rmSync(join(output, 'wwwroot'), { recursive: true, force: true });
cpSync(built, join(output, 'wwwroot'), { recursive: true });

const created = spawnSync('dotnet', [dll, 'create-operator', operator.name, '--password-stdin'], {
  env,
  input: `${operator.password}\n`,
  stdio: ['pipe', 'inherit', 'inherit'],
});
if (created.error) fail(`Cannot start dotnet: ${created.error.message}`);
if (created.status !== 0) fail('create-operator failed', created.status ?? 1);

const server = spawn('dotnet', [dll], { env, stdio: 'inherit' });
server.on('error', (error) => fail(`Cannot start dotnet: ${error.message}`));
// Remove the data folder only after the app has exited, so the database files are not locked.
server.on('exit', (code) => {
  cleanUp();
  process.exit(code ?? 0);
});
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.kill(signal));
