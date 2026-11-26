import { afterEach, describe, expect, it, vi } from 'vitest'
import { getRecommendation } from './api'
import type { RecommendationResult } from './types'

const partial: RecommendationResult = {
  purpose: 'Gaming',
  totalBudget: 3000,
  actualTotalPrice: 2400,
  isSuccess: false,
  message: 'Partial build',
  slots: [],
  failedSlots: [{ category: 'Case', reason: 'No compatible case within budget' }],
}

function respond(status: number, body: unknown) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify(body), { status })))
}

afterEach(() => vi.unstubAllGlobals())

describe('getRecommendation', () => {
  it('returns the partial build on 422', async () => {
    respond(422, partial)
    await expect(getRecommendation({ budget: 3000, purpose: 'Gaming' })).resolves.toEqual(partial)
  })

  it('throws the API message on other errors', async () => {
    respond(400, { message: 'Budget is too low.' })
    await expect(getRecommendation({ budget: 100, purpose: 'Gaming' })).rejects.toThrow(
      'Budget is too low.',
    )
  })
})
