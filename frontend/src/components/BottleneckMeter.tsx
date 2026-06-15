import type { ReactNode } from 'react'
import { motion } from 'framer-motion'
import { Scale, Cpu, MonitorPlay } from 'lucide-react'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import {
  BOTTLENECK_THRESHOLD,
  STATUS_BLURB,
  STATUS_LABEL,
  type BottleneckReading,
} from '@/lib/bottleneck'

const CPU_COLOR = 'var(--color-cpu)'
const GPU_COLOR = 'var(--color-gpu)'

function pct(n: number) {
  return `${Math.round(Math.max(0, Math.min(1, n)) * 100)}%`
}

export function BottleneckMeter({ reading }: { reading: BottleneckReading }) {
  const { status, cpuScore, gpuScore, gap } = reading
  const tone = status === 'balanced' ? 'good' : status === 'unknown' ? 'neutral' : 'warn'

  // Balance beam: pointer leans toward the STRONGER part; centre band (±threshold) = balanced.
  const cpu = cpuScore ?? 0
  const gpu = gpuScore ?? 0
  const lean = Math.max(-1, Math.min(1, gpu - cpu)) // -1 (CPU stronger) .. +1 (GPU stronger)
  const pointerLeft = 50 + lean * 50
  const bandHalf = BOTTLENECK_THRESHOLD * 50 // band half-width in %

  return (
    <Card className="p-5">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 font-mono text-xs tracking-wide text-faint uppercase">
          <Scale size={14} /> CPU ↔ GPU balance
        </div>
        <Badge tone={tone}>{STATUS_LABEL[status]}</Badge>
      </div>

      {/* Balance beam */}
      <div className="relative mt-6 mb-2 h-10">
        {/* track */}
        <div className="absolute top-1/2 h-1.5 w-full -translate-y-1/2 rounded-full bg-surface-2" />
        {/* balanced zone */}
        <div
          className="absolute top-1/2 h-1.5 -translate-y-1/2 rounded-full bg-good/25"
          style={{ left: `${50 - bandHalf}%`, width: `${bandHalf * 2}%` }}
        />
        {/* centre tick */}
        <div className="absolute top-1/2 left-1/2 h-4 w-px -translate-x-1/2 -translate-y-1/2 bg-line" />
        {/* pointer */}
        <motion.div
          className="absolute top-1/2 h-6 w-6 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-fg/70 bg-surface shadow-md"
          initial={{ left: '50%' }}
          animate={{ left: `${pointerLeft}%` }}
          transition={{ type: 'spring', stiffness: 120, damping: 16 }}
        />
      </div>
      <div className="flex justify-between font-mono text-[11px] text-faint">
        <span>← CPU stronger</span>
        <span>balanced</span>
        <span>GPU stronger →</span>
      </div>

      {/* Score rows */}
      <div className="mt-5 space-y-3">
        <ScoreRow
          icon={<Cpu size={14} style={{ color: CPU_COLOR }} />}
          label="CPU"
          score={cpuScore}
          color={CPU_COLOR}
        />
        <ScoreRow
          icon={<MonitorPlay size={14} style={{ color: GPU_COLOR }} />}
          label="GPU"
          score={gpuScore}
          color={GPU_COLOR}
        />
      </div>

      <p className="mt-4 text-xs leading-relaxed text-muted">
        {STATUS_BLURB[status]}
        {status !== 'unknown' && (
          <span className="text-faint">
            {' '}
            (gap {pct(gap)} vs {pct(BOTTLENECK_THRESHOLD)} threshold)
          </span>
        )}
      </p>
    </Card>
  )
}

function ScoreRow({
  icon,
  label,
  score,
  color,
}: {
  icon: ReactNode
  label: string
  score: number | null
  color: string
}) {
  return (
    <div className="flex items-center gap-3">
      <div className="flex w-12 items-center gap-1.5 font-mono text-xs text-muted">
        {icon}
        {label}
      </div>
      <div className="h-2 flex-1 overflow-hidden rounded-full bg-surface-2">
        <motion.div
          className="h-full rounded-full"
          style={{ backgroundColor: color }}
          initial={{ width: 0 }}
          animate={{ width: pct(score ?? 0) }}
          transition={{ duration: 0.6, ease: 'easeOut' }}
        />
      </div>
      <span className="w-10 text-right font-mono text-xs tabular-nums text-fg">
        {score === null ? '—' : pct(score)}
      </span>
    </div>
  )
}
