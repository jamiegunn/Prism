import { cn } from '@/lib/utils'
import type { InferenceInstance } from '../types'

interface StatusIndicatorProps {
  status: InferenceInstance['status']
  className?: string
}

const statusConfig: Record<InferenceInstance['status'], { color: string; label: string; pulse: boolean }> = {
  Online: { color: 'bg-emerald-500', label: 'Online', pulse: true },
  Degraded: { color: 'bg-amber-500', label: 'Degraded', pulse: false },
  Offline: { color: 'bg-red-500', label: 'Offline', pulse: false },
  Unknown: { color: 'bg-zinc-500', label: 'Unknown', pulse: false },
}

export function StatusIndicator({ status, className }: StatusIndicatorProps) {
  const config = statusConfig[status]

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <span className="relative flex h-2.5 w-2.5">
        {config.pulse && (
          <span
            className={cn(
              'absolute inline-flex h-full w-full animate-ping rounded-full opacity-75',
              config.color
            )}
          />
        )}
        <span className={cn('relative inline-flex h-2.5 w-2.5 rounded-full', config.color)} />
      </span>
      <span className="text-sm text-zinc-400">{config.label}</span>
    </div>
  )
}
