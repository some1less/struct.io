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
  // exact: build rows are buttons too, and part names like "TUF GAMING" would match
  await expect(page.getByRole('button', { name: 'Gaming', exact: true })).toHaveAttribute('aria-pressed', 'true')
  await expect(page.getByText('Complete build')).toBeVisible()
  await expect(page.getByRole('heading', { name: /the build · 8 parts/i })).toBeVisible()
})

test('Work sends purpose Work and renders the new build', async ({ page }) => {
  await page.goto('/')
  await expect(page.getByText('Complete build')).toBeVisible()

  const work = page.getByText('Work', { exact: true })
  await expect(work).toHaveCount(1) // only the purpose button; the result chip still says Gaming

  await page.getByRole('button', { name: 'Work', exact: true }).click()
  const response = page.waitForResponse('**/api/algorithms/recommend')
  await page.getByRole('button', { name: 'Build it' }).click()

  expect((await (await response).json()).purpose).toBe('Work')
  await expect(work).toHaveCount(2) // button + result chip
})
