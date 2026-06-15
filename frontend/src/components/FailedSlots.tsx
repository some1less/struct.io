import { AlertTriangle } from 'lucide-react'
import type { FailedSlot } from '@/lib/types'
import { categoryLabel } from '@/lib/format'

export function FailedSlots({ slots }: { slots: FailedSlot[] }) {
  if (slots.length === 0) return null
  return (
    <div className="rounded-xl border border-bad/40 bg-bad/10 p-5">
      <div className="flex items-center gap-2 font-mono text-sm font-semibold text-bad">
        <AlertTriangle size={16} />
        {slots.length} slot{slots.length > 1 ? 's' : ''} could not be filled
      </div>
      <ul className="mt-3 space-y-2">
        {slots.map((s) => (
          <li key={s.category} className="flex flex-col gap-0.5 sm:flex-row sm:gap-2">
            <span className="font-mono text-xs font-semibold text-fg">
              {categoryLabel(s.category)}:
            </span>
            <span className="text-xs text-muted">{s.reason}</span>
          </li>
        ))}
      </ul>
    </div>
  )
}
