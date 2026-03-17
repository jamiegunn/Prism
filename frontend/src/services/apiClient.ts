const BASE_URL = '/api/v1'

/**
 * Request configuration accepted by the API client.
 * Extends RequestInit to accept plain objects as body (auto-serialized to JSON).
 * Compatible with both hand-written feature API calls and Orval-generated calls.
 */
export interface RequestConfig extends Omit<RequestInit, 'body'> {
  body?: BodyInit | object | null
}

export async function apiClient<T>(url: string, config?: RequestConfig): Promise<T> {
  const rawBody = config?.body
  const body: BodyInit | null | undefined = rawBody !== undefined && rawBody !== null
    ? (typeof rawBody === 'string' || rawBody instanceof FormData || rawBody instanceof Blob || rawBody instanceof ArrayBuffer || rawBody instanceof URLSearchParams || rawBody instanceof ReadableStream
      ? rawBody as BodyInit
      : JSON.stringify(rawBody))
    : undefined

  const response = await fetch(`${BASE_URL}${url}`, {
    method: config?.method ?? 'GET',
    headers: {
      'Content-Type': 'application/json',
      ...(config?.headers as Record<string, string> | undefined),
    },
    body,
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

// For orval custom instance — accepts RequestInit from generated code
export const customInstance = apiClient as <T>(url: string, config?: RequestInit) => Promise<T>

// Type helper for orval SecondParameter
export type SecondParameter<T extends (...args: never[]) => unknown> = Parameters<T>[1]
