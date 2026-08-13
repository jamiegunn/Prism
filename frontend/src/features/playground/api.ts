import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { Conversation, ConversationSummary, PagedResult } from './types'

const PLAYGROUND_KEY = ['playground', 'conversations']

interface ConversationListParams {
  page?: number
  pageSize?: number
  search?: string
}

/**
 * Re-exported rather than re-declared. This was a second copy of the same query against the same
 * key, which meant a change to how instance health is kept fresh reached some screens and not
 * others depending on which import they happened to use.
 */
export { useInstances } from '@/features/models/api'

export function useConversations(params?: ConversationListParams) {
  const searchParams = new URLSearchParams()
  if (params?.page) searchParams.set('page', String(params.page))
  if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize))
  if (params?.search) searchParams.set('search', params.search)
  const query = searchParams.toString()

  return useQuery({
    queryKey: [...PLAYGROUND_KEY, params],
    queryFn: () =>
      apiClient<PagedResult<ConversationSummary>>(
        `/playground/conversations${query ? `?${query}` : ''}`
      ),
  })
}

export function useConversation(id: string | null) {
  return useQuery({
    queryKey: [...PLAYGROUND_KEY, id],
    queryFn: () =>
      apiClient<Conversation>(
        `/playground/conversations/${id}?includeLogprobs=true`
      ),
    enabled: !!id,
  })
}

export function useDeleteConversation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/playground/conversations/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PLAYGROUND_KEY }),
  })
}

export function useExportConversation() {
  return useMutation({
    mutationFn: async ({ id, format }: { id: string; format: 'json' | 'markdown' | 'jsonl' }) => {
      const response = await fetch(
        `/api/v1/playground/conversations/${id}/export?format=${format}`
      )
      if (!response.ok) {
        throw new Error('Export failed')
      }
      const blob = await response.blob()
      return blob
    },
  })
}

export function invalidateConversations(queryClient: ReturnType<typeof useQueryClient>) {
  return queryClient.invalidateQueries({ queryKey: PLAYGROUND_KEY })
}
