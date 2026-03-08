import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  RagCollection,
  RagDocument,
  ChunkSearchResult,
  RagPipelineResult,
  CollectionStats,
} from './types'

const RAG_KEY = ['rag']

export function useCollections(projectId?: string, search?: string) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...RAG_KEY, 'collections', { projectId, search }],
    queryFn: () => apiClient<RagCollection[]>(`/rag/collections${query ? `?${query}` : ''}`),
  })
}

export function useCollection(id: string) {
  return useQuery({
    queryKey: [...RAG_KEY, 'collections', id],
    queryFn: () => apiClient<RagCollection>(`/rag/collections/${id}`),
    enabled: !!id,
  })
}

export function useCreateCollection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      name: string
      description?: string
      embeddingModel: string
      dimensions: number
      distanceMetric?: string
      chunkingStrategy?: string
      chunkSize?: number
      chunkOverlap?: number
    }) =>
      apiClient<RagCollection>('/rag/collections', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...RAG_KEY, 'collections'] }),
  })
}

export function useDeleteCollection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/rag/collections/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...RAG_KEY, 'collections'] }),
  })
}

export function useDocuments(collectionId: string) {
  return useQuery({
    queryKey: [...RAG_KEY, 'documents', collectionId],
    queryFn: () => apiClient<RagDocument[]>(`/rag/collections/${collectionId}/documents`),
    enabled: !!collectionId,
  })
}

export function useIngestDocument(collectionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData()
      formData.append('file', file)
      const response = await fetch(`/api/v1/rag/collections/${collectionId}/ingest`, {
        method: 'POST',
        body: formData,
      })
      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: response.statusText }))
        throw new Error((error as Record<string, string>).detail ?? 'Upload failed')
      }
      return response.json() as Promise<RagDocument>
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...RAG_KEY, 'documents', collectionId] })
      queryClient.invalidateQueries({ queryKey: [...RAG_KEY, 'collections'] })
    },
  })
}

export function useQueryCollection(collectionId: string) {
  return useMutation({
    mutationFn: (data: {
      queryText: string
      topK?: number
      searchType?: string
      vectorWeight?: number
    }) =>
      apiClient<ChunkSearchResult[]>(`/rag/collections/${collectionId}/query`, {
        method: 'POST',
        body: data,
      }),
  })
}

export function useRagPipeline(collectionId: string) {
  return useMutation({
    mutationFn: (data: {
      query: string
      model: string
      instanceId: string
      systemPrompt?: string
      promptTemplate?: string
      topK?: number
      searchType?: string
      temperature?: number
      maxTokens?: number
    }) =>
      apiClient<RagPipelineResult>(`/rag/collections/${collectionId}/rag`, {
        method: 'POST',
        body: data,
      }),
  })
}

export function useCollectionStats(collectionId: string) {
  return useQuery({
    queryKey: [...RAG_KEY, 'stats', collectionId],
    queryFn: () => apiClient<CollectionStats>(`/rag/collections/${collectionId}/stats`),
    enabled: !!collectionId,
  })
}
