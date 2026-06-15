import { motion } from 'framer-motion'
import * as Collapsible from '@radix-ui/react-collapsible'
import {
  Cpu,
  MonitorPlay,
  CircuitBoard,
  MemoryStick,
  HardDrive,
  Power,
  Box,
  Fan,
  ChevronDown,
  Layers,
  type LucideIcon,
} from 'lucide-react'
import type { SlotRecommendation } from '@/lib/types'
import { SpecSheet } from '@/components/SpecSheet'
import { categoryLabel, formatPln } from '@/lib/format'

const ICONS: Record<string, LucideIcon> = {
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

const BAR_COLOR: Record<string, string> = {
  Cpu: 'var(--color-cpu)',
  Gpu: 'var(--color-gpu)',
}

export function BuildList({ slots }: { slots: SlotRecommendation[] }) {
  const total = slots.reduce((sum, s) => {
    const top = s.recommendations.find((r) => r.rank === 1) ?? s.recommendations[0]
    return sum + (top?.component.price ?? 0)
  }, 0)

  return (
    <div className="overflow-hidden rounded-xl border border-line bg-surface shadow-[0_1px_2px_rgba(15,27,45,0.04),0_10px_30px_-16px_rgba(15,27,45,0.18)]">
      {/* column header (desktop) */}
      <div className="hidden grid-cols-[minmax(0,1fr)_84px_104px_28px] items-center gap-3 border-b border-line bg-surface-2/60 px-4 py-2 font-mono text-[10px] tracking-wider text-faint uppercase sm:grid">
        <span>Component</span>
        <span className="text-right">Score</span>
        <span className="text-right">Price</span>
        <span />
      </div>

      <ul className="divide-y divide-line">
        {slots.map((slot, i) => (
          <BuildRow key={slot.category} slot={slot} index={i} />
        ))}
      </ul>

      {/* total footer */}
      <div className="flex items-center justify-between border-t border-line bg-surface-2/40 px-4 py-3">
        <span className="font-mono text-xs tracking-wide text-faint uppercase">
          {slots.length} parts · total
        </span>
        <span className="font-mono text-base font-semibold tabular-nums text-fg">
          {formatPln(total)}
        </span>
      </div>
    </div>
  )
}

function BuildRow({ slot, index }: { slot: SlotRecommendation; index: number }) {
  const top = slot.recommendations.find((r) => r.rank === 1) ?? slot.recommendations[0]
  if (!top) return null

  const alts = slot.recommendations.filter((r) => r !== top)
  const Icon = ICONS[slot.category] ?? Box
  const color = BAR_COLOR[slot.category] ?? 'var(--accent)'
  const c = top.component
  const pct = Math.round(top.performanceScore * 100)

  return (
    <motion.li
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.28, delay: index * 0.03, ease: 'easeOut' }}
    >
      <Collapsible.Root className="group/row">
        <Collapsible.Trigger className="grid w-full cursor-pointer grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 px-4 py-3 text-left transition-colors hover:bg-surface-2/50 sm:grid-cols-[auto_minmax(0,1fr)_84px_104px_28px]">
          {/* icon */}
          <span
            className="grid h-9 w-9 shrink-0 place-items-center rounded-lg"
            style={{ backgroundColor: 'var(--accent-soft)', color }}
          >
            <Icon size={18} />
          </span>

          {/* name + meta */}
          <span className="min-w-0">
            <span className="block font-mono text-[10px] tracking-wider text-faint uppercase">
              {categoryLabel(slot.category)}
            </span>
            <span className="block truncate text-sm font-semibold text-fg">{c.name}</span>
            <span className="font-mono text-[11px] text-faint">{c.brand}</span>
          </span>

          {/* score (desktop col) */}
          <span className="hidden flex-col items-end gap-1 sm:flex">
            <span className="font-mono text-xs font-semibold tabular-nums" style={{ color }}>
              {pct}%
            </span>
            <span className="h-1.5 w-16 overflow-hidden rounded-full bg-surface-2">
              <span
                className="block h-full rounded-full"
                style={{ width: `${Math.min(100, pct)}%`, backgroundColor: color }}
              />
            </span>
          </span>

          {/* price */}
          <span className="flex items-center justify-end gap-2 sm:contents">
            <span className="font-mono text-xs font-semibold tabular-nums text-fg sm:hidden">
              {pct}%
            </span>
            <span className="text-right font-mono text-sm font-semibold tabular-nums text-fg">
              {formatPln(c.price)}
            </span>
          </span>

          {/* chevron */}
          <ChevronDown
            size={16}
            className="hidden shrink-0 text-faint transition-transform duration-200 group-data-[state=open]/row:rotate-180 sm:block"
          />
        </Collapsible.Trigger>

        <Collapsible.Content className="overflow-hidden">
          <div className="space-y-4 border-t border-line bg-surface-2/30 px-4 py-4">
            <div className="flex items-center justify-between font-mono text-[11px] text-faint tabular-nums">
              <span>Allocated slot budget</span>
              <span>{formatPln(slot.allocatedBudget)}</span>
            </div>

            <SpecSheet category={slot.category} specs={c.technicalSpecs} />

            {alts.length > 0 && (
              <div>
                <p className="mb-2 flex items-center gap-1.5 font-mono text-[11px] tracking-wide text-faint uppercase">
                  <Layers size={12} /> Alternatives ({alts.length})
                </p>
                <ul className="space-y-1.5">
                  {alts.map((a) => (
                    <li
                      key={a.component.id}
                      className="flex items-center justify-between gap-2 rounded-lg bg-surface px-3 py-2"
                    >
                      <span className="min-w-0 truncate text-xs text-fg">{a.component.name}</span>
                      <span className="shrink-0 font-mono text-[11px] text-faint tabular-nums">
                        {Math.round(a.performanceScore * 100)}% · {formatPln(a.component.price)}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </Collapsible.Content>
      </Collapsible.Root>
    </motion.li>
  )
}
