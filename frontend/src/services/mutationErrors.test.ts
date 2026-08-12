import { describe, it, expect } from 'vitest'
import { ApiError } from './apiClient'
import { describeMutationError } from './mutationErrors'

/*
 * What a failed write tells the reader.
 *
 * Most mutations in this app passed no `onError`, so a failure resolved into nothing at all —
 * the delete looked like it worked, the tag looked saved, the run looked started. That is worse
 * than a bad error message, because the reader carries on believing it happened. These pin the
 * distinctions that change what someone does next: their request versus the server's fault, and
 * a server that answered badly versus one that is not there.
 */

describe('describeMutationError', () => {
  it('passes a 4xx message through, since it is about the request', () => {
    expect(describeMutationError(new ApiError(400, 'Name is required'))).toBe('Name is required')
  })

  it('says a 404 may already be gone', () => {
    const message = describeMutationError(new ApiError(404, 'Collection not found'))

    expect(message).toContain('Collection not found')
    expect(message).toMatch(/deleted already/i)
  })

  it('says a 409 means something else got there first', () => {
    expect(describeMutationError(new ApiError(409, 'Version conflict'))).toMatch(/changed it first/i)
  })

  it('does not blame the reader for a 5xx', () => {
    const message = describeMutationError(new ApiError(500, 'Object reference not set'))

    // The raw server message is noise to a reader and often frightening; point them at the log.
    expect(message).toMatch(/server failed/i)
    expect(message).toContain('500')
    expect(message).not.toContain('Object reference')
  })

  it('passes a 503 through, because it names the dependency that failed', () => {
    // A replay against a model the instance does not serve comes back as 503 with the reason.
    // Hiding it behind "check the API log" costs the reader the one fact they needed.
    const message = describeMutationError(
      new ApiError(503, "Replay of model 'no-such-model' on instance 'Local Ollama' failed: model not found"))

    expect(message).toContain('no-such-model')
    expect(message).toContain('Local Ollama')
  })

  it('distinguishes an unreachable API from a server error', () => {
    // fetch rejects with a TypeError when it cannot connect at all, which is what a restarting
    // API looks like — a completely different thing to do about it than a 500.
    expect(describeMutationError(new TypeError('Failed to fetch'))).toMatch(/reach the Prism API/i)
  })

  it('falls back to a plain error message', () => {
    expect(describeMutationError(new Error('something specific'))).toBe('something specific')
  })

  it('never returns an empty string', () => {
    for (const thrown of [undefined, null, '', 0, {}, new Error('')]) {
      expect(describeMutationError(thrown).length).toBeGreaterThan(0)
    }
  })
})
