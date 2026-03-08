/** Paged result wrapper matching the API contract. */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

/** Column schema definition for a dataset. */
export interface ColumnSchema {
  name: string
  type: string
  purpose: string | null
}

/** Split information within a dataset. */
export interface DatasetSplit {
  id: string
  datasetId: string
  name: string
  recordCount: number
}

/** Dataset DTO. */
export interface Dataset {
  id: string
  projectId: string | null
  name: string
  description: string | null
  format: DatasetFormat
  schema: ColumnSchema[]
  recordCount: number
  sizeBytes: number
  version: number
  splits: DatasetSplit[]
  createdAt: string
  updatedAt: string
}

export type DatasetFormat = 'Csv' | 'Json' | 'Jsonl' | 'Parquet'

/** A single record in a dataset. */
export interface DatasetRecord {
  id: string
  datasetId: string
  data: Record<string, unknown>
  splitLabel: string | null
  orderIndex: number
  createdAt: string
}

/** Dataset statistics. */
export interface DatasetStats {
  recordCount: number
  splitDistribution: Record<string, number>
  columnStats: ColumnStats[]
}

export interface ColumnStats {
  column: string
  nonNullCount: number
  nullCount: number
  uniqueCount: number
  topValues: TopValue[]
}

export interface TopValue {
  value: string
  count: number
}

/** Parameters for splitting a dataset. */
export interface SplitDatasetParams {
  trainRatio: number
  testRatio: number
  valRatio: number
  seed: number | null
}
