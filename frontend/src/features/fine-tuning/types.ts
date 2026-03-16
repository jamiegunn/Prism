export interface LoraAdapter {
  id: string
  name: string
  description: string | null
  instanceId: string
  adapterPath: string
  baseModel: string
  isActive: boolean
  createdAt: string
}

export interface ExportFineTuneResult {
  content: string
  contentType: string
  filename: string
  recordCount: number
  warnings: string[]
}
