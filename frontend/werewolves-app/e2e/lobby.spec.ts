import { test, expect } from '@playwright/test';

test.describe('Lobby', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to home and create a new game
    await page.goto('/');
    await page.getByRole('button', { name: 'Organize Game' }).click();
    await expect(page).toHaveURL(/\/game\/.*\/lobby/);
  });

  test('organize game shows lobby with QR code', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Werewolves' })).toBeVisible();
    await expect(page.getByRole('img', { name: 'QR Code' })).toBeVisible();
    await expect(page.getByText('Players (1)')).toBeVisible();
    await expect(page.getByText('PlayerName')).toBeVisible();
  });

  test('can adjust min players', async ({ page }) => {
    const minPlayersInput = page.getByRole('spinbutton', { name: 'Min Players' });
    await expect(minPlayersInput).toHaveValue('4');

    // Increase min players from 4 to 6
    await minPlayersInput.click();
    await minPlayersInput.press('ArrowUp');
    await minPlayersInput.press('ArrowUp');
    await expect(minPlayersInput).toHaveValue('6');

    // Decrease back to 5
    await minPlayersInput.press('ArrowDown');
    await expect(minPlayersInput).toHaveValue('5');
  });

  test('can adjust max players', async ({ page }) => {
    const maxPlayersInput = page.getByRole('spinbutton', { name: 'Max Players' });
    await expect(maxPlayersInput).toHaveValue('20');

    // Clear and type a new value
    await maxPlayersInput.click();
    await maxPlayersInput.fill('15');
    await expect(maxPlayersInput).toHaveValue('15');

    // Use arrow keys to go up
    await maxPlayersInput.press('ArrowUp');
    await expect(maxPlayersInput).toHaveValue('16');
  });

  test('max players cannot exceed 20', async ({ page }) => {
    const maxPlayersInput = page.getByRole('spinbutton', { name: 'Max Players' });
    await maxPlayersInput.click();
    await maxPlayersInput.fill('20');
    await maxPlayersInput.press('ArrowUp');
    // Should stay at 20 (max constraint)
    await expect(maxPlayersInput).toHaveValue('20');
  });

  test('min players cannot go below 2', async ({ page }) => {
    const minPlayersInput = page.getByRole('spinbutton', { name: 'Min Players' });
    await minPlayersInput.click();
    await minPlayersInput.fill('2');
    await minPlayersInput.press('ArrowDown');
    // Should stay at 2 (min constraint)
    await expect(minPlayersInput).toHaveValue('2');
  });

  test('start game button is disabled with insufficient players', async ({ page }) => {
    const startButton = page.getByRole('button', { name: 'Start Game' });
    await expect(startButton).toBeDisabled();
    await expect(page.getByText(/Need at least .* players to start/)).toBeVisible();
  });
});
