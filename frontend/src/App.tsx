import { useCallback, useEffect, useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Boxes, AlertCircle, Sparkles } from 'lucide-react'
import type { Purpose, RecommendationResult } from '@/lib/types'
import { getRecommendation, PURPOSE_DEFAULT_BUDGET } from '@/lib/api'
import { readBottleneck } from '@/lib/bottleneck'
import { sortByCategory } from '@/lib/format'
import { ControlPanel } from '@/components/ControlPanel'
import { BudgetMeter } from '@/components/BudgetMeter'
import { BottleneckMeter } from '@/components/BottleneckMeter'
import { SlotCard } from '@/components/SlotCard'
import { FailedSlots } from '@/components/FailedSlots'
import { Badge } from '@/components/ui/badge'

export default function App() {
  const [purpose, setPurpose] = useState<Purpose>('Gaming')
  const [budget, setBudget] = useState<number>(PURPOSE_DEFAULT_BUDGET.Gaming)
  const [result, setResult] = useState<RecommendationResult | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [runId, setRunId] = useState(0)

  const runRecommend = useCallback(async (b: number, p: Purpose) => {
    setLoading(true)
    setError(null)
    try {
      const res = await getRecommendation({ budget: b, purpose: p })
      setResult(res)
      setRunId((n) => n + 1)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Something went wrong.')
    } finally {
      setLoading(false)
    }
  }, [])

  // Build a recommendation on first load so the page isn't empty.
  useEffect(() => {
    void runRecommend(PURPOSE_DEFAULT_BUDGET.Gaming, 'Gaming')
  }, [runRecommend])

  function handlePurposeChange(p: Purpose) {
    setPurpose(p)
    setBudget(PURPOSE_DEFAULT_BUDGET[p])
  }

  const sortedSlots = result
    ? [...result.slots].sort((a, b) => sortByCategory(a.category) - sortByCategory(b.category))
    : []

  return (
    <div data-purpose={purpose} className="min-h-dvh">
      <div className="mx-auto max-w-6xl px-4 py-8 sm:px-6 sm:py-10">
        {/* Header */}
        <header className="mb-8 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span
              className="grid h-10 w-10 place-items-center rounded-xl text-bg"
              style={{ backgroundColor: 'var(--accent)' }}
            >
              <Boxes size={22} />
            </span>
            <div>
              <h1 className="font-mono text-lg font-bold tracking-tight text-fg">Struct</h1>
              <p className="text-xs text-faint">PC build recommender · engine visualizer</p>
            </div>
          </div>
          <Badge tone="neutral">
            <Sparkles size={12} /> mock data
          </Badge>
        </header>

        <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
          {/* Controls */}
          <div className="lg:col-span-4">
            <div className="lg:sticky lg:top-8">
              <ControlPanel
                budget={budget}
                purpose={purpose}
                loading={loading}
                onBudgetChange={setBudget}
                onPurposeChange={handlePurposeChange}
                onSubmit={() => runRecommend(budget, purpose)}
              />
            </div>
          </div>

          {/* Results */}
          <div className="lg:col-span-8">
            {error && (
              <div className="mb-6 flex items-center gap-2 rounded-xl border border-bad/40 bg-bad/10 p-4 font-mono text-sm text-bad">
                <AlertCircle size={16} />
                {error}
              </div>
            )}

            {result && (
              <AnimatePresence mode="wait">
                <motion.div
                  key={runId}
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  transition={{ duration: 0.25 }}
                  className="space-y-6"
                >
                  {/* Summary line */}
                  <div className="flex flex-wrap items-center gap-2">
                    <Badge tone={result.isSuccess ? 'good' : 'warn'}>
                      {result.isSuccess ? 'Complete build' : 'Partial build'}
                    </Badge>
                    <Badge tone="accent">{result.purpose}</Badge>
                    <span className="text-sm text-muted">{result.message}</span>
                  </div>

                  {/* Meters */}
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
                    <BudgetMeter
                      totalBudget={result.totalBudget}
                      actualTotalPrice={result.actualTotalPrice}
                    />
                    <BottleneckMeter reading={readBottleneck(result)} />
                  </div>

                  {/* Build breakdown */}
                  <div>
                    <h2 className="mb-3 font-mono text-xs tracking-wide text-faint uppercase">
                      The build · {sortedSlots.length} parts
                    </h2>
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
                      {sortedSlots.map((slot, i) => (
                        <SlotCard key={slot.category} slot={slot} index={i} />
                      ))}
                    </div>
                  </div>

                  <FailedSlots slots={result.failedSlots} />
                </motion.div>
              </AnimatePresence>
            )}

            {!result && loading && (
              <div className="grid place-items-center rounded-xl border border-line bg-surface/50 p-16 font-mono text-sm text-faint">
                Building your rig…
              </div>
            )}
          </div>
        </div>

        <footer className="mt-12 border-t border-line pt-4 font-mono text-[11px] text-faint">
          Bottleneck reproduces the engine's own term · gap = |cpuScore − gpuScore|, threshold 0.15
        </footer>
      </div>
    </div>
  )
}
