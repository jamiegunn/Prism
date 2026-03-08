import type { LogprobsData } from '@/services/types/logprobs'

export type { LogprobsData }
export type { TokenLogprob, TopLogprob } from '@/services/types/logprobs'

export interface Conversation {
  id: string
  title: string
  systemPrompt: string | null
  modelId: string
  instanceId: string
  parameters: ConversationParameters
  messages: Message[]
  isPinned: boolean
  totalTokens: number
  lastMessageAt: string | null
  createdAt: string
  updatedAt: string
}

export interface ConversationSummary {
  id: string
  title: string
  modelId: string
  messageCount: number
  totalTokens: number
  lastMessageAt: string | null
  isPinned: boolean
  createdAt: string
}

export interface Message {
  id: string
  conversationId: string
  role: 'System' | 'User' | 'Assistant'
  content: string
  tokenCount: number | null
  logprobsData: LogprobsData | null
  perplexity: number | null
  latencyMs: number | null
  ttftMs: number | null
  tokensPerSecond: number | null
  finishReason: string | null
  sortOrder: number
  createdAt: string
}

export interface ConversationParameters {
  temperature: number | null
  topP: number | null
  topK: number | null
  maxTokens: number | null
  stopSequences: string[] | null
  frequencyPenalty: number | null
  presencePenalty: number | null
  logprobs: boolean
  topLogprobs: number | null
}

export interface SendMessageRequest {
  conversationId?: string
  instanceId: string
  systemPrompt?: string
  message: string
  temperature?: number
  topP?: number
  topK?: number
  maxTokens?: number
  stopSequences?: string[]
  frequencyPenalty?: number
  presencePenalty?: number
  logprobs?: boolean
  topLogprobs?: number
}

export interface TokenLogprobInfo {
  token: string
  logprob: number
  probability: number
  topAlternatives: TokenAlternative[]
}

export interface TokenAlternative {
  token: string
  logprob: number
  probability: number
}

export interface StreamToken {
  content: string
  logprob: TokenLogprobInfo | null
}

export interface ChatStartedEvent {
  conversationId: string
  messageId: string
}

export interface ChatTokenEvent {
  content: string
  logprob?: TokenLogprobInfo
}

export interface ChatCompletedEvent {
  message: Message
  conversation: Conversation
}

export interface ChatErrorEvent {
  error: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasNextPage: boolean
  hasPreviousPage: boolean
}
