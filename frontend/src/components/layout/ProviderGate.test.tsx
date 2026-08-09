import { describe, expect, it } from 'vitest'
import { describeProviderState } from './ProviderGate'
import type { InferenceInstance } from '@/features/models/types'

/**
 * The first-run experience used to live only on the Models page, so someone landing on the
 * Playground with nothing connected saw a disabled text box and no reason for it. These pin the
 * four states the banner has to tell apart, because collapsing any two of them sends a user
 * looking in the wrong place.
 */

function instance(overrides: Partial<InferenceInstance> = {}): InferenceInstance {
  return {
    id: 'a',
    name: 'Local vLLM',
    endpoint: 'http://localhost:8000',
    providerType: 'Vllm',
    status: 'Online',
    isDefault: false,
    tags: [],
    supportsLogprobs: true,
    supportsStreaming: true,
    supportsMetrics: true,
    supportsTokenize: true,
    supportsGuidedDecoding: true,
    supportsMultimodal: false,
    supportsModelSwap: false,
    ...overrides,
  } as InferenceInstance
}

describe('describeProviderState', () => {
  it('says nothing when a model is online', () => {
    expect(describeProviderState([instance()], false)).toBeNull()
  })

  it('stays quiet while the list is still loading', () => {
    // Otherwise the banner flashes on every page load before the request resolves.
    expect(describeProviderState(undefined, false)).toBeNull()
  })

  it('distinguishes an unreachable API from having no providers', () => {
    // These lead somewhere completely different: one is "start the backend", the other is
    // "connect a model". Collapsing them sends someone hunting for a model server when the
    // API is what is down.
    const apiDown = describeProviderState(undefined, true)

    expect(apiDown?.tone).toBe('error')
    expect(apiDown?.title).toMatch(/Cannot reach the Prism API/)
    expect(apiDown?.detail).toMatch(/doctor\.sh/)
  })

  it('offers to connect one when none is registered', () => {
    const empty = describeProviderState([], false)

    expect(empty?.tone).toBe('warning')
    expect(empty?.title).toBe('No model connected')
    expect(empty?.action).toBe('Connect a model')
  })

  it('separates registered-but-offline from not-registered-at-all', () => {
    // A stale registration pointing at a server that is no longer running is the confusing
    // case: the model list looks populated and every panel is still empty.
    const offline = describeProviderState([instance({ status: 'Offline' })], false)

    expect(offline?.title).toBe('Local vLLM is not responding')
    expect(offline?.detail).toMatch(/registered but not reachable/)
  })

  it('counts them when several are all offline', () => {
    const offline = describeProviderState(
      [
        instance({ id: 'a', name: 'vLLM', status: 'Offline' }),
        instance({ id: 'b', name: 'Ollama', status: 'Unknown' }),
      ],
      false,
    )

    expect(offline?.title).toBe('None of your 2 models are responding')
  })

  it('is silent when at least one of several is up', () => {
    // One working model is enough to use the product; nagging about the others would train
    // people to ignore the banner.
    expect(
      describeProviderState(
        [instance({ id: 'a', status: 'Offline' }), instance({ id: 'b', status: 'Online' })],
        false,
      ),
    ).toBeNull()
  })
})
