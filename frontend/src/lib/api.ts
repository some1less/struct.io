import type { Purpose, RecommendationRequest, RecommendationResult } from './types'
import { MOCK_RESULTS } from '@/data/mockResult'

// Flip to true (and run the .NET API at API_BASE) to use the live recommendation engine.
// The API serves on http://localhost:5165 by default (see launchSettings.json); it needs CORS
// enabled for this origin (a permissive dev policy is wired in Program.cs).
const USE_LIVE = false
const API_BASE = 'http://localhost:5165'

export const PURPOSES: Purpose[] = ['Gaming', 'Work', 'Office']
export const MIN_BUDGET = 2400 // mirrors AlgorithmsController's guard

export const PURPOSE_DEFAULT_BUDGET: Record<Purpose, number> = {
  Gaming: MOCK_RESULTS.Gaming.totalBudget,
  Work: MOCK_RESULTS.Work.totalBudget,
  Office: MOCK_RESULTS.Office.totalBudget,
}

function delay(ms: number) {
  return new Promise((r) => setTimeout(r, ms))
}

export async function getRecommendation(
  req: RecommendationRequest,
): Promise<RecommendationResult> {
  if (USE_LIVE) {
    const res = await fetch(`${API_BASE}/api/algorithms/recommend`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(req),
    })
    if (!res.ok) {
      const body = await res.json().catch(() => ({}))
      throw new Error(body?.message ?? `Request failed (${res.status})`)
    }
    return (await res.json()) as RecommendationResult
  }

  // Mock path: return the canonical build for the purpose, reflecting the chosen budget.
  await delay(650)
  const base = MOCK_RESULTS[req.purpose]
  return { ...structuredClone(base), totalBudget: req.budget }
}
