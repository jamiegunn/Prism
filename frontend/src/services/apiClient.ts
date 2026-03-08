const BASE_URL = '/api/v1'

interface RequestConfig {
  method?: string
  body?: unknown
  headers?: Record<string, string>
  signal?: AbortSignal
}

export async function apiClient<T>(url: string, config?: RequestConfig): Promise<T> {
  const response = await fetch(`${BASE_URL}${url}`, {
    method: config?.method ?? 'GET',
    headers: {
      'Content-Type': 'application/json',
      ...config?.headers,
    },
    body: config?.body ? JSON.stringify(config.body) : undefined,
    signal: config?.signal,
  })

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: response.statusText })) as Record<string, unknown>
    throw new ApiError(
      response.status,
      (error.title as string) ?? (error.message as string) ?? 'Request failed',
      error
    )
  }

  return response.json() as Promise<T>
}

export class ApiError extends Error {
  status: number
  details?: unknown

  constructor(status: number, message: string, details?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.details = details
  }
}

// For orval custom instance
export const customInstance = apiClient
