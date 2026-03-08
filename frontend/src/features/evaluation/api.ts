import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  Evaluation,
  EvaluationSummary,
  EvaluationResult,
  LeaderboardEntry,
  PagedResult,
} from './types'

const EVALUATIONS_KEY = ['evaluations']

export function useEvaluations(projectId?: string, search?: string) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...EVALUATIONS_KEY, { projectId, search }],
    queryFn: () => apiClient<Evaluation[]>(`/evaluation${query ? `?${query}` : ''}`),
  })
}

export function useEvaluation(id: string | null) {
  return useQuery({
    queryKey: [...EVALUATIONS_KEY, id],
    queryFn: () => apiClient<Evaluation>(`/evaluation/${id}`),
    enabled: !!id,
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'Running' || status === 'Pending' ? 3000 : false
    },
  })
}

export function useStartEvaluation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: {
      name: string
      datasetId: string
      splitLabel?: string
      projectId?: string
      models: string[]
      promptVersionId?: string
      scoringMethods: string[]
      config?: Record<string, unknown>
    }) => apiClient<Evaluation>('/evaluation', { method: 'POST', body }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: EVALUATIONS_KEY }),
  })
}

export function useCancelEvaluation() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<Evaluation>(`/evaluation/${id}/cancel`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: EVALUATIONS_KEY }),
  })
}

export function useEvaluationResults(id: string | null) {
  return useQuery({
    queryKey: [...EVALUATIONS_KEY, id, 'results'],
    queryFn: () => apiClient<EvaluationSummary>(`/evaluation/${id}/results`),
    enabled: !!id,
  })
}

export function useEvaluationResultRecords(id: string | null, model?: string, page = 1, pageSize = 50) {
  const params = new URLSearchParams()
  if (model) params.set('model', model)
  params.set('page', String(page))
  params.set('pageSize', String(pageSize))

  return useQuery({
    queryKey: [...EVALUATIONS_KEY, id, 'records', { model, page, pageSize }],
    queryFn: () => apiClient<PagedResult<EvaluationResult>>(`/evaluation/${id}/results/records?${params.toString()}`),
    enabled: !!id,
  })
}

export function useLeaderboard(projectId?: string, scoringMethod?: string) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (scoringMethod) params.set('scoringMethod', scoringMethod)
  const query = params.toString()

  return useQuery({
    queryKey: [...EVALUATIONS_KEY, 'leaderboard', { projectId, scoringMethod }],
    queryFn: () => apiClient<LeaderboardEntry[]>(`/evaluation/leaderboard${query ? `?${query}` : ''}`),
  })
}
