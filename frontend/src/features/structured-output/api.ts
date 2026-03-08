import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { JsonSchema, StructuredInferenceResult } from './types'

const SO_KEY = ['structured-output']

export function useSchemas(projectId?: string, search?: string) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...SO_KEY, 'schemas', { projectId, search }],
    queryFn: () =>
      apiClient<JsonSchema[]>(`/structured-output/schemas${query ? `?${query}` : ''}`),
  })
}

export function useSchema(id: string) {
  return useQuery({
    queryKey: [...SO_KEY, 'schemas', id],
    queryFn: () => apiClient<JsonSchema>(`/structured-output/schemas/${id}`),
    enabled: !!id,
  })
}

export function useCreateSchema() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { name: string; description?: string; schemaJson: string }) =>
      apiClient<JsonSchema>('/structured-output/schemas', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...SO_KEY, 'schemas'] }),
  })
}

export function useDeleteSchema() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/structured-output/schemas/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...SO_KEY, 'schemas'] }),
  })
}

export function useStructuredInference(schemaId: string) {
  return useMutation({
    mutationFn: (data: {
      instanceId: string
      model: string
      messages: { role: string; content: string }[]
      temperature?: number
      maxTokens?: number
    }) =>
      apiClient<StructuredInferenceResult>(
        `/structured-output/schemas/${schemaId}/infer`,
        { method: 'POST', body: data }
      ),
  })
}
