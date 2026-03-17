import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface WorkspaceStore {
  activeWorkspaceId: string | null
  activeProjectId: string | null
  setActiveWorkspace: (id: string | null) => void
  setActiveProject: (id: string | null) => void
}

export const useWorkspaceStore = create<WorkspaceStore>()(
  persist(
    (set) => ({
      activeWorkspaceId: null,
      activeProjectId: null,
      setActiveWorkspace: (id) => set({ activeWorkspaceId: id, activeProjectId: null }),
      setActiveProject: (id) => set({ activeProjectId: id }),
    }),
    { name: 'prism-workspace' }
  )
)
