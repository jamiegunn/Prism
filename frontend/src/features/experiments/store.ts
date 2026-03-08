import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface ExperimentsState {
  // Persisted preferences
  selectedProjectId: string | null
  includeArchived: boolean
  runsSortBy: string
  runsSortOrder: string
  runsPageSize: number

  // Transient state
  selectedRunIds: string[]

  // Actions
  setSelectedProjectId: (id: string | null) => void
  setIncludeArchived: (include: boolean) => void
  setRunsSortBy: (field: string) => void
  setRunsSortOrder: (order: string) => void
  setRunsPageSize: (size: number) => void
  toggleRunSelection: (id: string) => void
  setSelectedRunIds: (ids: string[]) => void
  clearRunSelection: () => void
}

export const useExperimentsStore = create<ExperimentsState>()(
  persist(
    (set) => ({
      selectedProjectId: null,
      includeArchived: false,
      runsSortBy: 'createdAt',
      runsSortOrder: 'desc',
      runsPageSize: 50,
      selectedRunIds: [],

      setSelectedProjectId: (id) => set({ selectedProjectId: id }),
      setIncludeArchived: (include) => set({ includeArchived: include }),
      setRunsSortBy: (field) => set({ runsSortBy: field }),
      setRunsSortOrder: (order) => set({ runsSortOrder: order }),
      setRunsPageSize: (size) => set({ runsPageSize: size }),
      toggleRunSelection: (id) =>
        set((state) => ({
          selectedRunIds: state.selectedRunIds.includes(id)
            ? state.selectedRunIds.filter((r) => r !== id)
            : [...state.selectedRunIds, id],
        })),
      setSelectedRunIds: (ids) => set({ selectedRunIds: ids }),
      clearRunSelection: () => set({ selectedRunIds: [] }),
    }),
    {
      name: 'prism-experiments-state',
      partialize: (state) => ({
        selectedProjectId: state.selectedProjectId,
        includeArchived: state.includeArchived,
        runsSortBy: state.runsSortBy,
        runsSortOrder: state.runsSortOrder,
        runsPageSize: state.runsPageSize,
      }),
    }
  )
)
