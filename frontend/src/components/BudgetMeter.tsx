import { motion } from 'framer-motion'
import { Wallet } from 'lucide-react'
import { Card } from '@/components/ui/card'
import { formatPln } from '@/lib/format'
import { cn } from '@/lib/utils'

interface BudgetMeterProps {
  totalBudget: number
  actualTotalPrice: number
}

export function BudgetMeter({ totalBudget, actualTotalPrice }: BudgetMeterProps) {
  const util = totalBudget > 0 ? actualTotalPrice / totalBudget : 0
  const leftover = totalBudget - actualTotalPrice
  const over = leftover < 0
  const pct = Math.round(util * 100)

  const color = over
    ? 'var(--color-bad)'
    : util > 0.97
      ? 'var(--color-warn)'
      : 'var(--color-good)'

  return (
    <Card className="p-5">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2 font-mono text-xs tracking-wide text-faint uppercase">
          <Wallet size={14} /> Budget used
        </div>
        <span className="font-mono text-sm font-semibold tabular-nums" style={{ color }}>
          {pct}%
        </span>
      </div>

      <div className="mt-3 flex items-baseline gap-2">
        <span className="font-mono text-3xl font-semibold tabular-nums text-fg">
          {formatPln(actualTotalPrice)}
        </span>
        <span className="font-mono text-sm text-faint">/ {formatPln(totalBudget)}</span>
      </div>

      <div className="mt-3 h-2.5 w-full overflow-hidden rounded-full bg-surface-2">
        <motion.div
          className="h-full rounded-full"
          style={{ backgroundColor: color }}
          initial={{ width: 0 }}
          animate={{ width: `${Math.min(util, 1) * 100}%` }}
          transition={{ duration: 0.7, ease: 'easeOut' }}
        />
      </div>

      <p className={cn('mt-2 font-mono text-xs', over ? 'text-bad' : 'text-muted')}>
        {over
          ? `${formatPln(Math.abs(leftover))} over budget`
          : `${formatPln(leftover)} left unspent (stranded budget)`}
      </p>
    </Card>
  )
}
