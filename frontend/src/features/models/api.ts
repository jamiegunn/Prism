import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { InferenceInstance, InstanceMetrics, RegisterInstanceRequest } from './types'

const MODELS_KEY = ['models', 'instances']

export function useInstances() {
  return useQuery({
    queryKey: MODELS_KEY,
    queryFn: () => apiClient<InferenceInstance[]>('/models/instances'),
  })
}

export function useInstance(id: string) {
  return useQuery({
    queryKey: [...MODELS_KEY, id],
    queryFn: () => apiClient<InferenceInstance>(`/models/instances/${id}`),
    enabled: !!id,
  })
}

export function useInstanceMetrics(id: string) {
  return useQuery({
    queryKey: [...MODELS_KEY, id, 'metrics'],
    queryFn: () => apiClient<InstanceMetrics>(`/models/instances/${id}/metrics`),
    enabled: !!id,
    refetchInterval: 5000,
  })
}

export function useRegisterInstance() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: RegisterInstanceRequest) =>
      apiClient<InferenceInstance>('/models/instances', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MODELS_KEY }),
  })
}

export function useUnregisterInstance() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/models/instances/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MODELS_KEY }),
  })
}

export function useSwapModel(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (modelId: string) =>
      apiClient<InferenceInstance>(`/models/instances/${id}/swap-model`, {
        method: 'POST',
        body: { modelId },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MODELS_KEY }),
  })
}

export function useTriggerHealthCheck(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () =>
      apiClient<InferenceInstance>(`/models/instances/${id}/health-check`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MODELS_KEY }),
  })
}

export interface CapabilitySnapshot {
  instanceId: string
  providerName: string
  tier: 'Unknown' | 'Chat' | 'Inspect' | 'Research'
  supportsLogprobs: boolean
  maxLogprobs: number
  supportsTokenize: boolean
  supportsGuidedDecoding: boolean
  supportsStreaming: boolean
  supportsFunctionCalling: boolean
  supportsMetrics: boolean
  supportsModelSwap: boolean
  supportsMultimodal: boolean
  probedAt: string
  probeSucceeded: boolean
  probeError: string | null
}

export function useAllCapabilities() {
  return useQuery({
    queryKey: [...MODELS_KEY, 'capabilities'],
    queryFn: () => apiClient<CapabilitySnapshot[]>('/models/instances/capabilities'),
  })
}

export function useProbeCapabilities(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () =>
      apiClient<CapabilitySnapshot>(`/models/instances/${id}/probe`, { method: 'POST' }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: MODELS_KEY })
      queryClient.invalidateQueries({ queryKey: [...MODELS_KEY, 'capabilities'] })
    },
  })
}
