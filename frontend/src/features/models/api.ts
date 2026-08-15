import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { InferenceInstance, InstanceMetrics, RegisterInstanceRequest } from './types'

export const MODELS_KEY = ['models', 'instances']

/**
 * How often instance health is re-read, matching the background health check's own cadence.
 *
 * Without it the app kept whatever status it fetched when the page loaded, and every screen that
 * reads from it aged into a lie: the banner announced that none of your models were responding
 * while an agent was answering through one of them, and the Playground's footer went on saying
 * "Connected" to a server that had stopped. Both directions mislead, and neither corrects itself
 * until a reload.
 */
const HEALTH_REFRESH_MS = 30_000

export function useInstances() {
  return useQuery({
    queryKey: MODELS_KEY,
    queryFn: () => apiClient<InferenceInstance[]>('/models/instances'),
    refetchInterval: HEALTH_REFRESH_MS,
  })
}

export function useInstance(id: string) {
  return useQuery({
    queryKey: [...MODELS_KEY, id],
    queryFn: () => apiClient<InferenceInstance>(`/models/instances/${id}`),
    enabled: !!id,
  })
}

/** What an instance reports it can serve, or the stated reason it cannot say. */
export interface InstanceModels {
  models: string[]
  /** False for a server that serves only what it was started with, such as vLLM. */
  canList: boolean
  /** Why the list is empty or unavailable; null when it is neither. */
  reason: string | null
  /**
   * The subset of `models` that can only produce embeddings.
   *
   * An instance is asked to hold conversations, so choosing one of these leaves it unable to
   * answer anything. Empty when the server does not say what its models are for, in which case
   * every model is offered as usual — silence is not evidence.
   */
  embeddingOnly: string[]
}

/**
 * The models an instance can currently serve.
 *
 * Asked by anything that has to name a model on someone's behalf — replay, most of all, where
 * the alternative was typing an exact identifier from memory after a "model not found".
 */
export function useInstanceModels(id: string | null) {
  return useQuery({
    queryKey: [...MODELS_KEY, id, 'models'],
    queryFn: () => apiClient<InstanceModels>(`/models/instances/${id}/models`),
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

/**
 * Makes an instance the default one.
 *
 * The default could only be chosen when registering — there was no endpoint to change it and
 * nothing in the UI that asked. It decides which server embeddings, batch inference and the
 * evaluation runner use, so being unable to move it meant deleting an instance and adding it
 * again to change your mind.
 */
export function useSetDefaultInstance(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () =>
      apiClient<InferenceInstance>(`/models/instances/${id}/default`, { method: 'POST' }),
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
