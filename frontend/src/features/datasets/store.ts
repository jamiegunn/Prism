import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface DatasetsState {
  selectedDatasetId: string | null
  recordsPage: number
  recordsPageSize: number
  splitFilter: string | null
  searchQuery: string

  setSelectedDatasetId: (id: string | null) => void
  setRecordsPage: (page: number) => void
  setRecordsPageSize: (size: number) => void
  setSplitFilter: (split: string | null) => void
  setSearchQuery: (query: string) => void
}

export const useDatasetsStore = create<DatasetsState>()(
  persist(
    (set) => ({
      selectedDatasetId: null,
      recordsPage: 1,
      recordsPageSize: 50,
      splitFilter: null,
      searchQuery: '',

      setSelectedDatasetId: (id) => set({ selectedDatasetId: id, recordsPage: 1, splitFilter: null }),
      setRecordsPage: (page) => set({ recordsPage: page }),
      setRecordsPageSize: (size) => set({ recordsPageSize: size, recordsPage: 1 }),
      setSplitFilter: (split) => set({ splitFilter: split, recordsPage: 1 }),
      setSearchQuery: (query) => set({ searchQuery: query }),
    }),
    {
      name: 'prism-datasets-state',
      partialize: (state) => ({
        recordsPageSize: state.recordsPageSize,
      }),
    }
  )
)
