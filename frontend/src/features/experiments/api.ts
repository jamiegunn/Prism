import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  Project,
  Experiment,
  ExperimentStatus,
  Run,
  RunComparison,
  ListRunsParams,
  PagedResult,
} from './types'

const PROJECTS_KEY = ['projects']
const EXPERIMENTS_KEY = ['experiments']

// ─── Projects ────────────────────────────────────────────────────────

export function useProjects(includeArchived = false, search?: string) {
  const params = new URLSearchParams()
  if (includeArchived) params.set('includeArchived', 'true')
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...PROJECTS_KEY, { includeArchived, search }],
    queryFn: () => apiClient<Project[]>(`/projects${query ? `?${query}` : ''}`),
  })
}

export function useProject(id: string | null) {
  return useQuery({
    queryKey: [...PROJECTS_KEY, id],
    queryFn: () => apiClient<Project>(`/projects/${id}`),
    enabled: !!id,
  })
}

export function useCreateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { name: string; description?: string }) =>
      apiClient<Project>('/projects', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PROJECTS_KEY }),
  })
}

export function useUpdateProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...data }: { id: string; name: string; description?: string }) =>
      apiClient<Project>(`/projects/${id}`, { method: 'PUT', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PROJECTS_KEY }),
  })
}

export function useArchiveProject() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<Project>(`/projects/${id}/archive`, { method: 'POST' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: PROJECTS_KEY }),
  })
}

// ─── Experiments ─────────────────────────────────────────────────────

export function useExperiments(projectId?: string, status?: ExperimentStatus) {
  const params = new URLSearchParams()
  if (projectId) params.set('projectId', projectId)
  if (status) params.set('status', status)
  const query = params.toString()

  return useQuery({
    queryKey: [...EXPERIMENTS_KEY, { projectId, status }],
    queryFn: () =>
      apiClient<Experiment[]>(`/experiments${query ? `?${query}` : ''}`),
  })
}

export function useExperiment(id: string | null) {
  return useQuery({
    queryKey: [...EXPERIMENTS_KEY, id],
    queryFn: () => apiClient<Experiment>(`/experiments/${id}`),
    enabled: !!id,
  })
}

export function useCreateExperiment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      projectId: string
      name: string
      description?: string
      hypothesis?: string
    }) => apiClient<Experiment>('/experiments', { method: 'POST', body: data }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: EXPERIMENTS_KEY }),
  })
}

export function useUpdateExperiment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      ...data
    }: {
      id: string
      name: string
      description?: string
      hypothesis?: string
    }) => apiClient<Experiment>(`/experiments/${id}`, { method: 'PUT', body: data }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: EXPERIMENTS_KEY }),
  })
}

export function useChangeExperimentStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }: { id: string; status: ExperimentStatus }) =>
      apiClient<Experiment>(`/experiments/${id}/status`, {
        method: 'POST',
        body: { status },
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: EXPERIMENTS_KEY }),
  })
}

// ─── Runs ────────────────────────────────────────────────────────────

function runsKey(experimentId: string) {
  return [...EXPERIMENTS_KEY, experimentId, 'runs']
}

export function useRuns(experimentId: string, params?: ListRunsParams) {
  const searchParams = new URLSearchParams()
  if (params?.model) searchParams.set('model', params.model)
  if (params?.status) searchParams.set('status', params.status)
  if (params?.sortBy) searchParams.set('sortBy', params.sortBy)
  if (params?.order) searchParams.set('order', params.order)
  if (params?.page) searchParams.set('page', String(params.page))
  if (params?.pageSize) searchParams.set('pageSize', String(params.pageSize))
  const query = searchParams.toString()

  return useQuery({
    queryKey: [...runsKey(experimentId), params],
    queryFn: () =>
      apiClient<PagedResult<Run>>(
        `/experiments/${experimentId}/runs${query ? `?${query}` : ''}`
      ),
  })
}

export function useRun(experimentId: string, runId: string | null) {
  return useQuery({
    queryKey: [...runsKey(experimentId), runId],
    queryFn: () =>
      apiClient<Run>(`/experiments/${experimentId}/runs/${runId}`),
    enabled: !!runId,
  })
}

export function useDeleteRun(experimentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (runId: string) =>
      apiClient<void>(`/experiments/${experimentId}/runs/${runId}`, {
        method: 'DELETE',
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: runsKey(experimentId) }),
  })
}

export function useCompareRuns(experimentId: string) {
  return useMutation({
    mutationFn: (runIds: string[]) =>
      apiClient<RunComparison>(`/experiments/${experimentId}/compare`, {
        method: 'POST',
        body: { runIds },
      }),
  })
}
