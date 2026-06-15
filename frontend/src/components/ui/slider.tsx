import * as SliderPrimitive from '@radix-ui/react-slider'
import { cn } from '@/lib/utils'

interface SliderProps {
  value: number
  min: number
  max: number
  step?: number
  onValueChange: (v: number) => void
  className?: string
}

export function Slider({
  value,
  min,
  max,
  step = 100,
  onValueChange,
  className,
}: SliderProps) {
  return (
    <SliderPrimitive.Root
      className={cn('relative flex h-5 w-full touch-none items-center select-none', className)}
      value={[value]}
      min={min}
      max={max}
      step={step}
      onValueChange={(v) => onValueChange(v[0])}
    >
      <SliderPrimitive.Track className="relative h-1.5 w-full grow rounded-full bg-surface-2">
        <SliderPrimitive.Range className="absolute h-full rounded-full bg-[var(--accent)]" />
      </SliderPrimitive.Track>
      <SliderPrimitive.Thumb
        aria-label="Budget"
        className="block h-5 w-5 cursor-grab rounded-full border-2 border-[var(--accent)] bg-bg shadow-md transition-transform outline-none focus-visible:ring-2 focus-visible:ring-[var(--accent)] focus-visible:ring-offset-2 focus-visible:ring-offset-bg active:scale-110 active:cursor-grabbing"
      />
    </SliderPrimitive.Root>
  )
}
