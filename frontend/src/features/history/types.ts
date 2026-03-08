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
  responsePreview: string
  promptTokens: number
  completionTokens: number
  latencyMs: number
  isSuccess: boolean
  tags: string[]
  startedAt: string
}

/** Full detail for a single history record. */
export interface HistoryRecordDetail {
  id: string
  sourceModule: string
  model: string
  providerName: string
  promptPreview: string
  responsePreview: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
  latencyMs: number
  ttftMs: number | null
  perplexity: number | null
  isSuccess: boolean
  tags: string[]
  startedAt: string
  requestJson: string
  responseJson: string
  environmentJson: string
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
