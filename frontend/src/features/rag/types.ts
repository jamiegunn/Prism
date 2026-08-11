export interface RagCollection {
  id: string
  projectId: string | null
  name: string
  description: string | null
  embeddingModel: string
  dimensions: number
  distanceMetric: string
  chunkingStrategy: string
  chunkSize: number
  chunkOverlap: number
  documentCount: number
  chunkCount: number
  status: string
  createdAt: string
  updatedAt: string
}

export interface RagDocument {
  id: string
  collectionId: string
  filename: string
  contentType: string
  sizeBytes: number
  chunkCount: number
  characterCount: number
  metadata: Record<string, string>
  status: string
  errorMessage: string | null
  createdAt: string
}

export interface ChunkSearchResult {
  chunkId: string
  documentId: string
  documentFilename: string
  content: string
  score: number
  orderIndex: number
  tokenCount: number
  metadata: Record<string, string>
}

export interface RagPipelineResult {
  query: string
  generatedResponse: string
  retrievedChunks: ChunkSearchResult[]
  model: string
  promptTokens: number
  completionTokens: number
  latencyMs: number
  renderedPrompt: string
}

export interface CollectionStats {
  collectionId: string
  name: string
  documentCount: number
  chunkCount: number
  totalCharacters: number
  totalTokens: number
  averageChunkSize: number
  documentsByStatus: Record<string, number>
}

export interface RagQuerySet {
  id: string
  collectionId: string
  name: string
  description: string | null
  itemCount: number
  createdAt: string
}

export interface RagQuerySetItem {
  id: string
  queryText: string
  relevantChunkIds: string[]
}

export interface RagQuerySetDetail {
  id: string
  collectionId: string
  name: string
  description: string | null
  items: RagQuerySetItem[]
}

export interface RetrievalModeResult {
  mode: string
  queryCount: number
  /** Mean metric values, or null when the mode could not run at all. */
  metrics: Record<string, number> | null
  /** Why the mode failed, or null on success. */
  error: string | null
}

export interface RetrievalEvaluation {
  collectionId: string
  querySetId: string
  topK: number
  modes: RetrievalModeResult[]
  definitions: Record<string, string>
}
