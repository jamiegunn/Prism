import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { NotebookSummary, NotebookDetail } from './types'

const NOTEBOOKS_KEY = ['notebooks']

export function useNotebooks(search?: string) {
  const params = new URLSearchParams()
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...NOTEBOOKS_KEY, { search }],
    queryFn: () => apiClient<NotebookSummary[]>(`/notebooks${query ? `?${query}` : ''}`),
  })
}

export function useNotebook(id: string) {
  return useQuery({
    queryKey: [...NOTEBOOKS_KEY, id],
    queryFn: () => apiClient<NotebookDetail>(`/notebooks/${id}`),
    enabled: !!id,
  })
}

export function useCreateNotebook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { name: string; description?: string; content?: string }) =>
      apiClient<NotebookSummary>('/notebooks', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: NOTEBOOKS_KEY }),
  })
}

export function useUpdateNotebook(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { name?: string; description?: string; content?: string }) =>
      apiClient<NotebookDetail>(`/notebooks/${id}`, { method: 'PUT', body: data }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: NOTEBOOKS_KEY })
      queryClient.invalidateQueries({ queryKey: [...NOTEBOOKS_KEY, id] })
    },
  })
}

export function useDeleteNotebook() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/notebooks/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: NOTEBOOKS_KEY }),
  })
}
