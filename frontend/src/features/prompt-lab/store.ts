import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface PromptLabState {
  // Persisted
  selectedTemplateId: string | null
  selectedCategory: string | null
  testInstanceId: string | null
  testTemperature: number
  testTopP: number
  testMaxTokens: number

  // Transient
  selectedVersionNumber: number | null
  variableValues: Record<string, string>
  isTesting: boolean

  // Actions
  setSelectedTemplateId: (id: string | null) => void
  setSelectedCategory: (category: string | null) => void
  setTestInstanceId: (id: string | null) => void
  setTestTemperature: (value: number) => void
  setTestTopP: (value: number) => void
  setTestMaxTokens: (value: number) => void
  setSelectedVersionNumber: (version: number | null) => void
  setVariableValues: (values: Record<string, string>) => void
  setVariableValue: (name: string, value: string) => void
  setIsTesting: (testing: boolean) => void
  reset: () => void
}

const defaults = {
  selectedTemplateId: null as string | null,
  selectedCategory: null as string | null,
  testInstanceId: null as string | null,
  testTemperature: 0.7,
  testTopP: 0.9,
  testMaxTokens: 2048,
}

export const usePromptLabStore = create<PromptLabState>()(
  persist(
    (set) => ({
      ...defaults,
      selectedVersionNumber: null,
      variableValues: {},
      isTesting: false,

      setSelectedTemplateId: (id) =>
        set({ selectedTemplateId: id, selectedVersionNumber: null, variableValues: {} }),
      setSelectedCategory: (category) => set({ selectedCategory: category }),
      setTestInstanceId: (id) => set({ testInstanceId: id }),
      setTestTemperature: (value) => set({ testTemperature: value }),
      setTestTopP: (value) => set({ testTopP: value }),
      setTestMaxTokens: (value) => set({ testMaxTokens: value }),
      setSelectedVersionNumber: (version) => set({ selectedVersionNumber: version }),
      setVariableValues: (values) => set({ variableValues: values }),
      setVariableValue: (name, value) =>
        set((state) => ({
          variableValues: { ...state.variableValues, [name]: value },
        })),
      setIsTesting: (testing) => set({ isTesting: testing }),
      reset: () =>
        set({ ...defaults, selectedVersionNumber: null, variableValues: {}, isTesting: false }),
    }),
    {
      name: 'prism-prompt-lab-state',
      partialize: (state) => ({
        selectedTemplateId: state.selectedTemplateId,
        selectedCategory: state.selectedCategory,
        testInstanceId: state.testInstanceId,
        testTemperature: state.testTemperature,
        testTopP: state.testTopP,
        testMaxTokens: state.testMaxTokens,
      }),
    }
  )
)
