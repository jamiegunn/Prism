import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { TemplateEditor } from './TemplateEditor'
import { usePromptLabStore } from '../store'

/**
 * Prompt Lab remembers which template you had open, and the id outlives the template.
 *
 * After a reinstall or a reset database the remembered id names something that no longer exists,
 * and the page opened on "Template not found" — with a full list of templates sitting beside it.
 * Nothing was wrong except one stale pointer, but the screen said the feature was broken, and
 * the state persisted across reloads because that is what persisting a selection means.
 */

function renderWithQuery(ui: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

/** Answers every request with 404, the way the API does for a template that is gone. */
function mockMissingTemplate() {
  vi.stubGlobal(
    'fetch',
    vi.fn().mockResolvedValue({
      ok: false,
      status: 404,
      headers: new Headers({ 'content-type': 'application/json' }),
      json: async () => ({ title: 'NotFound', detail: 'Prompt template was not found.' }),
      text: async () => '{"title":"NotFound"}',
    }),
  )
}

beforeEach(() => {
  vi.restoreAllMocks()
  usePromptLabStore.setState({ selectedTemplateId: null, selectedVersionNumber: null })
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('TemplateEditor', () => {
  it('lets go of a remembered template that no longer exists', async () => {
    mockMissingTemplate()
    usePromptLabStore.setState({ selectedTemplateId: 'a-template-that-was-deleted' })

    renderWithQuery(<TemplateEditor />)

    await waitFor(() =>
      expect(usePromptLabStore.getState().selectedTemplateId).toBeNull()
    )
  })

  it('shows the ordinary empty state rather than an error', async () => {
    mockMissingTemplate()
    usePromptLabStore.setState({ selectedTemplateId: 'a-template-that-was-deleted' })

    renderWithQuery(<TemplateEditor />)

    // "Template not found" reads as a fault; the prompt to pick one reads as a starting point.
    await waitFor(() =>
      expect(screen.getByText(/Select a template from the list/i)).toBeInTheDocument()
    )
  })
})
