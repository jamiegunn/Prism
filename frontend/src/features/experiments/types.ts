/** Paged result wrapper matching the API contract. */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

/** Research project containing experiments. */
export interface Project {
  id: string
  name: string
  description: string | null
  isArchived: boolean
  experimentCount: number
  createdAt: string
  updatedAt: string
}

/** Experiment within a project. */
export interface Experiment {
  id: string
  projectId: string
  name: string
  description: string | null
  hypothesis: string | null
  status: ExperimentStatus
  runCount: number
  createdAt: string
  updatedAt: string
}

export type ExperimentStatus = 'Active' | 'Completed' | 'Archived'

/** Inference parameters used for a run. */
export interface RunParameters {
  temperature: number | null
  topP: number | null
  topK: number | null
  maxTokens: number | null
  stopSequences: string[] | null
  frequencyPenalty: number | null
  presencePenalty: number | null
}

export type RunStatus = 'Pending' | 'Running' | 'Completed' | 'Failed'

/** A single experiment run with full metrics. */
export interface Run {
  id: string
  experimentId: string
  name: string | null
  model: string
  instanceId: string | null
  parameters: RunParameters
  input: string
  output: string | null
  systemPrompt: string | null
  metrics: Record<string, number>
  promptTokens: number
  completionTokens: number
  totalTokens: number
  cost: number | null
  latencyMs: number
  ttftMs: number | null
  tokensPerSecond: number | null
  perplexity: number | null
  logprobsData: string | null
  status: RunStatus
  error: string | null
  tags: string[]
  finishReason: string | null
  createdAt: string
}

/** Result of comparing multiple runs. */
export interface RunComparison {
  runs: Run[]
  parameterDiffs: Record<string, (string | null)[]>
  metricComparison: Record<string, (number | null)[]>
}

/** Parameters for listing runs. */
export interface ListRunsParams {
  model?: string
  status?: string
  sortBy?: string
  order?: string
  page?: number
  pageSize?: number
}
