import { Gamepad2, Briefcase, Building2, Cpu, type LucideIcon } from 'lucide-react'
import type { Purpose } from '@/lib/types'
import { MIN_BUDGET, PURPOSES } from '@/lib/api'
import { formatPln } from '@/lib/format'
import { Button } from '@/components/ui/button'
import { Slider } from '@/components/ui/slider'
import { cn } from '@/lib/utils'

const MAX_BUDGET = 25000

const PURPOSE_META: Record<Purpose, { icon: LucideIcon; blurb: string }> = {
  Gaming: { icon: Gamepad2, blurb: 'GPU-first, high frame rates' },
  Work: { icon: Briefcase, blurb: 'Cores & memory for productivity' },
  Office: { icon: Building2, blurb: 'Efficient everyday essentials' },
}

interface ControlPanelProps {
  budget: number
  purpose: Purpose
  loading: boolean
  onBudgetChange: (b: number) => void
  onPurposeChange: (p: Purpose) => void
  onSubmit: () => void
}

export function ControlPanel({
  budget,
  purpose,
  loading,
  onBudgetChange,
  onPurposeChange,
  onSubmit,
}: ControlPanelProps) {
  const tooLow = budget < MIN_BUDGET

  return (
    <div className="rounded-2xl border border-line bg-surface/70 p-5 shadow-xl shadow-black/30 backdrop-blur-sm sm:p-6">
      {/* Purpose segmented control */}
      <fieldset>
        <legend className="mb-2 font-mono text-xs tracking-wide text-faint uppercase">
          Purpose
        </legend>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          {PURPOSES.map((p) => {
            const Icon = PURPOSE_META[p].icon
            const active = p === purpose
            return (
              <button
                key={p}
                type="button"
                onClick={() => onPurposeChange(p)}
                aria-pressed={active}
                className={cn(
                  'group flex cursor-pointer items-center gap-3 rounded-xl border p-3 text-left transition-colors duration-150',
                  active
                    ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
                    : 'border-line bg-surface-2/40 hover:border-faint',
                )}
              >
                <span
                  className={cn(
                    'grid h-10 w-10 shrink-0 place-items-center rounded-lg transition-colors',
                    active ? 'bg-[var(--accent)] text-bg' : 'bg-surface-2 text-muted',
                  )}
                >
                  <Icon size={20} strokeWidth={2} />
                </span>
                <span className="min-w-0">
                  <span
                    className={cn(
                      'block font-mono text-sm font-semibold',
                      active ? 'text-fg' : 'text-muted',
                    )}
                  >
                    {p}
                  </span>
                  <span className="block truncate text-xs text-faint">
                    {PURPOSE_META[p].blurb}
                  </span>
                </span>
              </button>
            )
          })}
        </div>
      </fieldset>

      {/* Budget */}
      <div className="mt-6">
        <div className="mb-2 flex items-end justify-between">
          <label
            htmlFor="budget"
            className="font-mono text-xs tracking-wide text-faint uppercase"
          >
            Budget
          </label>
          <div className="flex items-baseline gap-2">
            <span className="font-mono text-2xl font-semibold tabular-nums text-fg">
              {formatPln(budget)}
            </span>
          </div>
        </div>
        <Slider
          value={budget}
          min={MIN_BUDGET}
          max={MAX_BUDGET}
          step={100}
          onValueChange={onBudgetChange}
        />
        <div className="mt-1 flex justify-between font-mono text-[11px] text-faint tabular-nums">
          <span>{formatPln(MIN_BUDGET)}</span>
          <span>{formatPln(MAX_BUDGET)}</span>
        </div>
        <input
          id="budget"
          type="number"
          min={MIN_BUDGET}
          max={MAX_BUDGET}
          step={100}
          value={budget}
          onChange={(e) => onBudgetChange(Number(e.target.value) || 0)}
          className="mt-3 w-full rounded-lg border border-line bg-surface-2/50 px-3 py-2 font-mono text-sm tabular-nums text-fg outline-none focus-visible:border-[var(--accent)] focus-visible:ring-2 focus-visible:ring-[var(--accent)]"
        />
        {tooLow && (
          <p role="alert" className="mt-2 font-mono text-xs text-bad">
            Minimum budget is {formatPln(MIN_BUDGET)} for a valid build.
          </p>
        )}
      </div>

      <Button
        size="lg"
        className="mt-6 w-full"
        disabled={loading || tooLow}
        onClick={onSubmit}
      >
        <Cpu size={18} className={cn(loading && 'animate-spin')} />
        {loading ? 'Building…' : 'Build it'}
      </Button>
    </div>
  )
}
