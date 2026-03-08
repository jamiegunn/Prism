import { cn } from '@/lib/utils'

interface KvCacheGaugeProps {
  utilization: number | null | undefined
  className?: string
}

function getBarColor(value: number): string {
  if (value > 90) return 'bg-red-500'
  if (value >= 70) return 'bg-amber-500'
  return 'bg-emerald-500'
}

export function KvCacheGauge({ utilization, className }: KvCacheGaugeProps) {
  if (utilization == null) {
    return <span className={cn('text-sm text-zinc-500', className)}>N/A</span>
  }

  const clamped = Math.min(100, Math.max(0, utilization))

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <div className="h-2 w-20 overflow-hidden rounded-full bg-zinc-800">
        <div
          className={cn('h-full rounded-full transition-all', getBarColor(clamped))}
          style={{ width: `${clamped}%` }}
        />
      </div>
      <span className="text-xs text-zinc-400">{clamped.toFixed(0)}%</span>
    </div>
  )
}
