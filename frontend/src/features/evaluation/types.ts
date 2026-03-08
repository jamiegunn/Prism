export interface Evaluation {
  id: string
  projectId: string | null
  datasetId: string
  splitLabel: string | null
  name: string
  models: string[]
  promptVersionId: string | null
  scoringMethods: string[]
  config: Record<string, unknown>
  status: string
  progress: number
  totalRecords: number
  completedRecords: number
  failedRecords: number
  errorMessage: string | null
  startedAt: string | null
  finishedAt: string | null
  createdAt: string
  updatedAt: string
}

export interface EvaluationResult {
  id: string
  evaluationId: string
  model: string
  recordId: string
  input: string
  expectedOutput: string | null
  actualOutput: string | null
  scores: Record<string, number>
  logprobsData: string | null
  perplexity: number | null
  latencyMs: number
  promptTokens: number
  completionTokens: number
  error: string | null
  createdAt: string
}

export interface EvaluationSummary {
  evaluationId: string
  modelSummaries: ModelSummary[]
}

export interface ModelSummary {
  model: string
  recordCount: number
  averageScores: Record<string, number>
  averageLatencyMs: number
  totalPromptTokens: number
  totalCompletionTokens: number
  errorCount: number
}

export interface LeaderboardEntry {
  evaluationId: string
  evaluationName: string
  model: string
  averageScores: Record<string, number>
  recordCount: number
  averageLatencyMs: number
  evaluatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}
