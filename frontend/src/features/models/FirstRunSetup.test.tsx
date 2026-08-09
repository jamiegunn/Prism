import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { FirstRunSetup } from './FirstRunSetup'

/**
 * The first run used to be an empty list with no explanation. These tests pin the two things
 * that make the replacement worth having: that it tells someone with nothing running what to
 * do, and that it states a provider's limitations *before* they commit to it.
 */

function renderWithQuery(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

function mockDiscovery(body: unknown, ok = true) {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok,
      status: ok ? 200 : 500,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => body,
      text: async () => JSON.stringify(body),
    }),
  )
}

beforeEach(() => {
  vi.restoreAllMocks()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('FirstRunSetup', () => {
  it('tells a user with nothing running exactly what to do', async () => {
    mockDiscovery({ found: [], probed: ['http://localhost:8000', 'http://localhost:11434'] })

    renderWithQuery(<FirstRunSetup />)

    // Naming the ports it checked turns "nothing found" from a dead end into a diagnosis.
    expect(await screen.findByText(/localhost:11434/)).toBeInTheDocument()
    expect(screen.getByText(/ollama serve/)).toBeInTheDocument()
  })

  it('warns that a chat-only provider leaves the token views empty, before it is chosen', async () => {
    mockDiscovery({
      found: [
        {
          providerType: 'Ollama',
          endpoint: 'http://localhost:11434',
          suggestedName: 'Local Ollama',
          models: ['mistral:7b-instruct'],
          supportsLogprobs: false,
          alreadyRegistered: false,
          note: 'Ollama does not return per-token probabilities, so the heatmap will be empty.',
        },
      ],
      probed: [],
    })

    renderWithQuery(<FirstRunSetup />)

    expect(await screen.findByText(/Chat only/)).toBeInTheDocument()
    expect(screen.getByText(/heatmap will be empty/)).toBeInTheDocument()

    // The limitation must be visible at the point of choosing, not after.
    expect(screen.getByRole('button', { name: /use this/i })).toBeEnabled()
  })

  it('marks a fully capable provider differently', async () => {
    mockDiscovery({
      found: [
        {
          providerType: 'Vllm',
          endpoint: 'http://localhost:8000',
          suggestedName: 'Local vLLM',
          models: ['meta-llama/Llama-3.1-8B-Instruct'],
          supportsLogprobs: true,
          alreadyRegistered: false,
          note: 'Token heatmaps, entropy and guided decoding all work.',
        },
      ],
      probed: [],
    })

    renderWithQuery(<FirstRunSetup />)

    // The badge is the at-a-glance signal; the note explains it.
    expect(await screen.findByText('Full introspection')).toBeInTheDocument()
    expect(screen.getByText(/entropy and guided decoding all work/)).toBeInTheDocument()
    expect(screen.queryByText('Chat only')).not.toBeInTheDocument()
  })

  it('does not offer to add a provider that is already registered', async () => {
    mockDiscovery({
      found: [
        {
          providerType: 'Vllm',
          endpoint: 'http://localhost:8000',
          suggestedName: 'Local vLLM',
          models: [],
          supportsLogprobs: true,
          alreadyRegistered: true,
          note: 'Full introspection.',
        },
      ],
      probed: [],
    })

    renderWithQuery(<FirstRunSetup />)

    expect(await screen.findByRole('button', { name: /already added/i })).toBeDisabled()
  })

  it('says the backend is unreachable rather than reporting no providers', async () => {
    // These are different diagnoses and lead somewhere different. Collapsing them would send a
    // user hunting for a model server when the API is what is down.
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('connection refused')))

    renderWithQuery(<FirstRunSetup />)

    expect(await screen.findByText(/Could not reach the Prism API/)).toBeInTheDocument()
  })
})
