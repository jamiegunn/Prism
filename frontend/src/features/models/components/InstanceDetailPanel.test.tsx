import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { InstanceDetailPanel } from './InstanceDetailPanel'
import type { InferenceInstance } from '../types'

/*
 * This panel is the model selector: choosing a model, choosing the default, health, removal.
 *
 * It had no test at all, and the cost showed the first time one line moved. Reading a piece of
 * state a few lines above where it was declared threw "Cannot access 'showSwapInput' before
 * initialization" and replaced the whole page with an error boundary — while the type checker,
 * the linter and every other test passed, because none of them ever rendered it.
 *
 * So these are deliberately shallow. They are not about the panel's cleverness; they are about
 * it rendering at all, and about the two things that must be true of a model picker: that it
 * offers what the server has, and that it refuses to offer what cannot chat.
 */

const INSTANCE: InferenceInstance = {
  id: 'i-1',
  name: 'Local Ollama',
  endpoint: 'http://ollama:11434',
  providerType: 'Ollama',
  modelId: 'mistral:7b-instruct',
  status: 'Online',
  isDefault: false,
  tags: [],
  supportsLogprobs: true,
  supportsStreaming: true,
  supportsMetrics: false,
  supportsTokenize: false,
  supportsGuidedDecoding: true,
  supportsModelSwap: true,
  maxContextLength: 4096,
  maxTopLogprobs: 5,
  supportsMultimodal: false,
  gpuConfig: null,
  lastHealthCheck: null,
  lastHealthError: null,
  createdAt: '2026-08-14T00:00:00Z',
  updatedAt: '2026-08-14T00:00:00Z',
}

function renderPanel(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

/** Answers the models list; everything else gets an empty object. */
function mockApi(models: unknown) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      const body = String(url).includes('/models') && !String(url).endsWith('/instances')
        ? models
        : {}

      return {
        ok: true,
        status: 200,
        headers: new Headers({ 'content-type': 'application/json' }),
        json: async () => body,
        text: async () => JSON.stringify(body),
      }
    }),
  )
}

beforeEach(() => {
  vi.restoreAllMocks()
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('InstanceDetailPanel', () => {
  it('renders without throwing', () => {
    mockApi({ models: [], canList: true, reason: null, embeddingOnly: [] })

    renderPanel(<InstanceDetailPanel instance={INSTANCE} onRemoved={() => {}} />)

    expect(screen.getByText('Local Ollama')).toBeInTheDocument()
  })

  it('offers to make a non-default instance the default', () => {
    mockApi({ models: [], canList: true, reason: null, embeddingOnly: [] })

    renderPanel(<InstanceDetailPanel instance={INSTANCE} onRemoved={() => {}} />)

    expect(screen.getByRole('button', { name: /make default/i })).toBeInTheDocument()
  })

  it('does not offer to make the default the default again', () => {
    mockApi({ models: [], canList: true, reason: null, embeddingOnly: [] })

    renderPanel(<InstanceDetailPanel instance={{ ...INSTANCE, isDefault: true }} onRemoved={() => {}} />)

    expect(screen.queryByRole('button', { name: /make default/i })).not.toBeInTheDocument()
  })

  it('offers the models the server has, and will not offer one that cannot chat', async () => {
    mockApi({
      models: ['mistral:7b-instruct', 'qwen2.5:0.5b', 'nomic-embed-text:latest'],
      canList: true,
      reason: null,
      embeddingOnly: ['nomic-embed-text:latest'],
    })

    renderPanel(<InstanceDetailPanel instance={INSTANCE} onRemoved={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: /swap model/i }))

    const chat = await screen.findByRole('option', { name: /qwen2\.5:0\.5b/ })
    expect(chat).not.toBeDisabled()

    // Selectable would mean choosable, and choosing it leaves an instance that cannot answer.
    const embedding = await screen.findByRole('option', { name: /nomic-embed-text/ })
    expect(embedding).toBeDisabled()
  })

  it('falls back to typing when the server cannot list its models', async () => {
    // A vLLM serves the model it was started with and cannot enumerate others; a picker with
    // nothing in it would be a dead end.
    mockApi({
      models: ['meta-llama/Llama-3.1-8B-Instruct'],
      canList: false,
      reason: 'A Vllm server serves the model it was started with; it cannot list others.',
      embeddingOnly: [],
    })

    renderPanel(<InstanceDetailPanel instance={INSTANCE} onRemoved={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: /swap model/i }))

    await waitFor(() =>
      expect(screen.getByPlaceholderText('Model ID')).toBeInTheDocument()
    )
  })
})
