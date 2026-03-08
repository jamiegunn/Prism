import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type { UsageSummary, PerformanceSummary } from './types'

const ANALYTICS_KEY = ['analytics']

export function useUsage(from?: string, to?: string, model?: string, sourceModule?: string) {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (model) params.set('model', model)
  if (sourceModule) params.set('sourceModule', sourceModule)
  const query = params.toString()

  return useQuery({
    queryKey: [...ANALYTICS_KEY, 'usage', { from, to, model, sourceModule }],
    queryFn: () => apiClient<UsageSummary>(`/analytics/usage${query ? `?${query}` : ''}`),
  })
}

export function usePerformance(from?: string, to?: string, model?: string) {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (model) params.set('model', model)
  const query = params.toString()

  return useQuery({
    queryKey: [...ANALYTICS_KEY, 'performance', { from, to, model }],
    queryFn: () => apiClient<PerformanceSummary>(`/analytics/performance${query ? `?${query}` : ''}`),
  })
}
