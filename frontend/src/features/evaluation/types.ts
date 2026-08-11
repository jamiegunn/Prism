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
  /** Definition of every metric in the summaries, recorded when the evaluation ran. */
  scoreDefinitions: Record<string, string>
  /** Paired comparisons between each model pair, per shared metric. */
  comparisons: ModelComparison[]
}

/** A Student-t 95% confidence interval on a mean per-item score. */
export interface ScoreInterval {
  mean: number
  lower: number
  upper: number
  stdDev: number
  sampleCount: number
}

/** A paired two-sided t comparison of two models on one metric, paired by dataset item. */
export interface ModelComparison {
  metric: string
  modelA: string
  modelB: string
  pairCount: number
  meanDifference: number
  lower: number
  upper: number
  /** Null when every pair differs identically — undefined, not zero. */
  tStatistic: number | null
  /** Two-sided p-value; null when the statistic is undefined. */
  pValue: number | null
}

export interface ModelSummary {
  model: string
  recordCount: number
  /** Mean per-item (sentence-level) score per scoring method. */
  averageScores: Record<string, number>
  /** 95% CI per scoring method; a metric with fewer than two scored items has no entry. */
  scoreIntervals: Record<string, ScoreInterval>
  /** Corpus-level metrics from pooled statistics — not means of per-item scores. */
  corpusMetrics: Record<string, number>
  /** Mean latency over successful items; null when nothing succeeded. */
  averageLatencyMs: number | null
  totalPromptTokens: number
  totalCompletionTokens: number
  errorCount: number
}

export interface CalibrationPrediction {
  confidence: number
  isCorrect: boolean
}

export interface Calibration {
  evaluationId: string
  model: string
  predictions: CalibrationPrediction[]
  ece: number | null
  brier: number | null
  binCount: number
  totalResults: number
  withLogprobs: number
  withLabel: number
  definition: string
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
