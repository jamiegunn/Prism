import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  PromptTemplate,
  PromptTemplateWithVersion,
  PromptVersion,
  TestPromptResult,
  AbTestResult,
  VersionDiff,
  AbTestVariation,
  AbTestParameterSet,
  PromptVariable,
  FewShotExample,
} from './types'

const TEMPLATES_KEY = ['prompt-templates']

// ─── Templates ───────────────────────────────────────────────────────

export function useTemplates(category?: string, search?: string) {
  const params = new URLSearchParams()
  if (category) params.set('category', category)
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...TEMPLATES_KEY, { category, search }],
    queryFn: () =>
      apiClient<PromptTemplate[]>(`/prompts${query ? `?${query}` : ''}`),
  })
}

export function useTemplate(id: string | null) {
  return useQuery({
    queryKey: [...TEMPLATES_KEY, id],
    queryFn: () =>
      apiClient<PromptTemplateWithVersion>(`/prompts/${id}`),
    enabled: !!id,
  })
}

export function useCreateTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      name: string
      userTemplate: string
      projectId?: string
      category?: string
      description?: string
      tags?: string[]
      systemPrompt?: string
      variables?: PromptVariable[]
      fewShotExamples?: FewShotExample[]
    }) => apiClient<PromptTemplateWithVersion>('/prompts', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TEMPLATES_KEY }),
  })
}

export function useUpdateTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      id,
      ...data
    }: {
      id: string
      name: string
      category?: string
      description?: string
      tags?: string[]
      projectId?: string
    }) => apiClient<PromptTemplate>(`/prompts/${id}`, { method: 'PUT', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TEMPLATES_KEY }),
  })
}

export function useDeleteTemplate() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) =>
      apiClient<void>(`/prompts/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: TEMPLATES_KEY }),
  })
}

// ─── Versions ────────────────────────────────────────────────────────

function versionsKey(templateId: string) {
  return [...TEMPLATES_KEY, templateId, 'versions']
}

export function useVersions(templateId: string) {
  return useQuery({
    queryKey: versionsKey(templateId),
    queryFn: () =>
      apiClient<PromptVersion[]>(`/prompts/${templateId}/versions`),
    enabled: !!templateId,
  })
}

export function useCreateVersion() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      templateId,
      ...data
    }: {
      templateId: string
      userTemplate: string
      systemPrompt?: string
      variables?: PromptVariable[]
      fewShotExamples?: FewShotExample[]
      notes?: string
    }) =>
      apiClient<PromptVersion>(`/prompts/${templateId}/versions`, {
        method: 'POST',
        body: data,
      }),
    onSuccess: (_, vars) => {
      queryClient.invalidateQueries({ queryKey: versionsKey(vars.templateId) })
      queryClient.invalidateQueries({ queryKey: TEMPLATES_KEY })
    },
  })
}

export function useVersionDiff(templateId: string, v1: number, v2: number) {
  return useQuery({
    queryKey: [...versionsKey(templateId), 'diff', v1, v2],
    queryFn: () =>
      apiClient<VersionDiff>(
        `/prompts/${templateId}/versions/diff?v1=${v1}&v2=${v2}`
      ),
    enabled: !!templateId && v1 > 0 && v2 > 0 && v1 !== v2,
  })
}

// ─── Test & A/B Test ─────────────────────────────────────────────────

export function useTestPrompt() {
  return useMutation({
    mutationFn: ({
      templateId,
      ...data
    }: {
      templateId: string
      variables: Record<string, string>
      instanceId: string
      version?: number
      temperature?: number
      topP?: number
      topK?: number
      maxTokens?: number
      logprobs?: boolean
      topLogprobs?: number
      saveAsRunExperimentId?: string
      runName?: string
    }) =>
      apiClient<TestPromptResult>(`/prompts/${templateId}/test`, {
        method: 'POST',
        body: data,
      }),
  })
}

export function useAbTest() {
  return useMutation({
    mutationFn: (data: {
      projectId: string
      experimentName: string
      variations: AbTestVariation[]
      instanceIds: string[]
      parameterSets: AbTestParameterSet[]
      runsPerCombo?: number
    }) =>
      apiClient<AbTestResult>('/prompts/ab-test', {
        method: 'POST',
        body: data,
      }),
  })
}
