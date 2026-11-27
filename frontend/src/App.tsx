import { useCallback, useEffect, useState } from 'react'
import {
  AlertCircle,
  Box,
  Boxes,
  Briefcase,
  Building2,
  CircuitBoard,
  Cpu,
  ExternalLink,
  Fan,
  Gamepad2,
  HardDrive,
  Loader2,
  MemoryStick,
  MonitorPlay,
  Power,
  type LucideIcon,
} from 'lucide-react'
import type { AppConfig, Purpose, RecommendationResult, SlotRecommendation } from '@/lib/types'
import { getConfig, getRecommendation, PURPOSE_DEFAULT_BUDGET, PURPOSE_LABEL } from '@/lib/api'
import { readBottleneck, shouldShowBottleneck, STATUS_LABEL } from '@/lib/bottleneck'
import { categoryLabel, formatPln, productUrl, sortByCategory, specSheet } from '@/lib/format'
import { Slider } from '@/components/ui/slider'
import { cn } from '@/lib/utils'

const MAX_BUDGET = 25000

const PURPOSE_ICON: Record<Purpose, LucideIcon> = {
  Gaming: Gamepad2,
  Work: Briefcase,
  Office: Building2,
}

const CATEGORY_ICON: Record<string, LucideIcon> = {
  Cpu,
  Gpu: MonitorPlay,
  Motherboard: CircuitBoard,
  Ram: MemoryStick,
  Ssd: HardDrive,
  Hdd: HardDrive,
  Psu: Power,
  Case: Box,
  Cooler: Fan,
}

export default function App() {
  const [config, setConfig] = useState<AppConfig | null>(null)
  const [purpose, setPurpose] = useState<Purpose>('Gaming')
  const [budget, setBudget] = useState<number>(PURPOSE_DEFAULT_BUDGET.Gaming)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  // State is set only in promise callbacks, so the mount effect below never sets state synchronously.
  const fetchRecommendation = useCallback(
    (b: number, p: Purpose) =>
      getRecommendation({ budget: b, purpose: p })
        .then(setResult, (e: unknown) => setError(e instanceof Error ? e.message : 'Something went wrong.'))
        .finally(() => setLoading(false)),
    [],
  )

  useEffect(() => {
    getConfig().then(setConfig, (e: unknown) =>
      setError(e instanceof Error ? e.message : 'Could not load engine config.'),
    )
  }, [])

  // Build once on first load so the page isn't empty (loading starts true).
  useEffect(() => {
    void fetchRecommendation(PURPOSE_DEFAULT_BUDGET.Gaming, 'Gaming')
  }, [fetchRecommendation])

  function build() {
    setLoading(true)
    setError(null)
    void fetchRecommendation(budget, purpose)
  }

  function choosePurpose(p: Purpose) {
    setPurpose(p)
    setBudget(PURPOSE_DEFAULT_BUDGET[p])
  }

  return (
    <div className="min-h-dvh">
      <main className="mx-auto flex max-w-2xl flex-col gap-6 px-4 py-12 sm:py-16">
        <header className="text-center">
          <div className="mx-auto mb-4 grid h-12 w-12 place-items-center rounded-2xl bg-fg text-white shadow-sm">
            <Boxes size={24} />
          </div>
          <h1 className="text-3xl font-semibold tracking-tight text-fg sm:text-4xl">Struct</h1>
          <p className="mt-2 text-muted">Complete, compatible PC builds for your budget.</p>
        </header>

        {config && (
          <section className="card p-5 sm:p-6">
            <div role="group" aria-label="Purpose" className="grid grid-cols-3 gap-1 rounded-xl bg-surface-2 p-1">
              {config.purposes.map((p) => {
                const Icon = PURPOSE_ICON[p]
                const active = p === purpose
                return (
                  <button
                    key={p}
                    type="button"
                    aria-pressed={active}
                    onClick={() => choosePurpose(p)}
                    className={cn(
                      'flex cursor-pointer items-center justify-center gap-2 rounded-lg px-3 py-2.5 text-sm font-medium transition',
                      active ? 'bg-surface text-fg shadow-sm' : 'text-muted hover:text-fg',
                    )}
                  >
                    <Icon size={16} aria-hidden />
                    <span>{PURPOSE_LABEL[p]}</span>
                  </button>
                )
              })}
            </div>

            <div className="mt-6">
              <div className="mb-3 flex items-baseline justify-between">
                <span className="text-sm font-medium text-muted">Budget</span>
                <span className="text-2xl font-semibold tabular-nums text-fg">{formatPln(budget)}</span>
              </div>
              <Slider value={budget} min={config.minBudget} max={MAX_BUDGET} step={100} onValueChange={setBudget} />
              <div className="mt-2 flex justify-between text-xs text-faint tabular-nums">
                <span>{formatPln(config.minBudget)}</span>
                <span>{formatPln(MAX_BUDGET)}</span>
              </div>
            </div>

            <button
              type="button"
              onClick={build}
              disabled={loading}
              className="mt-6 flex w-full cursor-pointer items-center justify-center gap-2 rounded-xl bg-[var(--accent)] px-4 py-3 font-medium text-white shadow-sm transition hover:brightness-110 disabled:cursor-wait disabled:opacity-70"
            >
              {loading && <Loader2 size={18} className="animate-spin" aria-hidden />}
              {loading ? 'Building…' : 'Build it'}
            </button>
          </section>
        )}

        {error && (
          <div role="alert" className="flex items-center gap-2 rounded-xl border border-bad/30 bg-bad/5 p-4 text-sm text-bad">
            <AlertCircle size={16} aria-hidden />
            {error}
          </div>
        )}

        {result && config && <Result result={result} config={config} />}

        {!result && loading && (
          <div className="card grid place-items-center p-12 text-sm text-faint">Building your PC…</div>
        )}

        <footer className="text-center text-xs text-faint">
          Multi-start local search over x-kom prices (26 Nov 2026) · performance from PassMark
        </footer>
      </main>
    </div>
  )
}

