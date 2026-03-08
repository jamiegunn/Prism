export interface TokenPredictionEntry {
  token: string
  logprob: number
  probability: number
  cumulativeProbability: number
}

export interface NextTokenPrediction {
  predictions: TokenPredictionEntry[]
  inputTokenCount: number
  modelId: string
  totalProbability: number
}

export interface StepThroughResult {
  extendedText: string
  appendedToken: string
  nextPredictions: NextTokenPrediction
}

export interface BranchToken {
  token: string
  logprob: number
  probability: number
  topAlternatives: { token: string; logprob: number; probability: number }[]
}

export interface BranchExploration {
  forcedToken: string
  generatedText: string
  tokens: BranchToken[]
  perplexity: number | null
  modelId: string
}

export interface StepEntry {
  token: string
  probability: number
  logprob: number
  wasForced: boolean
  predictions: TokenPredictionEntry[]
}

export interface PredictRequest {
  instanceId: string
  prompt: string
  topLogprobs?: number
  temperature?: number
  enableThinking?: boolean
}

export interface StepRequest {
  instanceId: string
  prompt: string
  selectedToken: string
  previousTokens?: string
  topLogprobs?: number
  temperature?: number
  enableThinking?: boolean
}

export interface BranchRequest {
  instanceId: string
  prompt: string
  forcedToken: string
  maxTokens?: number
  temperature?: number
  topLogprobs?: number
  enableThinking?: boolean
}

export interface TokenBlock {
  id: number
  text: string
  displayText: string
  byteLength: number
  hexBytes: string
}

export interface TokenizeResult {
  tokens: TokenBlock[]
  tokenCount: number
  characterCount: number
  byteCount: number
  modelId: string
}

export interface InstanceTokenizeResult {
  instanceId: string
  instanceName: string
  modelId: string
  tokenization: TokenizeResult | null
  error: string | null
}

export interface CompareTokenizeResult {
  text: string
  results: InstanceTokenizeResult[]
}
