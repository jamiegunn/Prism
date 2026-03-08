import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  HistoryRecord,
  HistoryRecordDetail,
  HistoryFilterParams,
  ReplayResult,
  PagedResult,
} from './types'

const HISTORY_KEY = ['history']

/** Fetch paginated + filtered history records. */
export function useHistoryRecords(params?: HistoryFilterParams) {
  const searchParams = new URLSearchParams()
  if (params?.search) searchParams.set('search', params.search)
  if (params?.sourceModule) searchParams.set('sourceModule', params.sourceModule)
  if (params?.model) searchParams.set('model', params.model)
  if (params?.from) searchParams.set('from', params.from)
  if (params?.to) searchParams.set('to', params.to)
  if (params?.tags) searchParams.set('tags', params.tags)
  if (params?.isSuccess !== undefined) searchParams.set('isSuccess', String(params.isSuccess))
  if (params?.page) searchParams.set('page', String(params.page))
  if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize))
  const query = searchParams.toString()

  return useQuery({
    queryKey: [...HISTORY_KEY, params],
    queryFn: () =>
      apiClient<PagedResult<HistoryRecord>>(
        `/history${query ? `?${query}` : ''}`
      ),
  })
}

/** Fetch a single history record with full detail. */
export function useHistoryRecord(id: string | null) {
  return useQuery({
    queryKey: [...HISTORY_KEY, id],
    queryFn: () => apiClient<HistoryRecordDetail>(`/history/${id}`),
    enabled: !!id,
  })
}

/** Update tags on a history record. */
export function useTagRecord() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, tags }: { id: string; tags: string[] }) =>
      apiClient<void>(`/history/${id}/tags`, {
        method: 'PUT',
        body: { tags },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: HISTORY_KEY }),
  })
}

/** Replay a history record against a given instance. */
export function useReplayRecord() {
  return useMutation({
    mutationFn: ({ id, instanceId }: { id: string; instanceId: string }) =>
      apiClient<ReplayResult>(`/history/${id}/replay`, {
        method: 'POST',
        body: { instanceId },
      }),
  })
}
