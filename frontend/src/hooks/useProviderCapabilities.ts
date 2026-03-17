import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'

interface CapabilitySnapshot {
  instanceId: string
  providerName: string
  tier: string
  supportsLogprobs: boolean
  maxLogprobs: number
  supportsTokenize: boolean
  supportsGuidedDecoding: boolean
  supportsStreaming: boolean
  supportsMetrics: boolean
  supportsModelSwap: boolean
  supportsMultimodal: boolean
  probeSucceeded: boolean
  probeError: string | null
}

/**
 * Fetches the cached capability snapshot for a specific provider instance.
 * Returns null capabilities if the instance ID is not set.
 */
export function useProviderCapabilities(instanceId: string | null) {
  return useQuery({
    queryKey: ['models', 'instances', instanceId, 'capabilities'],
    queryFn: () => apiClient<CapabilitySnapshot>(`/models/instances/${instanceId}/capabilities`),
    enabled: !!instanceId,
    staleTime: 60_000,
  })
}

/**
 * Returns whether a specific capability is supported by the current instance.
 * Returns true by default (permissive) when capabilities haven't loaded yet.
 */
export function useCapabilityCheck(instanceId: string | null, capability: keyof CapabilitySnapshot): boolean {
  const { data } = useProviderCapabilities(instanceId)
  if (!data) return true // permissive default while loading
  const value = data[capability]
  return typeof value === 'boolean' ? value : true
}
