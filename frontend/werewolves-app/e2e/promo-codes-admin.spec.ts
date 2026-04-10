/**
 * Promo codes admin page test.
 *
 * Hits the real backend directly (not via Angular).
 * Requires the following to be running before executing this test:
 *
 *   docker compose up -d
 *   cd backend/WerewolvesAPI && dotnet run
 *
 * Run:
 *   cd frontend/werewolves-app
 *   npx playwright test e2e/promo-codes-admin.spec.ts
 */

import { test, expect } from '@playwright/test';

const ADMIN_URL = 'http://localhost:5000/promo-codes';
const USERNAME = 'admin';
const PASSWORD = 'hoihoi123';

test('admin page requires authentication', async ({ page }) => {
  // Navigate without credentials — browser should show a 401
  const response = await page.goto(ADMIN_URL);
  expect(response?.status()).toBe(401);
});

test('admin page loads after Basic Auth login', async ({ browser }) => {
  // Supply credentials via the URL so Playwright bypasses the browser dialog
  const context = await browser.newContext({
    httpCredentials: { username: USERNAME, password: PASSWORD },
  });
  const page = await context.newPage();

  const response = await page.goto(ADMIN_URL);
  expect(response?.status()).toBe(200);

  await expect(page.locator('h1')).toContainText('Promo Codes');
  await expect(page.getByRole('button', { name: 'Generate new code' })).toBeVisible();

  await context.close();
});

test('generate button creates a new code', async ({ browser }) => {
  const context = await browser.newContext({
    httpCredentials: { username: USERNAME, password: PASSWORD },
  });
  const page = await context.newPage();
  await page.goto(ADMIN_URL);

  await page.getByRole('button', { name: 'Generate new code' }).click();

  // After redirect back, a new code should be displayed
  await expect(page.locator('#newCode')).toBeVisible();

  // Copy button should be present next to the code
  await expect(page.getByRole('button', { name: 'Copy' })).toBeVisible();

  await context.close();
});

test('generated code appears in the recent codes table', async ({ browser }) => {
  const context = await browser.newContext({
    httpCredentials: { username: USERNAME, password: PASSWORD },
  });
  const page = await context.newPage();
  await page.goto(ADMIN_URL);

  await page.getByRole('button', { name: 'Generate new code' }).click();
  await expect(page.locator('#newCode')).toBeVisible();

  const code = await page.locator('#newCode').innerText();
  expect(code).toMatch(/^WOLF-[A-Z0-9]{4}-[A-Z0-9]{4}$/);

  // URL should not contain the code
  expect(page.url()).not.toContain(code);

  // The code should also appear in the table below
  await expect(page.locator('table')).toContainText(code);
  await expect(page.locator('table')).toContainText('Unused');

  await context.close();
});
