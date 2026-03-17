import { Check, X, HelpCircle, RefreshCw } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import { useAllCapabilities } from '../api'
import type { CapabilitySnapshot } from '../api'

const CAPABILITY_COLUMNS = [
  { key: 'supportsLogprobs' as const, label: 'Logprobs', description: 'Token-level probabilities for heatmaps, entropy, and surprise detection' },
  { key: 'supportsTokenize' as const, label: 'Tokenize', description: 'Text-to-token and token-to-text conversion endpoints' },
  { key: 'supportsGuidedDecoding' as const, label: 'Guided', description: 'Constrained generation using JSON schema or regex' },
  { key: 'supportsStreaming' as const, label: 'Stream', description: 'Server-sent events for real-time token streaming' },
  { key: 'supportsMetrics' as const, label: 'Metrics', description: 'GPU utilization, KV cache, and request throughput metrics' },
  { key: 'supportsModelSwap' as const, label: 'Swap', description: 'Hot-swap loaded model without restarting the provider' },
  { key: 'supportsMultimodal' as const, label: 'Multi', description: 'Image and other non-text inputs' },
  { key: 'supportsFunctionCalling' as const, label: 'Tools', description: 'Function/tool calling for agent workflows' },
] as const

const TIER_COLORS: Record<string, string> = {
  Research: 'bg-emerald-900/30 text-emerald-400 border-emerald-700',
  Inspect: 'bg-amber-900/30 text-amber-400 border-amber-700',
  Chat: 'bg-zinc-800 text-zinc-400 border-zinc-700',
  Unknown: 'bg-zinc-900 text-zinc-600 border-zinc-800',
}

export function CapabilityMatrix() {
  const { data: capabilities, isLoading, refetch, isFetching } = useAllCapabilities()

  if (isLoading) {
    return (
      <div className="rounded-lg border border-zinc-800 bg-zinc-900/50 p-6">
        <div className="animate-pulse space-y-3">
          <div className="h-4 w-48 bg-zinc-800 rounded" />
          <div className="h-32 bg-zinc-800 rounded" />
        </div>
      </div>
    )
  }

  if (!capabilities || capabilities.length === 0) {
    return null
  }

  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-900/50">
      <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800">
        <h3 className="text-sm font-medium text-zinc-300">Capability Matrix</h3>
        <Button
          variant="ghost"
          size="sm"
          onClick={() => refetch()}
          disabled={isFetching}
          className="h-7 gap-1.5 text-xs text-zinc-500"
        >
          <RefreshCw className={cn('h-3 w-3', isFetching && 'animate-spin')} />
          Refresh
        </Button>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full text-xs">
          <thead>
            <tr className="border-b border-zinc-800">
              <th className="text-left px-4 py-2 text-zinc-500 font-medium">Provider</th>
              <th className="text-left px-3 py-2 text-zinc-500 font-medium">Tier</th>
              {CAPABILITY_COLUMNS.map((col) => (
                <th key={col.key} className="text-center px-2 py-2">
                  <Tooltip>
                    <TooltipTrigger>
                      <span className="text-zinc-500 font-medium cursor-help">{col.label}</span>
                    </TooltipTrigger>
                    <TooltipContent>{col.description}</TooltipContent>
                  </Tooltip>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {capabilities.map((cap) => (
              <CapabilityRow key={cap.instanceId} capability={cap} />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function CapabilityRow({ capability }: { capability: CapabilitySnapshot }) {
  const tierColor = TIER_COLORS[capability.tier] ?? TIER_COLORS.Unknown

  return (
    <tr className="border-b border-zinc-800/50 hover:bg-zinc-800/30">
      <td className="px-4 py-2">
        <div className="flex items-center gap-2">
          <span className="text-zinc-300 font-medium">{capability.providerName}</span>
          {!capability.probeSucceeded && capability.probeError && (
            <Tooltip>
              <TooltipTrigger>
                <HelpCircle className="h-3 w-3 text-amber-500" />
              </TooltipTrigger>
              <TooltipContent className="max-w-xs">
                <p className="text-amber-400">Probe issue: {capability.probeError}</p>
              </TooltipContent>
            </Tooltip>
          )}
        </div>
      </td>
      <td className="px-3 py-2">
        <Badge variant="outline" className={cn('text-[10px]', tierColor)}>
          {capability.tier}
        </Badge>
      </td>
      {CAPABILITY_COLUMNS.map((col) => (
        <td key={col.key} className="text-center px-2 py-2">
          {capability[col.key] ? (
            <Check className="h-3.5 w-3.5 text-emerald-500 mx-auto" />
          ) : (
            <X className="h-3.5 w-3.5 text-zinc-700 mx-auto" />
          )}
        </td>
      ))}
    </tr>
  )
}
