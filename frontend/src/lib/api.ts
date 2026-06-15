import type { Purpose, RecommendationRequest, RecommendationResult } from './types'
import { MOCK_RESULTS } from '@/data/mockResult'

// Live recommendation engine. The dockerized API serves on http://localhost:8080
// (compose.yaml); a permissive dev CORS policy in Program.cs allows this origin.
// Set USE_LIVE = false to fall back to the bundled mock build (no API required).
// Override the base URL at build time with VITE_API_BASE (e.g. http://localhost:5165 for `dotnet run`).
const USE_LIVE = true
const API_BASE = import.meta.env.VITE_API_BASE ?? 'http://localhost:8080'

// Exposed so the UI can label its data source.
export const IS_LIVE = USE_LIVE

export const PURPOSES: Purpose[] = ['Gaming', 'Work', 'Office']
export const MIN_BUDGET = 2400 // mirrors AlgorithmsController's guard

// Display names. The API value stays the Purpose key; 'Work' is surfaced as 'Hybrid'.
export const PURPOSE_LABEL: Record<Purpose, string> = {
  Gaming: 'Gaming',
  Work: 'Hybrid',
  Office: 'Office',
}

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
