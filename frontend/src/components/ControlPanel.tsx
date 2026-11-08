import { Gamepad2, Briefcase, Building2, Cpu, type LucideIcon } from 'lucide-react'
import type { Purpose } from '@/lib/types'
import { PURPOSE_LABEL } from '@/lib/api'
import { formatPln } from '@/lib/format'
import { Button } from '@/components/ui/button'
import { Slider } from '@/components/ui/slider'
import { cn } from '@/lib/utils'

const MAX_BUDGET = 25000

const PURPOSE_META: Record<Purpose, { icon: LucideIcon; blurb: string }> = {
  Gaming: { icon: Gamepad2, blurb: 'GPU-first build tuned for high frame rates.' },
  Work: { icon: Briefcase, blurb: 'Balanced cores, memory & GPU for work plus play.' },
  Office: { icon: Building2, blurb: 'Efficient, quiet everyday essentials.' },
}

interface ControlPanelProps {
  budget: number
  purpose: Purpose
  purposes: Purpose[]
  minBudget: number
  loading: boolean
  onBudgetChange: (b: number) => void
  onPurposeChange: (p: Purpose) => void
  onSubmit: () => void
}

export function ControlPanel({
  budget,
  purpose,
  purposes,
  minBudget,
  loading,
  onBudgetChange,
  onPurposeChange,
  onSubmit,
}: ControlPanelProps) {
  const tooLow = budget < minBudget

  return (
    <div className="rounded-2xl border border-line bg-surface p-5 shadow-[0_1px_2px_rgba(15,27,45,0.04),0_14px_38px_-20px_rgba(15,27,45,0.22)] sm:p-6">
      {/* Purpose segmented control */}
      <fieldset>
        <legend className="mb-2 font-mono text-xs tracking-wide text-faint uppercase">
          Purpose
        </legend>
        <div className="grid grid-cols-3 gap-2">
          {purposes.map((p) => {
            const Icon = PURPOSE_META[p].icon
            const active = p === purpose
            return (
              <button
                key={p}
                type="button"
                onClick={() => onPurposeChange(p)}
                aria-pressed={active}
                className={cn(
                  'flex cursor-pointer flex-col items-center gap-2 rounded-xl border px-2 py-3 transition-colors duration-150',
                  active
                    ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
                    : 'border-line bg-surface-2/40 hover:border-faint',
                )}
              >
                <span
                  className={cn(
                    'grid h-9 w-9 shrink-0 place-items-center rounded-lg transition-colors',
                    active ? 'bg-[var(--accent)] text-[var(--on-accent)]' : 'bg-surface-2 text-muted',
                  )}
                >
                  <Icon size={18} strokeWidth={2} />
                </span>
                <span
                  className={cn(
                    'font-mono text-sm font-semibold',
                    active ? 'text-fg' : 'text-muted',
                  )}
                >
                  {PURPOSE_LABEL[p]}
                </span>
              </button>
            )
          })}
        </div>
        <p className="mt-2 min-h-8 text-xs leading-relaxed text-faint">
          {PURPOSE_META[purpose].blurb}
        </p>
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
          min={minBudget}
          max={MAX_BUDGET}
          step={100}
          onValueChange={onBudgetChange}
        />
        <div className="mt-1 flex justify-between font-mono text-[11px] text-faint tabular-nums">
          <span>{formatPln(minBudget)}</span>
          <span>{formatPln(MAX_BUDGET)}</span>
        </div>
        <input
          id="budget"
          type="number"
          min={minBudget}
          max={MAX_BUDGET}
          step={100}
          value={budget}
          onChange={(e) => onBudgetChange(Number(e.target.value) || 0)}
          className="mt-3 w-full rounded-lg border border-line bg-surface-2/50 px-3 py-2 font-mono text-sm tabular-nums text-fg outline-none focus-visible:border-[var(--accent)] focus-visible:ring-2 focus-visible:ring-[var(--accent)]"
        />
        {tooLow && (
          <p role="alert" className="mt-2 font-mono text-xs text-bad">
            Minimum budget is {formatPln(minBudget)} for a valid build.
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
