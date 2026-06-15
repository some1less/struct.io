import { motion } from 'framer-motion'
import { cn } from '@/lib/utils'

interface ScoreBarProps {
  score: number // 0..1
  color?: string
  className?: string
  height?: string
}

export function ScoreBar({
  score,
  color = 'var(--accent)',
  className,
  height = 'h-2',
}: ScoreBarProps) {
  const pct = Math.max(0, Math.min(1, score)) * 100
  return (
    <div className={cn('w-full overflow-hidden rounded-full bg-surface-2', height, className)}>
      <motion.div
        className="h-full rounded-full"
        style={{ backgroundColor: color }}
        initial={{ width: 0 }}
        animate={{ width: `${pct}%` }}
        transition={{ duration: 0.6, ease: 'easeOut' }}
      />
    </div>
  )
}
