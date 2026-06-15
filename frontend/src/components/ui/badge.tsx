import type { HTMLAttributes } from 'react'
import { cn } from '@/lib/utils'

type Tone = 'accent' | 'neutral' | 'good' | 'warn' | 'bad'

const tones: Record<Tone, string> = {
  accent: 'bg-[var(--accent-soft)] text-[var(--accent)] border-[var(--accent)]/30',
  neutral: 'bg-surface-2 text-muted border-line',
  good: 'bg-good/10 text-good border-good/30',
  warn: 'bg-warn/10 text-warn border-warn/30',
  bad: 'bg-bad/10 text-bad border-bad/30',
}

export function Badge({
  tone = 'neutral',
  className,
  ...props
}: HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 font-mono text-xs font-medium',
        tones[tone],
        className,
      )}
      {...props}
    />
  )
}
