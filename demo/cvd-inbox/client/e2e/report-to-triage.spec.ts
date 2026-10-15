import { expectNoAccessibilityViolations } from './axe';
import { expect, test } from './fixtures';
import { operator } from './settings.mjs';

test('a researcher reports, an operator triages, the researcher sees the change', async ({ page }) => {
  const mainNavigation = page.getByRole('navigation', { name: 'Main' });

  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Report a vulnerability' })).toBeVisible();
  await expectNoAccessibilityViolations(page);

  await page.getByLabel('Product').fill('Example Product');
  await page.getByLabel('Version').fill('1.2.3');
  await page.getByLabel('Description').fill('Stored XSS in the comment field of the admin page.');
  await page.getByLabel('Contact (optional)').fill('researcher@example.test');
  await page.getByRole('button', { name: 'Send report' }).click();

  await expect(page.getByRole('heading', { name: 'Report received' })).toBeFocused();
  const receipt = (await page.getByTestId('receipt-code').innerText()).trim();
  expect(receipt).toMatch(/^[0-9a-f]{4}(-[0-9a-f]{4}){7}$/);
  await expectNoAccessibilityViolations(page);

  await page.getByRole('link', { name: 'Check the status of your report' }).click();
  await page.getByLabel('Receipt code').fill(receipt);
  await page.getByRole('button', { name: 'Check status' }).click();
  await expect(page.getByTestId('report-status')).toHaveText('New');
  await expectNoAccessibilityViolations(page);

  await mainNavigation.getByRole('link', { name: 'Operator' }).click();
  await expect(page).toHaveURL(/\/operator\/sign-in$/);
  await expectNoAccessibilityViolations(page);
  await page.getByLabel('Name').fill(operator.name);
  await page.getByLabel('Password').fill(operator.password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByRole('heading', { name: 'Inbox' })).toBeVisible();
  const reportLink = page.getByRole('link', { name: 'Example Product' });
  await expect(reportLink).toBeVisible();
  await expectNoAccessibilityViolations(page);
  await reportLink.click();

  await expect(page.getByTestId('description')).toHaveText('Stored XSS in the comment field of the admin page.');
  await expectNoAccessibilityViolations(page);
  await page.getByRole('button', { name: 'Move to Triage' }).click();
  await expect(page.getByTestId('report-status')).toHaveText('Triage');
  await page.getByLabel('New note').fill('Reproduced on 1.2.3.');
  await page.getByRole('button', { name: 'Add note' }).click();
  await expect(page.getByText('Reproduced on 1.2.3.')).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page).toHaveURL(/\/operator\/sign-in$/);

  await mainNavigation.getByRole('link', { name: 'Check status' }).click();
  await page.getByLabel('Receipt code').fill(receipt);
  await page.getByRole('button', { name: 'Check status' }).click();
  await expect(page.getByTestId('report-status')).toHaveText('Triage');
});
