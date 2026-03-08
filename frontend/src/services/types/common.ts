export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface ApiErrorResponse {
  type: string
  title: string
  status: number
  detail?: string
  errors?: Record<string, string[]>
}
