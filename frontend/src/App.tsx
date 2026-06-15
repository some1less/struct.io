import { useCallback, useEffect, useState } from 'react'
import { motion, AnimatePresence } from 'framer-motion'
import { Boxes, AlertCircle, Sparkles, Radio } from 'lucide-react'
import type { Purpose, RecommendationResult } from '@/lib/types'
import { getRecommendation, IS_LIVE, PURPOSE_DEFAULT_BUDGET, PURPOSE_LABEL } from '@/lib/api'
import { readBottleneck } from '@/lib/bottleneck'
import { sortByCategory } from '@/lib/format'
import { ControlPanel } from '@/components/ControlPanel'
import { BudgetMeter } from '@/components/BudgetMeter'
import { BottleneckMeter } from '@/components/BottleneckMeter'
import { BuildList } from '@/components/BuildList'
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
              className="grid h-10 w-10 place-items-center rounded-xl shadow-sm shadow-[var(--accent-2-soft)] ring-1 ring-black/5"
              style={{ backgroundColor: 'var(--accent-2)', color: 'var(--on-accent-2)' }}
            >
              <Boxes size={22} />
            </span>
            <div>
              <h1 className="font-mono text-lg font-bold tracking-tight text-fg">Struct</h1>
              <p className="text-xs text-faint">PC build recommender · engine visualizer</p>
            </div>
          </div>
          {IS_LIVE ? (
            <Badge tone="good">
              <Radio size={12} /> live data
            </Badge>
          ) : (
            <Badge tone="neutral">
              <Sparkles size={12} /> mock data
            </Badge>
          )}
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
                    <Badge tone="accent">
                      {PURPOSE_LABEL[result.purpose as Purpose] ?? result.purpose}
                    </Badge>
                    <span className="text-sm text-muted">{result.message}</span>
                  </div>

                  {/* Meters — budget gets less width, bottleneck gets room to breathe */}
                  <div className="grid grid-cols-1 gap-6 md:grid-cols-5">
                    <div className="md:col-span-2">
                      <BudgetMeter
                        totalBudget={result.totalBudget}
                        actualTotalPrice={result.actualTotalPrice}
                      />
                    </div>
                    <div className="md:col-span-3">
                      <BottleneckMeter reading={readBottleneck(result)} />
                    </div>
                  </div>

                  {/* Build breakdown */}
                  <div>
                    <h2 className="mb-3 font-mono text-xs tracking-wide text-faint uppercase">
                      The build · {sortedSlots.length} parts
                    </h2>
                    <BuildList slots={sortedSlots} />
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
