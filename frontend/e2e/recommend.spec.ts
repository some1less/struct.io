import { expect, test } from '@playwright/test'

const API_BASE = process.env.VITE_API_BASE ?? 'http://localhost:8080'

test('first load: controls come from /api/config and a complete build renders', async ({
  page,
  request,
}) => {
  const config = await (await request.get(`${API_BASE}/api/config`)).json()

  await page.goto('/')

  await expect(page.getByRole('slider', { name: 'Budget' })).toHaveAttribute(
    'aria-valuemin',
    String(config.minBudget),
  )
  await expect(page.getByRole('button', { name: 'Gaming' })).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByText('Complete build')).toBeVisible()
  await expect(page.getByRole('heading', { name: /the build · 8 parts/i })).toBeVisible()
})

test('Hybrid sends purpose Work and renders the new build', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByText('Complete build')).toBeVisible()

  const hybrid = page.getByText('Hybrid', { exact: true })
  await expect(hybrid).toHaveCount(1) // only the purpose button; the result badge still says Gaming

  await page.getByRole('button', { name: 'Hybrid' }).click()
  const response = page.waitForResponse('**/api/algorithms/recommend')
  await page.getByRole('button', { name: 'Build it' }).click()

  expect((await (await response).json()).purpose).toBe('Work')
  await expect(hybrid).toHaveCount(2) // button + result badge
})
