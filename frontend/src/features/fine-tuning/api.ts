import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { LoraAdapter, ExportFineTuneResult } from './types'

const FT_KEY = ['fine-tuning']

export function useAdapters(instanceId?: string) {
  const params = new URLSearchParams()
  if (instanceId) params.set('instanceId', instanceId)
  const query = params.toString()

  return useQuery({
    queryKey: [...FT_KEY, 'adapters', { instanceId }],
    queryFn: () => apiClient<LoraAdapter[]>(`/fine-tuning/adapters${query ? `?${query}` : ''}`),
  })
}

export function useCreateAdapter() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      name: string
      description?: string
      instanceId: string
      adapterPath: string
      baseModel: string
    }) => apiClient<LoraAdapter>('/fine-tuning/adapters', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...FT_KEY, 'adapters'] }),
  })
}

export function useDeleteAdapter() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/fine-tuning/adapters/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...FT_KEY, 'adapters'] }),
  })
}

export function useExportFineTune() {
  return useMutation({
    mutationFn: (data: {
      datasetId: string
      format: string
      instructionColumn?: string
      inputColumn?: string
      outputColumn?: string
    }) => apiClient<ExportFineTuneResult>('/fine-tuning/export', { method: 'POST', body: data }),
  })
}
