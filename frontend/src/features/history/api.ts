import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  HistoryRecord,
  HistoryRecordDetail,
  HistoryFilterParams,
  ReplayResult,
  PagedResult,
  TraceResponse,
} from './types'

const HISTORY_KEY = ['history']

/**
 * Builds the filter query string shared by search and export. One builder, so an export
 * always requests exactly what the list shows — the two cannot drift.
 */
export function buildHistoryFilterParams(params?: HistoryFilterParams): URLSearchParams {
  const searchParams = new URLSearchParams()
  if (params?.search) searchParams.set('search', params.search)
  if (params?.sourceModule) searchParams.set('sourceModule', params.sourceModule)
  if (params?.model) searchParams.set('model', params.model)
  if (params?.from) searchParams.set('from', params.from)
  if (params?.to) searchParams.set('to', params.to)
  if (params?.tags) searchParams.set('tags', params.tags)
  if (params?.isSuccess !== undefined) searchParams.set('isSuccess', String(params.isSuccess))
  return searchParams
}

/** Fetch paginated + filtered history records. */
export function useHistoryRecords(params?: HistoryFilterParams) {
  const searchParams = buildHistoryFilterParams(params)
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

/** Fetch the per-token trace of a record (logprobs, entropy, surprise, alternatives). */
export function useHistoryTrace(id: string | null, enabled: boolean) {
  return useQuery({
    queryKey: [...HISTORY_KEY, id, 'trace'],
    queryFn: () => apiClient<TraceResponse>(`/history/${id}/trace`),
    enabled: !!id && enabled,
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

/** The formats the history export endpoint supports. */
export type HistoryExportFormat = 'jsonl' | 'csv' | 'parquet'

/**
 * Downloads a filtered history export. Bypasses `apiClient` deliberately: the body is a file,
 * not JSON, and the filename comes from the Content-Disposition header. Throws with the
 * server's problem detail when the request fails, so the caller can say what failed.
 */
export function useExportHistory() {
  return useMutation({
    mutationFn: async ({
      filters,
      format,
    }: {
      filters: HistoryFilterParams
      format: HistoryExportFormat
    }) => {
      const params = buildHistoryFilterParams(filters)
      params.set('format', format)

      const response = await fetch(`/api/v1/history/export?${params.toString()}`)

      if (!response.ok) {
        let detail = `Export failed (HTTP ${response.status})`
        try {
          const problem = (await response.json()) as { detail?: string; title?: string }
          detail = problem.detail ?? problem.title ?? detail
        } catch {
          // Body was not problem JSON; keep the status-based message.
        }
        throw new Error(detail)
      }

      const rowCount = response.headers.get('X-Export-Row-Count')
      const contentDisposition = response.headers.get('Content-Disposition')
      const fileName =
        contentDisposition?.match(/filename="?([^";]+)"?/)?.[1] ??
        `history-export.${format}`

      const blob = await response.blob()
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(url)

      return { fileName, rowCount: rowCount ? Number(rowCount) : null }
    },
  })
}

export interface ReplayRequest {
  id: string
  instanceId: string
  overrideModel?: string
  overrideTemperature?: number
  overrideMaxTokens?: number
  overrideTopP?: number
}

/**
 * Replay a history record against a given instance with optional parameter overrides.
 *
 * A replay is itself an inference call and is recorded like any other, so the list is
 * invalidated on success — otherwise the run you just made is missing from the table behind
 * the dialog. Records are persisted on a background channel rather than in the request, so an
 * immediate refetch can arrive before the row exists; the second pass a moment later is what
 * makes the replay appear without the reader having to reload the page to find their own run.
 */
const RECORD_PERSISTENCE_GRACE_MS = 2000

export function useReplayRecord() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: ReplayRequest) =>
      apiClient<ReplayResult>(`/history/${id}/replay`, {
        method: 'POST',
        body,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: HISTORY_KEY })
      setTimeout(
        () => queryClient.invalidateQueries({ queryKey: HISTORY_KEY }),
        RECORD_PERSISTENCE_GRACE_MS
      )
    },
  })
}
