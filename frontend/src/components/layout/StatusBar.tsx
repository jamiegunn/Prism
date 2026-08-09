import { useInstances } from '@/features/models/api'
import { useProviderCapabilities } from '@/hooks/useProviderCapabilities'
import { usePlaygroundStore } from '@/features/playground/store'

/**
 * Reports the state of the selected inference instance.
 *
 * Previously every value here was hardcoded: a permanently green "Connected" dot, the literal
 * text "No model loaded", and an em dash for GPU. It claimed a healthy connection with the
 * backend switched off, which is worse than showing nothing — a status bar that cannot be
 * wrong is not a status bar.
 */
export function StatusBar() {
  const selectedInstanceId = usePlaygroundStore((s) => s.selectedInstanceId)
  const { data: instances, isLoading, isError } = useInstances()
  const { data: capabilities } = useProviderCapabilities(selectedInstanceId)

  const instance = instances?.find((i) => i.id === selectedInstanceId)
  const connection = describeConnection({ isLoading, isError, hasInstance: Boolean(instance), instance })

  return (
    <div
      data-tour="status-bar"
      className="fixed bottom-0 right-0 left-64 z-20 flex h-8 items-center justify-between border-t border-zinc-800 bg-zinc-900 px-4 text-xs text-zinc-400">
      <div className="flex items-center gap-2">
        <span
          data-testid="connection-dot"
          className={`inline-block h-2 w-2 rounded-full ${connection.dotClass}`}
        />
        <span>{connection.label}</span>
      </div>

      <div className="flex items-center gap-2">
        <span className="text-zinc-500">Model:</span>
        <span>{instance?.modelId ?? 'No model selected'}</span>
      </div>

      <div className="flex items-center gap-2">
        <span className="text-zinc-500">Logprobs:</span>
        <span title="Token-level probabilities are what the heatmap and entropy views are built from.">
          {describeLogprobs(capabilities?.supportsLogprobs, Boolean(instance))}
        </span>
      </div>
    </div>
  )
}

interface ConnectionInput {
  isLoading: boolean
  isError: boolean
  hasInstance: boolean
  instance?: { status?: string }
}

/**
 * Maps the query and instance state onto a label and colour.
 *
 * Exported so the mapping can be tested without mounting the component — the point of this
 * component is that the state it reports is derived rather than asserted.
 */
export function describeConnection({ isLoading, isError, hasInstance, instance }: ConnectionInput): {
  label: string
  dotClass: string
} {
  if (isLoading) return { label: 'Connecting…', dotClass: 'bg-zinc-500 animate-pulse' }
  if (isError) return { label: 'Backend unreachable', dotClass: 'bg-red-500' }
  if (!hasInstance) return { label: 'No instance selected', dotClass: 'bg-zinc-500' }

  const status = instance?.status?.toLowerCase()
  if (status === 'healthy' || status === 'online') return { label: 'Connected', dotClass: 'bg-emerald-500' }
  if (status === 'unhealthy' || status === 'offline') return { label: 'Instance unreachable', dotClass: 'bg-red-500' }

  return { label: 'Status unknown', dotClass: 'bg-amber-500' }
}

/**
 * Describes logprob availability for the status bar.
 *
 * @param supportsLogprobs Whether the provider reports logprob support; undefined while unprobed.
 * @param hasInstance Whether an instance is selected at all.
 * @returns A short label.
 */
export function describeLogprobs(supportsLogprobs: boolean | undefined, hasInstance: boolean): string {
  if (!hasInstance) return '—'
  if (supportsLogprobs === undefined) return 'Unprobed'
  return supportsLogprobs ? 'Available' : 'Unavailable'
}
