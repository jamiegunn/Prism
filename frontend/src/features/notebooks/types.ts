export interface NotebookSummary {
  id: string
  projectId: string | null
  name: string
  description: string | null
  version: number
  sizeBytes: number
  kernelName: string
  lastEditedAt: string | null
  createdAt: string
}

export interface NotebookDetail {
  id: string
  projectId: string | null
  name: string
  description: string | null
  content: string
  version: number
  sizeBytes: number
  kernelName: string
  lastEditedAt: string | null
  createdAt: string
}
