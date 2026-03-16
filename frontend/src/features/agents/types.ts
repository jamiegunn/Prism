export interface AgentWorkflow {
  id: string
  projectId: string | null
  name: string
  description: string | null
  systemPrompt: string
  model: string
  instanceId: string
  pattern: string
  maxSteps: number
  tokenBudget: number
  temperature: number
  enabledTools: string[]
  version: number
  runCount: number
  createdAt: string
  updatedAt: string
}

export interface AgentRun {
  id: string
  workflowId: string
  status: string
  input: string
  output: string | null
  errorMessage: string | null
  steps: AgentStep[]
  stepCount: number
  totalTokens: number
  totalLatencyMs: number
  startedAt: string | null
  completedAt: string | null
  createdAt: string
}

export interface AgentStep {
  index: number
  thought: string | null
  action: string | null
  actionInput: string | null
  observation: string | null
  isFinalAnswer: boolean
  finalAnswer: string | null
  tokensUsed: number
  latencyMs: number
  error: string | null
}

export interface AgentTool {
  name: string
  description: string
  parameterSchema: string
}