function Result({ result, config }: { result: RecommendationResult; config: AppConfig }) {
  const slots = [...result.slots].sort((a, b) => sortByCategory(a.category) - sortByCategory(b.category))
  const spent = Math.min(100, (result.actualTotalPrice / result.totalBudget) * 100)
  const balance = shouldShowBottleneck(config, result)
    ? STATUS_LABEL[readBottleneck(result, config.bottleneckThreshold).status]
    : null

  return (
    <section className="card overflow-hidden">
      <div className="p-5 sm:p-6">
        <div className="flex flex-wrap items-center gap-2">
          <span className={cn('chip', result.isSuccess ? 'text-good' : 'text-warn')}>
            <span className={cn('h-1.5 w-1.5 rounded-full', result.isSuccess ? 'bg-good' : 'bg-warn')} />
            {result.isSuccess ? 'Complete build' : 'Partial build'}
          </span>
          <span className="chip text-muted">{PURPOSE_LABEL[result.purpose as Purpose] ?? result.purpose}</span>
          {balance && <span className="chip text-muted">{balance}</span>}
        </div>

        <div className="mt-5 flex items-baseline justify-between">
          <span className="text-3xl font-semibold tabular-nums text-fg">{formatPln(result.actualTotalPrice)}</span>
          <span className="text-sm text-muted tabular-nums">of {formatPln(result.totalBudget)}</span>
        </div>
        <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-surface-2">
          <div className="h-full rounded-full bg-[var(--accent)]" style={{ width: `${spent}%` }} />
        </div>
      </div>

      <h2 className="border-t border-line px-5 pt-4 pb-2 text-xs font-medium tracking-wide text-faint uppercase sm:px-6">
        The build · {slots.length} parts
      </h2>
      <ul className="divide-y divide-line">
        {slots.map((slot) => (
          <PartRow key={slot.category} slot={slot} />
        ))}
      </ul>

      {result.failedSlots.length > 0 && (
        <div className="border-t border-line bg-warn/5 px-5 py-4 text-sm sm:px-6">
          <p className="font-medium text-warn">Could not fill</p>
          <ul className="mt-1 space-y-1 text-muted">
            {result.failedSlots.map((f) => (
              <li key={f.category}>
                <span className="font-medium text-fg">{categoryLabel(f.category)}</span> — {f.reason}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  )
}

function PartRow({ slot }: { slot: SlotRecommendation }) {
  const top = slot.recommendations.find((r) => r.rank === 1) ?? slot.recommendations[0]
  if (!top) return null
  const Icon = CATEGORY_ICON[slot.category] ?? Box
  const part = top.component
  const specs = specSheet(slot.category, part.technicalSpecs ?? {})
    .slice(0, 3)
    // A bare number ("14") means nothing without its label; values with units or words read on their own.
    .map((s) => (/^[\d.,]+$/.test(s.value) ? `${s.label} ${s.value}` : s.value))
    .join(' · ')
  const url = productUrl(part.sourceUrl)

  return (
    <li className="flex items-center gap-4 px-5 py-3.5 sm:px-6">
      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-surface-2 text-muted">
        <Icon size={18} aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-xs text-faint">{categoryLabel(slot.category)}</p>
        <p className="truncate font-medium text-fg" title={part.name}>
          {part.name}
        </p>
        {specs && <p className="truncate text-xs text-muted">{specs}</p>}
      </div>
      <div className="flex shrink-0 flex-col items-end gap-1.5">
        <span className="text-sm font-medium tabular-nums text-fg">{formatPln(part.price)}</span>
        <span className="h-1 w-16 overflow-hidden rounded-full bg-surface-2" title={`Score ${top.performanceScore.toFixed(2)}`}>
          <span className="block h-full rounded-full bg-[var(--accent)]" style={{ width: `${Math.round(top.performanceScore * 100)}%` }} />
        </span>
      </div>
      {url ? (
        <a
          href={url}
          target="_blank"
          rel="noreferrer"
          aria-label={`Open ${part.name} on x-kom`}
          className="shrink-0 text-faint transition hover:text-fg"
        >
          <ExternalLink size={16} />
        </a>
      ) : (
        <span className="w-4 shrink-0" />
      )}
    </li>
  )
}
