import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { StreamToken } from './types'

interface PlaygroundState {
  // Persisted parameters
  selectedInstanceId: string | null
  systemPrompt: string
  temperature: number
  topP: number
  topK: number
  maxTokens: number
  stopSequences: string[]
  frequencyPenalty: number
  presencePenalty: number
  logprobs: boolean
  topLogprobs: number

  // Transient streaming state
  currentConversationId: string | null
  streamingTokens: StreamToken[]
  isStreaming: boolean
  showLogprobsPanel: boolean
  selectedMessageId: string | null

  // Actions
  setSelectedInstanceId: (id: string | null) => void
  setSystemPrompt: (prompt: string) => void
  setTemperature: (value: number) => void
  setTopP: (value: number) => void
  setTopK: (value: number) => void
  setMaxTokens: (value: number) => void
  setStopSequences: (sequences: string[]) => void
  setFrequencyPenalty: (value: number) => void
  setPresencePenalty: (value: number) => void
  setLogprobs: (enabled: boolean) => void
  setTopLogprobs: (value: number) => void
  setConversationId: (id: string | null) => void
  addStreamToken: (token: StreamToken) => void
  clearTokens: () => void
  setStreaming: (streaming: boolean) => void
  setShowLogprobsPanel: (show: boolean) => void
  setSelectedMessageId: (id: string | null) => void
  resetStream: () => void
  reset: () => void
}

const defaultParameters = {
  selectedInstanceId: null as string | null,
  systemPrompt: '',
  temperature: 0.7,
  topP: 0.9,
  topK: 50,
  maxTokens: 2048,
  stopSequences: [] as string[],
  frequencyPenalty: 0,
  presencePenalty: 0,
  logprobs: true,
  topLogprobs: 5,
}

const defaultStreamState = {
  currentConversationId: null as string | null,
  streamingTokens: [] as StreamToken[],
  isStreaming: false,
  showLogprobsPanel: false,
  selectedMessageId: null as string | null,
}

export const usePlaygroundStore = create<PlaygroundState>()(
  persist(
    (set) => ({
      ...defaultParameters,
      ...defaultStreamState,

      // Parameter setters
      setSelectedInstanceId: (id) => set({ selectedInstanceId: id }),
      setSystemPrompt: (prompt) => set({ systemPrompt: prompt }),
      setTemperature: (value) => set({ temperature: value }),
      setTopP: (value) => set({ topP: value }),
      setTopK: (value) => set({ topK: value }),
      setMaxTokens: (value) => set({ maxTokens: value }),
      setStopSequences: (sequences) => set({ stopSequences: sequences }),
      setFrequencyPenalty: (value) => set({ frequencyPenalty: value }),
      setPresencePenalty: (value) => set({ presencePenalty: value }),
      setLogprobs: (enabled) => set({ logprobs: enabled }),
      setTopLogprobs: (value) => set({ topLogprobs: value }),

      // Streaming state setters
      setConversationId: (id) => set({ currentConversationId: id }),
      addStreamToken: (token) =>
        set((state) => ({ streamingTokens: [...state.streamingTokens, token] })),
      clearTokens: () => set({ streamingTokens: [] }),
      setStreaming: (streaming) => set({ isStreaming: streaming }),
      setShowLogprobsPanel: (show) => set({ showLogprobsPanel: show }),
      setSelectedMessageId: (id) => set({ selectedMessageId: id }),
      resetStream: () => set(defaultStreamState),
      reset: () => set({ ...defaultParameters, ...defaultStreamState }),
    }),
    {
      name: 'prism-playground-state',
      partialize: (state) => ({
        selectedInstanceId: state.selectedInstanceId,
        systemPrompt: state.systemPrompt,
        temperature: state.temperature,
        topP: state.topP,
        topK: state.topK,
        maxTokens: state.maxTokens,
        stopSequences: state.stopSequences,
        frequencyPenalty: state.frequencyPenalty,
        presencePenalty: state.presencePenalty,
        logprobs: state.logprobs,
        topLogprobs: state.topLogprobs,
      }),
    }
  )
)
