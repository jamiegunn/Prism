import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { AgentWorkflow, AgentRun, AgentTool } from './types'

const AGENTS_KEY = ['agents']

export function useWorkflows(search?: string) {
  const params = new URLSearchParams()
  if (search) params.set('search', search)
  const query = params.toString()

  return useQuery({
    queryKey: [...AGENTS_KEY, 'workflows', { search }],
    queryFn: () => apiClient<AgentWorkflow[]>(`/agents${query ? `?${query}` : ''}`),
  })
}

export function useWorkflow(id: string) {
  return useQuery({
    queryKey: [...AGENTS_KEY, 'workflows', id],
    queryFn: () => apiClient<AgentWorkflow>(`/agents/${id}`),
    enabled: !!id,
  })
}

export function useCreateWorkflow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      name: string
      description?: string
      systemPrompt: string
      model: string
      instanceId: string
      pattern: string
      maxSteps: number
      tokenBudget: number
      temperature: number
      enabledTools: string[]
    }) => apiClient<AgentWorkflow>('/agents', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...AGENTS_KEY, 'workflows'] }),
  })
}

export function useUpdateWorkflow(id: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: {
      name: string
      description?: string
      systemPrompt: string
      model: string
      instanceId: string
      pattern: string
      maxSteps: number
      tokenBudget: number
      temperature: number
      enabledTools: string[]
    }) => apiClient<AgentWorkflow>(`/agents/${id}`, { method: 'PUT', body: data }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...AGENTS_KEY, 'workflows'] })
      queryClient.invalidateQueries({ queryKey: [...AGENTS_KEY, 'workflows', id] })
    },
  })
}

export function useDeleteWorkflow() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => apiClient<void>(`/agents/${id}`, { method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [...AGENTS_KEY, 'workflows'] }),
  })
}

export function useRuns(workflowId: string) {
  return useQuery({
    queryKey: [...AGENTS_KEY, 'runs', workflowId],
    queryFn: () => apiClient<AgentRun[]>(`/agents/${workflowId}/runs`),
    enabled: !!workflowId,
  })
}

export function useRun(runId: string) {
  return useQuery({
    queryKey: [...AGENTS_KEY, 'run', runId],
    queryFn: () => apiClient<AgentRun>(`/agents/runs/${runId}`),
    enabled: !!runId,
  })
}

export function useTools() {
  return useQuery({
    queryKey: [...AGENTS_KEY, 'tools'],
    queryFn: () => apiClient<AgentTool[]>('/agents/tools'),
  })
}

export function useRunAgent(workflowId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: string) => {
      const response = await fetch(`/api/v1/agents/${workflowId}/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ input }),
      })

      if (!response.ok) {
        const error = await response.json().catch(() => ({ message: response.statusText }))
        throw new Error((error as Record<string, string>).detail ?? 'Run failed')
      }

      // Parse SSE response
      const reader = response.body?.getReader()
      if (!reader) throw new Error('No response body')

      const decoder = new TextDecoder()
      let buffer = ''
      let lastRun: AgentRun | null = null

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6)
            try {
              const parsed = JSON.parse(data) as { run?: AgentRun }
              if (parsed.run) {
                lastRun = parsed.run
              }
            } catch {
              // Skip unparseable lines
            }
          }
        }
      }

      return lastRun
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [...AGENTS_KEY, 'runs', workflowId] })
    },
  })
}
