export interface JsonSchema {
  id: string
  projectId: string | null
  name: string
  description: string | null
  schemaJson: string
  version: number
  createdAt: string
  updatedAt: string
}

export interface StructuredInferenceResult {
  rawOutput: string
  parsedJson: unknown
  isValid: boolean
  validationErrors: string[]
  promptTokens: number
  completionTokens: number
  latencyMs: number
}
