/** Variable declared in a prompt template. */
export interface PromptVariable {
  name: string
  type: string // "string" | "number" | "boolean"
  defaultValue: string | null
  description: string | null
  required: boolean
}

/** Few-shot example pair. */
export interface FewShotExample {
  input: string
  output: string
  label: string | null
}

/** Prompt template metadata (without version content). */
export interface PromptTemplate {
  id: string
  projectId: string | null
  name: string
  category: string | null
  description: string | null
  tags: string[]
  latestVersion: number
  createdAt: string
  updatedAt: string
}

/** Full version of a prompt template. */
export interface PromptVersion {
  id: string
  templateId: string
  version: number
  systemPrompt: string | null
  userTemplate: string
  variables: PromptVariable[]
  fewShotExamples: FewShotExample[]
  notes: string | null
  createdAt: string
}

/** Template with its latest version. */
export interface PromptTemplateWithVersion {
  template: PromptTemplate
  latestVersionContent: PromptVersion | null
}

/** Result of testing a prompt against an instance. */
export interface TestPromptResult {
  output: string
  renderedPrompt: string
  modelId: string
  promptTokens: number
  completionTokens: number
  totalTokens: number
  latencyMs: number
  ttftMs: number | null
  tokensPerSecond: number | null
  finishReason: string | null
  runId: string | null
}

/** Result of starting an A/B test. */
export interface AbTestResult {
  experimentId: string
  totalCombinations: number
  status: string
}

/** Diff between two versions. */
export interface VersionDiff {
  version1: PromptVersion
  version2: PromptVersion
}

/** A/B test variation config. */
export interface AbTestVariation {
  templateId: string
  versionNumber: number
  variables: Record<string, string>
}

/** A/B test parameter set. */
export interface AbTestParameterSet {
  temperature?: number
  topP?: number
  maxTokens?: number
}
