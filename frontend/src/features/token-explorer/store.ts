import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type {
  NextTokenPrediction,
  StepEntry,
  BranchExploration,
} from './types'

interface BranchEntry {
  token: string
  exploration: BranchExploration
}

interface TokenExplorerState {
  // Persisted parameters
  instanceId: string | null
  prompt: string
  temperature: number
  topP: number
  topK: number
  topLogprobs: number
  enableThinking: boolean

  // Transient state
  currentPredictions: NextTokenPrediction | null
  stepHistory: StepEntry[]
  branches: BranchEntry[]
  isLoading: boolean

  // Actions
  setInstanceId: (id: string | null) => void
  setPrompt: (prompt: string) => void
  setTemperature: (value: number) => void
  setTopP: (value: number) => void
  setTopK: (value: number) => void
  setTopLogprobs: (value: number) => void
  setEnableThinking: (enabled: boolean) => void
  setPredictions: (predictions: NextTokenPrediction | null) => void
  addStep: (entry: StepEntry) => void
  undoStep: () => void
  clearSteps: () => void
  addBranch: (token: string, exploration: BranchExploration) => void
  clearBranches: () => void
  setLoading: (loading: boolean) => void
  reset: () => void
}

const defaultParameters = {
  instanceId: null as string | null,
  prompt: '',
  temperature: 0,
  topP: 0.9,
  topK: 50,
  topLogprobs: 20,
  enableThinking: false,
}

const defaultTransient = {
  currentPredictions: null as NextTokenPrediction | null,
  stepHistory: [] as StepEntry[],
  branches: [] as BranchEntry[],
  isLoading: false,
}

export const useTokenExplorerStore = create<TokenExplorerState>()(
  persist(
    (set) => ({
      ...defaultParameters,
      ...defaultTransient,

      setInstanceId: (id) => set({ instanceId: id }),
      setPrompt: (prompt) => set({ prompt }),
      setTemperature: (value) => set({ temperature: value }),
      setTopP: (value) => set({ topP: value }),
      setTopK: (value) => set({ topK: value }),
      setTopLogprobs: (value) => set({ topLogprobs: value }),
      setEnableThinking: (enabled) => set({ enableThinking: enabled }),
      setPredictions: (predictions) => set({ currentPredictions: predictions }),
      addStep: (entry) =>
        set((state) => ({ stepHistory: [...state.stepHistory, entry] })),
      undoStep: () =>
        set((state) => {
          const newHistory = state.stepHistory.slice(0, -1)
          const lastEntry = newHistory.length > 0 ? newHistory[newHistory.length - 1] : null
          return {
            stepHistory: newHistory,
            currentPredictions: lastEntry
              ? {
                  predictions: lastEntry.predictions,
                  inputTokenCount: 0,
                  modelId: state.currentPredictions?.modelId ?? '',
                  totalProbability: lastEntry.predictions.reduce(
                    (sum, p) => sum + p.probability,
                    0
                  ),
                }
              : state.currentPredictions,
          }
        }),
      clearSteps: () => set({ stepHistory: [] }),
      addBranch: (token, exploration) =>
        set((state) => ({
          branches: [...state.branches, { token, exploration }],
        })),
      clearBranches: () => set({ branches: [] }),
      setLoading: (loading) => set({ isLoading: loading }),
      reset: () => set({ ...defaultParameters, ...defaultTransient }),
    }),
    {
      name: 'prism-token-explorer-state',
      partialize: (state) => ({
        instanceId: state.instanceId,
        prompt: state.prompt,
        temperature: state.temperature,
        topP: state.topP,
        topK: state.topK,
        topLogprobs: state.topLogprobs,
        enableThinking: state.enableThinking,
      }),
    }
  )
)
