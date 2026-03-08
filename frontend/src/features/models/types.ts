export interface InferenceInstance {
  id: string
  name: string
  endpoint: string
  providerType: string
  status: 'Unknown' | 'Online' | 'Degraded' | 'Offline'
  modelId: string | null
  gpuConfig: string | null
  maxContextLength: number | null
  supportsLogprobs: boolean
  maxTopLogprobs: number
  supportsStreaming: boolean
  supportsMetrics: boolean
  supportsTokenize: boolean
  supportsGuidedDecoding: boolean
  supportsMultimodal: boolean
  supportsModelSwap: boolean
  isDefault: boolean
  lastHealthCheck: string | null
  lastHealthError: string | null
  tags: string[]
  createdAt: string
  updatedAt: string
}

export interface InstanceMetrics {
  instanceId: string
  modelId: string | null
  status: string
  gpuUtilization: number | null
  gpuMemoryUsed: number | null
  gpuMemoryTotal: number | null
  kvCacheUtilization: number | null
  activeRequests: number | null
  requestsPerSecond: number | null
  queueDepth: number | null
}

export interface RegisterInstanceRequest {
  name: string
  endpoint: string
  providerType: string
  isDefault?: boolean
  tags?: string[]
}
