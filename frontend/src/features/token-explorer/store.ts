import { useMemo } from 'react'
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { applyTemperature } from './temperature'
import type {
  NextTokenPrediction,
  StepEntry,
  BranchExploration,
  TokenPredictionEntry,
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

/** The slice of state that is persisted, which is what a migration receives. */
type PersistedParameters = {
  instanceId: string | null
  prompt: string
  temperature: number
  topP: number
  topK: number
  topLogprobs: number
  enableThinking: boolean
}

const defaultParameters = {
  instanceId: null as string | null,
  prompt: '',
  // 1 is the model's own distribution: what a server returns is already the temperature-1
  // probabilities. The old default of 0 asked for greedy, which the page then could not show,
  // because it displayed the returned distribution unchanged whatever this said.
  temperature: 1,
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

      // Bumped when temperature started reshaping the displayed distribution. A stored 0 was
      // the old default rather than anyone's choice, and under the new meaning it would draw a
      // single bar at 100% — so it is moved to 1, the model's own distribution. A stored value
      // someone actually picked is left where they put it.
      version: 1,
      migrate: (persisted, version) => {
        const state = persisted as PersistedParameters

        if (version === 0 && state?.temperature === 0) {
          return { ...state, temperature: 1 }
        }

        return state
      },
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

/**
 * The current predictions as the chosen temperature would shape them.
 *
 * Shared by the distribution and the statistics panel so the two cannot show different numbers
 * for the same position.
 */
export function useAdjustedPredictions(): TokenPredictionEntry[] {
  const currentPredictions = useTokenExplorerStore((s) => s.currentPredictions)
  const temperature = useTokenExplorerStore((s) => s.temperature)

  return useMemo(
    () => applyTemperature(currentPredictions?.predictions ?? [], temperature),
    [currentPredictions, temperature]
  )
}
