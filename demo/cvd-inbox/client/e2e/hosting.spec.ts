import { expectNoAccessibilityViolations } from './axe';
import { expect, test } from './fixtures';

test('security.txt is served as UTF-8 text with the required fields', async ({ request }) => {
  const response = await request.get('/.well-known/security.txt');

  expect(response.status()).toBe(200);
  expect(response.headers()['content-type']).toBe('text/plain; charset=utf-8');
  const body = await response.text();
  expect(body).toContain('Contact: https://inbox.example.test/\n');
  expect(body).toMatch(/^Expires: \d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/m);
  expect(body).toContain('Policy: https://inbox.example.test/security-policy\n');
});

test('a deep link loads the app and the policy shows the configured retention', async ({ page }) => {
  await page.goto('/security-policy');

  await expect(page.getByRole('heading', { name: 'Security policy' })).toBeVisible();
  await expect(page.getByText('we erase them 30 days after the report is closed')).toBeVisible();
  await expectNoAccessibilityViolations(page);
});

test('the app is served with the content security policy', async ({ request }) => {
  const response = await request.get('/');

  expect(response.headers()['content-security-policy']).toContain("script-src 'self'");
  expect(response.headers()['x-frame-options']).toBe('DENY');
});
