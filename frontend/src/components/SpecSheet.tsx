import { specSheet } from '@/lib/format'

export function SpecSheet({
  category,
  specs,
}: {
  category: string
  specs: Record<string, string>
}) {
  const rows = specSheet(category, specs)
  if (rows.length === 0) {
    return <p className="font-mono text-xs text-faint">No specifications available.</p>
  }
  return (
    <dl className="grid grid-cols-2 gap-x-4 gap-y-2">
      {rows.map((r) => (
        <div key={r.label} className="flex flex-col">
          <dt className="font-mono text-[11px] tracking-wide text-faint uppercase">
            {r.label}
          </dt>
          <dd className="font-mono text-xs tabular-nums text-fg">{r.value}</dd>
        </div>
      ))}
    </dl>
  )
}
