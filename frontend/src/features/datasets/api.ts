import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  Dataset,
  DatasetRecord,
  DatasetStats,
  PagedResult,
  SplitDatasetParams,
  ColumnSchema,
} from './types'

const DATASETS_KEY = ['datasets']

// ─── Datasets CRUD ──────────────────────────────────────────────────

export function useDatasets(projectId?: string, search?: string) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...DATASETS_KEY, { projectId, search }],
    queryFn: () => apiClient<Dataset[]>(`/datasets${query ? `?${query}` : ''}`),
  })
}

export function useDataset(id: string | null) {
  return useQuery({
    queryKey: [...DATASETS_KEY, id],
    queryFn: () => apiClient<Dataset>(`/datasets/${id}`),
    enabled: !!id,
  })
}

export function useUploadDataset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ file, name, description }: { file: File; name: string; description?: string }) => {
      const formData = new FormData()
      formData.append('file', file)
      const params = new URLSearchParams({ name })
      if (description) params.set('description', description)

      const response = await fetch(`/api/v1/datasets/?${params.toString()}`, {
        method: 'POST',
        body: formData,
      })

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: response.statusText })) as Record<string, unknown>
        throw new Error((error.title as string) ?? (error.detail as string) ?? 'Upload failed')
      }

      return response.json() as Promise<Dataset>
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DATASETS_KEY }),
  })
}

export function useUpdateDataset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; name?: string; description?: string | null; schema?: ColumnSchema[] }) =>
      apiClient<Dataset>(`/datasets/${id}`, { method: 'PUT', body }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: DATASETS_KEY })
      queryClient.invalidateQueries({ queryKey: [...DATASETS_KEY, variables.id] })
    },
  })
}

export function useDeleteDataset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/datasets/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: DATASETS_KEY }),
  })
}

// ─── Records ────────────────────────────────────────────────────────

export function useDatasetRecords(datasetId: string | null, splitLabel?: string, page = 1, pageSize = 50) {
  const params = new URLSearchParams()
  if (splitLabel) params.set('splitLabel', splitLabel)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))

  return useQuery({
    queryKey: [...DATASETS_KEY, datasetId, 'records', { splitLabel, page, pageSize }],
    queryFn: () => apiClient<PagedResult<DatasetRecord>>(`/datasets/${datasetId}/records?${params.toString()}`),
    enabled: !!datasetId,
  })
}

export function useUpdateRecord() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ datasetId, recordId, data }: { datasetId: string; recordId: string; data: Record<string, unknown> }) =>
      apiClient<DatasetRecord>(`/datasets/${datasetId}/records/${recordId}`, { method: 'PUT', body: { data } }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: [...DATASETS_KEY, variables.datasetId, 'records'] })
    },
  })
}

// ─── Stats & Split & Export ─────────────────────────────────────────

export function useDatasetStats(datasetId: string | null) {
  return useQuery({
    queryKey: [...DATASETS_KEY, datasetId, 'stats'],
    queryFn: () => apiClient<DatasetStats>(`/datasets/${datasetId}/stats`),
    enabled: !!datasetId,
  })
}

export function useSplitDataset() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...params }: SplitDatasetParams & { id: string }) =>
      apiClient<Dataset>(`/datasets/${id}/split`, { method: 'POST', body: params }),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: [...DATASETS_KEY, variables.id] })
      queryClient.invalidateQueries({ queryKey: [...DATASETS_KEY, variables.id, 'stats'] })
      queryClient.invalidateQueries({ queryKey: [...DATASETS_KEY, variables.id, 'records'] })
    },
  })
}

export interface ValidationReport {
  datasetId: string
  totalRecords: number
  issues: { column: string; severity: string; message: string }[]
  isValid: boolean
}

export function useValidateDataset(datasetId: string | null) {
  return useQuery({
    queryKey: ['datasets', datasetId, 'validate'],
    queryFn: () => apiClient<ValidationReport>(`/datasets/${datasetId}/validate`),
    enabled: !!datasetId,
  })
}

export function useExportDataset() {
  return useMutation({
    mutationFn: async ({ id, format, splitLabel }: { id: string; format: string; splitLabel?: string }) => {
      const params = new URLSearchParams({ format })
      if (splitLabel) params.set('splitLabel', splitLabel)

      const response = await fetch(`/api/v1/datasets/${id}/export?${params.toString()}`, {
        method: 'POST',
      })

      if (!response.ok) throw new Error('Export failed')

      const blob = await response.blob()
      const contentDisposition = response.headers.get('content-disposition')
      const fileName = contentDisposition?.match(/filename="?(.+)"?/)?.[1] ?? `dataset.${format}`

      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      a.click()
      URL.revokeObjectURL(url)
    },
  })
}
