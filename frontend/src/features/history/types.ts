/** Paged result wrapper matching the API contract. */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

/** Summary record returned from the history list endpoint. */
export interface HistoryRecord {
  id: string
  sourceModule: string
  model: string
  providerName: string
  promptPreview: string
  /** Null when the call failed before producing a response. */
  responsePreview: string | null
  promptTokens: number
  completionTokens: number
  latencyMs: number
  isSuccess: boolean
  tags: string[]
  startedAt: string
}

/** Full detail for a single history record — mirrors InferenceRecordDetailDto. */
export interface HistoryRecordDetail {
  id: string
  sourceModule: string
  model: string
  providerName: string
  providerEndpoint: string
  providerType: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
  latencyMs: number
  ttftMs: number | null
  perplexity: number | null
  isSuccess: boolean
  errorMessage: string | null
  tags: string[]
  startedAt: string
  completedAt: string
  requestJson: string
  /** Null when the call failed before producing a response. */
  responseJson: string | null
  environmentJson: string | null
  /** W3C trace id of the span covering this call; null when tracing was not active. */
  traceId: string | null
  /** Span id of the inference span; null when tracing was not active. */
  spanId: string | null
}

/** Parameters for querying history records. */
export interface HistoryFilterParams {
  search?: string
  sourceModule?: string
  model?: string
  from?: string
  to?: string
  tags?: string
  isSuccess?: boolean
  page?: number
  pageSize?: number
}

/** Response from the replay endpoint. */
export interface ReplayResult {
  originalRecordId: string
  original: string
  replayResponseContent: string
  replayPromptTokens: number
  replayCompletionTokens: number
  replayLatencyMs: number
  replayModel: string
  diffSummary: string
}

/** One alternative the model considered at a trace position. */
export interface TraceAlternative {
  token: string
  logprob: number
  probability: number
}

/** One token of a recorded trace, in generation order. */
export interface TraceToken {
  position: number
  token: string
  logprob: number
  probability: number
  entropy: number
  isSurprise: boolean
  topLogprobs: TraceAlternative[]
}

/** A recorded per-token trace. */
export interface InferenceTrace {
  inferenceRecordId: string
  perplexity: number | null
  meanEntropy: number | null
  averageLogprob: number | null
  surpriseTokenCount: number
  surpriseThreshold: number
  schemaVersion: string
  tokens: TraceToken[]
}

/** The trace endpoint's response: a trace, or the stated reason there is none. */
export interface TraceResponse {
  hasTrace: boolean
  absenceReason: string | null
  trace: InferenceTrace | null
}
