// Display helpers: PLN money, category labels/icons, and per-category spec sheets.

const plnFmt = new Intl.NumberFormat('pl-PL', {
  style: 'currency',
  currency: 'PLN',
  maximumFractionDigits: 0,
})

export function formatPln(value: number): string {
  return plnFmt.format(Math.round(value))
}

// Canonical fill order used by the engine (GPU-first for gaming, etc.).
export const CATEGORY_ORDER = [
  'Cpu',
  'Gpu',
  'Motherboard',
  'Ram',
  'Ssd',
  'Hdd',
  'Psu',
  'Case',
  'Cooler',
]

export const CATEGORY_LABEL: Record<string, string> = {
  Cpu: 'Processor',
  Gpu: 'Graphics Card',
  Motherboard: 'Motherboard',
  Ram: 'Memory',
  Ssd: 'Storage',
  Hdd: 'Storage',
  Psu: 'Power Supply',
  Case: 'Case',
  Cooler: 'CPU Cooler',
}

export function categoryLabel(category: string): string {
  return CATEGORY_LABEL[category] ?? category
}

export function sortByCategory(category: string): number {
  const i = CATEGORY_ORDER.indexOf(category)
  return i === -1 ? 999 : i
}

// Which specs to surface, in order, with a friendly label and optional unit/transform.
type SpecField = { key: string; label: string; suffix?: string }

const SPEC_FIELDS: Record<string, SpecField[]> = {
  Cpu: [
    { key: 'Cores', label: 'Cores' },
    { key: 'Threads', label: 'Threads' },
    { key: 'BoostClock', label: 'Boost clock' },
    { key: 'TDP', label: 'TDP', suffix: ' W' },
    { key: 'Socket', label: 'Socket' },
    { key: 'MemoryType', label: 'Memory' },
  ],
  Gpu: [
    { key: 'Chipset', label: 'Chipset' },
    { key: 'VRAM', label: 'VRAM', suffix: ' GB' },
    { key: 'CoreClock', label: 'Core clock' },
    { key: 'Length', label: 'Length', suffix: ' mm' },
    { key: 'TDP', label: 'TDP', suffix: ' W' },
    { key: 'Interface', label: 'Interface' },
  ],
  Motherboard: [
    { key: 'Socket', label: 'Socket' },
    { key: 'FormFactor', label: 'Form factor' },
    { key: 'RamType', label: 'Memory' },
    { key: 'RamSlots', label: 'RAM slots' },
    { key: 'MaxRam', label: 'Max RAM', suffix: ' GB' },
  ],
  Ram: [
    { key: 'Capacity', label: 'Capacity', suffix: ' GB' },
    { key: 'Type', label: 'Type' },
    { key: 'Speed', label: 'Speed', suffix: ' MT/s' },
    { key: 'Modules', label: 'Modules' },
  ],
  Ssd: [
    { key: 'Capacity', label: 'Capacity', suffix: ' GB' },
    { key: 'Type', label: 'Type' },
    { key: 'FormFactor', label: 'Form factor' },
    { key: 'Interface', label: 'Interface' },
  ],
  Hdd: [
    { key: 'Capacity', label: 'Capacity', suffix: ' GB' },
    { key: 'Type', label: 'Type' },
    { key: 'FormFactor', label: 'Form factor' },
    { key: 'Interface', label: 'Interface' },
  ],
  Psu: [
    { key: 'Wattage', label: 'Wattage', suffix: ' W' },
    { key: 'Efficiency', label: 'Efficiency' },
    { key: 'Modular', label: 'Modular' },
    { key: 'FormFactor', label: 'Form factor' },
  ],
  Case: [
    { key: 'FormFactor', label: 'Form factor' },
    { key: 'MaxGpuLength', label: 'Max GPU', suffix: ' mm' },
    { key: 'SupportedMotherboards', label: 'Boards' },
    { key: 'SidePanel', label: 'Side panel' },
  ],
  Cooler: [
    { key: 'WaterCooled', label: 'Type' },
    { key: 'RadiatorSize', label: 'Radiator', suffix: ' mm' },
    { key: 'Height', label: 'Height', suffix: ' mm' },
    { key: 'CpuSockets', label: 'Sockets' },
  ],
}

export interface DisplaySpec {
  label: string
  value: string
}

export function specSheet(
  category: string,
  specs: Record<string, string>,
): DisplaySpec[] {
  const fields = SPEC_FIELDS[category]
  const out: DisplaySpec[] = []
  if (fields) {
    for (const f of fields) {
      const raw = specs[f.key]
      if (raw == null || raw === '' || raw === '0') continue
      let value = raw
      if (f.key === 'WaterCooled') value = raw === 'True' ? 'Liquid (AIO)' : 'Air'
      out.push({ label: f.label, value: value + (f.suffix ?? '') })
    }
  } else {
    for (const [k, v] of Object.entries(specs)) out.push({ label: k, value: v })
  }
  return out
}
