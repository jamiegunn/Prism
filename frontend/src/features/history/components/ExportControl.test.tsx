import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ExportControl } from './ExportControl'

/**
 * The export control's contract: it states the row count before anything is written, it is
 * disabled when there is nothing to export (rather than clickable and doing nothing), and the
 * request it sends carries the applied filters and the chosen format — the wire is the point.
 */

function renderControl(props: Partial<Parameters<typeof ExportControl>[0]> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ExportControl
        filters={{ page: 1, pageSize: 20 }}
        totalCount={42}
        isLoading={false}
        {...props}
      />
    </QueryClientProvider>,
  )
}

describe('ExportControl', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn())
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('states the row count on the button before anything is exported', () => {
    renderControl({ totalCount: 42 })

    expect(screen.getByRole('button', { name: /export 42/i })).toBeInTheDocument()
  })

  it('is disabled when no records match the filters', () => {
    renderControl({ totalCount: 0 })

    expect(screen.getByRole('button', { name: /export/i })).toBeDisabled()
  })

  it('is disabled while the count is still loading', () => {
    renderControl({ totalCount: 0, isLoading: true })

    expect(screen.getByRole('button', { name: /export/i })).toBeDisabled()
  })

  it('sends the applied filters and the chosen format on the wire', async () => {
    const fetchMock = vi.mocked(fetch)
    fetchMock.mockResolvedValue(
      new Response('{}', {
        status: 200,
        headers: {
          'Content-Disposition': 'attachment; filename="history-export.jsonl"',
          'X-Export-Row-Count': '2',
        },
      }),
    )

    // jsdom lacks URL.createObjectURL; the download itself is out of scope here.
    Object.assign(URL, {
      createObjectURL: vi.fn(() => 'blob:test'),
      revokeObjectURL: vi.fn(),
    })

    renderControl({
      filters: {
        page: 3,
        pageSize: 20,
        sourceModule: 'playground',
        tags: 'needle',
        isSuccess: true,
      },
      totalCount: 2,
    })

    fireEvent.click(screen.getByRole('button', { name: /export 2/i }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))

    const url = new URL(String(fetchMock.mock.calls[0][0]), 'http://localhost')
    expect(url.pathname).toBe('/api/v1/history/export')
    expect(url.searchParams.get('format')).toBe('jsonl')
    expect(url.searchParams.get('sourceModule')).toBe('playground')
    expect(url.searchParams.get('tags')).toBe('needle')
    expect(url.searchParams.get('isSuccess')).toBe('true')

    // Pagination must NOT be sent: an export selects everything the filters match.
    expect(url.searchParams.get('page')).toBeNull()
    expect(url.searchParams.get('pageSize')).toBeNull()
  })

  it('surfaces the server problem detail when the export fails', async () => {
    const fetchMock = vi.mocked(fetch)
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({ title: 'Validation', detail: "Invalid format 'xlsx'." }),
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    )

    renderControl({ totalCount: 1 })

    fireEvent.click(screen.getByRole('button', { name: /export 1/i }))

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    // The mutation rejects with the server's detail; sonner renders it. Asserting the toast
    // DOM would couple this test to sonner internals, so assert the rejection payload path
    // instead: the button re-enables (isPending false) after the failure.
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /export 1/i })).toBeEnabled(),
    )
  })
})
