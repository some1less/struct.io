import type { RecommendationResult } from './types'

// Reproduces the engine's CPU<->GPU bottleneck term (BuildObjective / ObjectiveSettings):
//   gap = |cpuScore - gpuScore|, penalty applies once gap exceeds BottleneckThreshold (0.15).
export const BOTTLENECK_THRESHOLD = 0.15

export type BottleneckStatus = 'balanced' | 'cpu-bound' | 'gpu-bound' | 'unknown'

export interface BottleneckReading {
  status: BottleneckStatus
  cpuScore: number | null
  gpuScore: number | null
  gap: number
}

function scoreFor(result: RecommendationResult, category: string): number | null {
  const slot = result.slots.find(
    (s) => s.category.toLowerCase() === category.toLowerCase(),
  )
  const top = slot?.recommendations.find((r) => r.rank === 1) ?? slot?.recommendations[0]
  return top ? top.performanceScore : null
}

export function readBottleneck(result: RecommendationResult): BottleneckReading {
  const cpuScore = scoreFor(result, 'Cpu')
  const gpuScore = scoreFor(result, 'Gpu')

  if (cpuScore === null || gpuScore === null) {
    return { status: 'unknown', cpuScore, gpuScore, gap: 0 }
  }

  const gap = Math.abs(cpuScore - gpuScore)
  let status: BottleneckStatus = 'balanced'
  if (gap > BOTTLENECK_THRESHOLD) {
    status = cpuScore > gpuScore ? 'gpu-bound' : 'cpu-bound'
  }
  return { status, cpuScore, gpuScore, gap }
}

export const STATUS_LABEL: Record<BottleneckStatus, string> = {
  balanced: 'Balanced',
  'cpu-bound': 'CPU bottleneck',
  'gpu-bound': 'GPU bottleneck',
  unknown: 'Not enough data',
}

export const STATUS_BLURB: Record<BottleneckStatus, string> = {
  balanced: 'CPU and GPU are well-matched — neither holds the other back.',
  'cpu-bound': 'The GPU outclasses the CPU; the processor may limit frame rates.',
  'gpu-bound': 'The CPU outclasses the GPU; the graphics card is the limiting factor.',
  unknown: 'A CPU and GPU are both required to assess balance.',
}
