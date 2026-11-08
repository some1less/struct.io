import { describe, expect, it } from 'vitest'
import { readBottleneck } from './bottleneck'
import type { RecommendationResult } from './types'

function resultWith(scores: Record<string, number>): RecommendationResult {
  const slots = Object.entries(scores).map(([category, performanceScore]) => ({
    category,
    allocatedBudget: 0,
    recommendations: [{ rank: 1, performanceScore, component: {} as never }],
  }))
  return { slots } as unknown as RecommendationResult
}

describe('readBottleneck', () => {
  it('is balanced while the CPU-GPU gap stays within the threshold', () => {
    const r = readBottleneck(resultWith({ Cpu: 0.8, Gpu: 0.9 }), 0.15)
    expect(r.status).toBe('balanced')
    expect(r.gap).toBeCloseTo(0.1)
  })

  it('is gpu-bound when the CPU outclasses the GPU', () => {
    expect(readBottleneck(resultWith({ Cpu: 0.95, Gpu: 0.6 }), 0.15).status).toBe('gpu-bound')
  })

  it('is cpu-bound when the GPU outclasses the CPU', () => {
    expect(readBottleneck(resultWith({ Cpu: 0.5, Gpu: 0.9 }), 0.15).status).toBe('cpu-bound')
  })

  it('uses the threshold it is given', () => {
    expect(readBottleneck(resultWith({ Cpu: 0.8, Gpu: 0.9 }), 0.05).status).toBe('cpu-bound')
  })

  it('is unknown without both a CPU and a GPU', () => {
    expect(readBottleneck(resultWith({ Cpu: 0.8 }), 0.15).status).toBe('unknown')
  })
})
