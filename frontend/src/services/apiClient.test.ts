import { describe, it, expect, vi, afterEach } from 'vitest'
import { apiClient, ApiError } from './apiClient'

/*
 * The API client's handling of a successful response that has no body.
 *
 * Every Result-returning delete in this API maps success to 204 No Content, and the client used
 * to call response.json() unconditionally. Empty body, thrown SyntaxError, rejected promise —
 * so a delete that had already succeeded surfaced as a failure: an error toast, no cache
 * invalidation, and any navigation in onSuccess skipped. The row really was gone, which is the
 * worst version of this: the UI disagreed with the database and the reader believed the UI.
 */

function respond(status: number, body: string, headers: Record<string, string> = {}) {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(headers),
    json: () => (body ? Promise.resolve(JSON.parse(body)) : Promise.reject(new SyntaxError('Unexpected end of JSON input'))),
    text: () => Promise.resolve(body),
  }))
}

afterEach(() => vi.unstubAllGlobals())

describe('apiClient', () => {
  it('resolves rather than throwing on 204 No Content', async () => {
    respond(204, '')

    await expect(apiClient('/datasets/abc', { method: 'DELETE' })).resolves.toBeUndefined()
  })

  it('resolves on a 200 that declares an empty body', async () => {
    respond(200, '', { 'content-length': '0' })

    await expect(apiClient('/whatever')).resolves.toBeUndefined()
  })

  it('still parses a normal JSON response', async () => {
    respond(200, '{"id":"abc"}', { 'content-type': 'application/json' })

    await expect(apiClient<{ id: string }>('/datasets/abc')).resolves.toEqual({ id: 'abc' })
  })

  it('still throws ApiError on a failure, with the status', async () => {
    respond(404, '{"title":"Not found"}')

    await expect(apiClient('/datasets/missing')).rejects.toBeInstanceOf(ApiError)
    await expect(apiClient('/datasets/missing')).rejects.toMatchObject({ status: 404 })
  })

  it('reports the problem detail rather than the error category', async () => {
    // ProblemDetails names the category in `title` ("Validation") and the reason in `detail`.
    // Preferring the title told every reader what kind of error it was and never why.
    respond(400, '{"title":"Validation","detail":"Temperature override must be between 0 and 2."}')

    await expect(apiClient('/history/abc/replay', { method: 'POST' })).rejects.toMatchObject({
      message: 'Temperature override must be between 0 and 2.',
    })
  })
})
