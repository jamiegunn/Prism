import { describe, expect, it } from 'vitest'
import { pickDefaultInstance, resolveSelection } from './useDefaultInstance'
import type { InferenceInstance } from './types'

/**
 * The Playground opened on "Select an instance..." with the composer disabled, even when
 * exactly one working model was registered and set as the default. Starting Prism, watching it
 * pull a model, and then being told to pick one makes the setup look like it did not work.
 *
 * These pin the choice rather than the wiring, because the choice is where the judgement is.
 */

function instance(overrides: Partial<InferenceInstance> = {}): InferenceInstance {
  return {
    id: 'a',
    name: 'Local Ollama',
    endpoint: 'http://localhost:11434',
    providerType: 'Ollama',
    status: 'Online',
    modelId: 'mistral:7b-instruct',
    gpuConfig: null,
    maxContextLength: 8192,
    supportsLogprobs: false,
    maxTopLogprobs: 0,
    supportsStreaming: true,
    supportsMetrics: false,
    supportsTokenize: false,
    supportsGuidedDecoding: false,
    supportsMultimodal: false,
    supportsModelSwap: true,
    isDefault: false,
    lastHealthCheck: null,
    lastHealthError: null,
    tags: [],
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

describe('pickDefaultInstance', () => {
  it('picks the only online instance', () => {
    expect(pickDefaultInstance([instance({ id: 'only' })])?.id).toBe('only')
  })

  it('prefers the one marked default', () => {
    const chosen = pickDefaultInstance([
      instance({ id: 'first' }),
      instance({ id: 'preferred', isDefault: true }),
    ])

    expect(chosen?.id).toBe('preferred')
  })

  it('skips an offline instance even when it is the marked default', () => {
    // A working model beats a stated preference that is switched off. The seed data registers
    // a vLLM instance that is usually not running, so this is the common case, not an edge one.
    const chosen = pickDefaultInstance([
      instance({ id: 'marked-but-down', isDefault: true, status: 'Offline' }),
      instance({ id: 'actually-up' }),
    ])

    expect(chosen?.id).toBe('actually-up')
  })

  it('selects nothing rather than something offline', () => {
    // Selecting a dead instance would let someone send a prompt that cannot succeed, and the
    // failure would read as a bug in Prism rather than a model that is not running.
    expect(
      pickDefaultInstance([
        instance({ id: 'a', status: 'Offline' }),
        instance({ id: 'b', status: 'Unknown' }),
      ]),
    ).toBeNull()
  })

  it('handles an empty list and a list that has not loaded', () => {
    expect(pickDefaultInstance([])).toBeNull()
    expect(pickDefaultInstance(undefined)).toBeNull()
  })
})

describe('resolveSelection', () => {
  it('fills in a selection when there is none', () => {
    expect(resolveSelection(null, [instance({ id: 'x' })])).toBe('x')
  })

  it('leaves an existing selection alone', () => {
    expect(resolveSelection('x', [instance({ id: 'x' }), instance({ id: 'y' })])).toBeNull()
  })

  it('does not move you off a model that merely went offline', () => {
    // Silently switching someone to a different model mid-session would change their results
    // without saying so, which matters more here than the convenience of always being usable.
    expect(resolveSelection('x', [instance({ id: 'x', status: 'Offline' })])).toBeNull()
  })

  it('replaces a selection pointing at an instance that no longer exists', () => {
    // The id is persisted in localStorage, so removing an instance and coming back leaves the
    // page pointing at a dead id: blank dropdown, refuses to send, no explanation.
    expect(resolveSelection('deleted', [instance({ id: 'live' })])).toBe('live')
  })

  it('does nothing while the list is still loading', () => {
    expect(resolveSelection(null, undefined)).toBeNull()
  })
})
