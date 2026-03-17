import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'

export interface Workspace {
  id: string
  name: string
  description: string | null
  isDefault: boolean
  iconColor: string | null
  createdAt: string
  updatedAt: string
}

const WORKSPACES_KEY = ['workspaces']

export function useWorkspaces() {
  return useQuery({
    queryKey: WORKSPACES_KEY,
    queryFn: () => apiClient<Workspace[]>('/workspaces'),
  })
}

export function useCreateWorkspace() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (data: { name: string; description?: string; iconColor?: string }) =>
      apiClient<Workspace>('/workspaces', { method: 'POST', body: data }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: WORKSPACES_KEY }),
  })
}
