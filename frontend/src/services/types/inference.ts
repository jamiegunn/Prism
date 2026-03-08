import type { LogprobsData, TokenLogprob } from './logprobs'

export interface ChatMessage {
  role: 'system' | 'user' | 'assistant'
  content: string
}

export interface ChatRequest {
  model: string
  messages: ChatMessage[]
  temperature?: number
  topP?: number
  topK?: number
  maxTokens?: number
  stopSequences?: string[]
  frequencyPenalty?: number
  presencePenalty?: number
  logprobs?: boolean
  topLogprobs?: number
  stream?: boolean
  sourceModule?: string
}

export interface ChatResponse {
  content: string
  finishReason: string
  usage: TokenUsage
  logprobsData?: LogprobsData
  modelId: string
  latencyMs: number
  ttftMs?: number
  tokensPerSecond?: number
}

export interface TokenUsage {
  promptTokens: number
  completionTokens: number
  totalTokens: number
}

export interface StreamChunk {
  content: string
  index: number
  logprobsEntry?: TokenLogprob
  finishReason?: string
  isFirst: boolean
  usage?: TokenUsage
}
