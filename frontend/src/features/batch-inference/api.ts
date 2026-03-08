import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { BatchJob, BatchResult, BatchEstimate, PagedResult } from './types'

const BATCH_KEY = ['batch']

export function useBatchJobs(status?: string) {
  const params = new URLSearchParams()
  if (status) params.set('status', status)
  const query = params.toString()

  return useQuery({
    queryKey: [...BATCH_KEY, { status }],
    queryFn: () => apiClient<BatchJob[]>(`/batch${query ? `?${query}` : ''}`),
  })
}

export function useBatchJob(id: string | null) {
  return useQuery({
    queryKey: [...BATCH_KEY, id],
    queryFn: () => apiClient<BatchJob>(`/batch/${id}`),
    enabled: !!id,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'Running' || status === 'Queued' ? 3000 : false
    },
  })
}

export function useCreateBatchJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      datasetId: string
      splitLabel?: string
      model: string
      promptVersionId?: string
      parameters?: Record<string, unknown>
      concurrency: number
      maxRetries: number
      captureLogprobs: boolean
    }) => apiClient<BatchJob>('/batch', { method: 'POST', body }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BATCH_KEY }),
  })
}

export function usePauseBatchJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiClient<BatchJob>(`/batch/${id}/pause`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BATCH_KEY }),
  })
}

export function useResumeBatchJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiClient<BatchJob>(`/batch/${id}/resume`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BATCH_KEY }),
  })
}

export function useCancelBatchJob() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiClient<BatchJob>(`/batch/${id}/cancel`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BATCH_KEY }),
  })
}

export function useRetryFailedBatch() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiClient<BatchJob>(`/batch/${id}/retry-failed`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: BATCH_KEY }),
  })
}

export function useBatchResults(jobId: string | null, status?: string, page = 1, pageSize = 50) {
  const params = new URLSearchParams()
  if (status) params.set('status', status)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))

  return useQuery({
    queryKey: [...BATCH_KEY, jobId, 'results', { status, page, pageSize }],
    queryFn: () => apiClient<PagedResult<BatchResult>>(`/batch/${jobId}/results?${params.toString()}`),
    enabled: !!jobId,
  })
}

export function useEstimateBatchCost() {
  return useMutation({
    mutationFn: (body: { datasetId: string; splitLabel?: string; model: string; concurrency: number }) =>
      apiClient<BatchEstimate>('/batch/estimate', { method: 'POST', body }),
  })
}
