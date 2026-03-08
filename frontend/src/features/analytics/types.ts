export interface UsageSummary {
  totalRequests: number
  totalPromptTokens: number
  totalCompletionTokens: number
  totalTokens: number
  totalCost: number | null
  byModel: UsageByModel[]
  byModule: UsageByModule[]
  timeSeries: UsageTimeSeries[]
}

export interface UsageByModel {
  model: string
  requestCount: number
  totalTokens: number
  totalCost: number | null
}

export interface UsageByModule {
  module: string
  requestCount: number
  totalTokens: number
}

export interface UsageTimeSeries {
  date: string
  requestCount: number
  totalTokens: number
}

export interface PerformanceSummary {
  averageLatencyMs: number
  p50LatencyMs: number
  p95LatencyMs: number
  p99LatencyMs: number
  averageTtftMs: number | null
  averageTokensPerSecond: number | null
  byModel: PerformanceByModel[]
}

export interface PerformanceByModel {
  model: string
  requestCount: number
  averageLatencyMs: number
  p50LatencyMs: number
  p95LatencyMs: number
  averageTtftMs: number | null
  averageTokensPerSecond: number | null
}
