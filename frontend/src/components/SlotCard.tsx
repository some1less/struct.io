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
import { Card } from '@/components/ui/card'
import { ScoreBar } from '@/components/ScoreBar'
import { SpecSheet } from '@/components/SpecSheet'
import { categoryLabel, formatPln } from '@/lib/format'
import { cn } from '@/lib/utils'

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

export function SlotCard({ slot, index }: { slot: SlotRecommendation; index: number }) {
  const top = slot.recommendations.find((r) => r.rank === 1) ?? slot.recommendations[0]
  if (!top) return null

  const alts = slot.recommendations.filter((r) => r !== top)
  const Icon = ICONS[slot.category] ?? Box
  const color = BAR_COLOR[slot.category] ?? 'var(--accent)'
  const c = top.component
  const pct = Math.round(top.performanceScore * 100)

  return (
    <motion.div
      initial={{ opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, delay: index * 0.04, ease: 'easeOut' }}
    >
      <Card className="flex h-full flex-col p-4">
        {/* header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span
              className="grid h-8 w-8 place-items-center rounded-lg"
              style={{ backgroundColor: 'var(--accent-soft)', color }}
            >
              <Icon size={17} />
            </span>
            <span className="font-mono text-xs tracking-wide text-faint uppercase">
              {categoryLabel(slot.category)}
            </span>
          </div>
          <span className="font-mono text-xs tabular-nums" style={{ color }}>
            {pct}%
          </span>
        </div>

        {/* name + brand */}
        <p className="mt-3 text-sm leading-snug font-semibold text-fg">{c.name}</p>
        <p className="mt-0.5 font-mono text-xs text-faint">{c.brand}</p>

        {/* score bar */}
        <div className="mt-3">
          <ScoreBar score={top.performanceScore} color={color} />
        </div>

        {/* price */}
        <div className="mt-3 flex items-baseline justify-between">
          <span className="font-mono text-base font-semibold tabular-nums text-fg">
            {formatPln(c.price)}
          </span>
          <span className="font-mono text-[11px] text-faint tabular-nums">
            slot {formatPln(slot.allocatedBudget)}
          </span>
        </div>

        <div className="mt-3 flex-1" />

        {/* specs toggle */}
        <Collapsible.Root className="mt-3 border-t border-line pt-3">
          <Collapsible.Trigger className="group flex w-full cursor-pointer items-center justify-between font-mono text-xs text-muted hover:text-fg">
            Specs
            <ChevronDown
              size={14}
              className="transition-transform duration-200 group-data-[state=open]:rotate-180"
            />
          </Collapsible.Trigger>
          <Collapsible.Content className="overflow-hidden">
            <div className="pt-3">
              <SpecSheet category={slot.category} specs={c.technicalSpecs} />
            </div>
          </Collapsible.Content>
        </Collapsible.Root>

        {/* alternatives */}
        {alts.length > 0 && (
          <Collapsible.Root className="mt-2 border-t border-line pt-3">
            <Collapsible.Trigger className="group flex w-full cursor-pointer items-center justify-between font-mono text-xs text-muted hover:text-fg">
              <span className="flex items-center gap-1.5">
                <Layers size={13} /> Alternatives ({alts.length})
              </span>
              <ChevronDown
                size={14}
                className="transition-transform duration-200 group-data-[state=open]:rotate-180"
              />
            </Collapsible.Trigger>
            <Collapsible.Content className="overflow-hidden">
              <ul className="space-y-2 pt-3">
                {alts.map((a) => (
                  <li
                    key={a.component.id}
                    className="flex items-center justify-between gap-2 rounded-lg bg-surface-2/50 px-3 py-2"
                  >
                    <span className="min-w-0">
                      <span className="block truncate text-xs text-fg">{a.component.name}</span>
                      <span className="font-mono text-[11px] text-faint tabular-nums">
                        {Math.round(a.performanceScore * 100)}% · {formatPln(a.component.price)}
                      </span>
                    </span>
                    <span
                      className={cn(
                        'shrink-0 rounded-md px-1.5 py-0.5 font-mono text-[10px]',
                        'bg-surface text-faint',
                      )}
                    >
                      #{a.rank}
                    </span>
                  </li>
                ))}
              </ul>
            </Collapsible.Content>
          </Collapsible.Root>
        )}
      </Card>
    </motion.div>
  )
}
