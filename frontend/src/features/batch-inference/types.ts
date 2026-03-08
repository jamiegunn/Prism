export interface BatchJob {
  id: string
  datasetId: string
  splitLabel: string | null
  model: string
  promptVersionId: string | null
  parameters: Record<string, unknown>
  concurrency: number
  maxRetries: number
  captureLogprobs: boolean
  status: string
  progress: number
  totalRecords: number
  completedRecords: number
  failedRecords: number
  tokensUsed: number
  cost: number | null
  startedAt: string | null
  finishedAt: string | null
  errorMessage: string | null
  createdAt: string
  updatedAt: string
}

export interface BatchResult {
  id: string
  batchJobId: string
  recordId: string
  input: string
  output: string | null
  logprobsData: string | null
  perplexity: number | null
  tokensUsed: number
  latencyMs: number
  status: string
  error: string | null
  attempt: number
  createdAt: string
}

export interface BatchEstimate {
  recordCount: number
  estimatedTokens: number
  estimatedMinutes: number
  model: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
