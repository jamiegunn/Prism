import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { cn, truncate } from '@/lib/utils'
import { StatusIndicator } from './StatusIndicator'
import { CapabilityBadges } from './CapabilityBadges'
import { KvCacheGauge } from './KvCacheGauge'
import { useInstanceMetrics } from '../api'
import type { InferenceInstance } from '../types'

interface InstanceCardProps {
  instance: InferenceInstance
  isSelected: boolean
  onClick: (id: string) => void
}

export function InstanceCard({ instance, isSelected, onClick }: InstanceCardProps) {
  const { data: metrics } = useInstanceMetrics(instance.id)

  return (
    <Card
      className={cn(
        'cursor-pointer transition-colors hover:border-zinc-600',
        isSelected && 'border-violet-500 ring-1 ring-violet-500/50'
      )}
      onClick={() => onClick(instance.id)}
    >
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base font-semibold">{instance.name}</CardTitle>
          <StatusIndicator status={instance.status} />
        </div>
      </CardHeader>

      <CardContent className="space-y-3 pb-3">
        <div className="flex items-center gap-2">
          <Badge variant="outline" className="text-xs">
            {instance.providerType}
          </Badge>
          {instance.isDefault && (
            <Badge variant="default" className="text-xs">
              Default
            </Badge>
          )}
        </div>

        <div className="space-y-1 text-sm">
          <div className="flex items-center justify-between">
            <span className="text-zinc-500">Endpoint</span>
            <span className="font-mono text-xs text-zinc-400">
              {truncate(instance.endpoint, 32)}
            </span>
          </div>
          {instance.modelId && (
            <div className="flex items-center justify-between">
              <span className="text-zinc-500">Model</span>
              <span className="font-mono text-xs text-zinc-300">
                {truncate(instance.modelId, 28)}
              </span>
            </div>
          )}
        </div>

        {metrics && (
          <div className="space-y-1 border-t border-zinc-800 pt-2">
            {metrics.gpuUtilization != null && (
              <div className="flex items-center justify-between text-sm">
                <span className="text-zinc-500">GPU</span>
                <span className="text-zinc-300">{metrics.gpuUtilization.toFixed(0)}%</span>
              </div>
            )}
            {metrics.kvCacheUtilization != null && (
              <div className="flex items-center justify-between text-sm">
                <span className="text-zinc-500">KV Cache</span>
                <KvCacheGauge utilization={metrics.kvCacheUtilization} />
              </div>
            )}
          </div>
        )}
      </CardContent>

      <CardFooter className="pt-0">
        <CapabilityBadges instance={instance} />
      </CardFooter>
    </Card>
  )
}
