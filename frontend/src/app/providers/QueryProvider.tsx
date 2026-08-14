import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ApiError } from '@/services/apiClient'
import { describeMutationError } from '@/services/mutationErrors'

/**
 * A failed write says so.
 *
 * Of the mutations in this app, only a handful passed their own `onError`; the rest resolved
 * into nothing when they failed. Deleting a collection, saving a tag, starting a run — each
 * looked identical whether it worked or not, which is worse than an error message because the
 * reader carries on believing the write happened.
 *
 * A mutation that already handles its own errors is left alone rather than toasting twice: the
 * cache callback can see whether the mutation defined `onError`, so this is a net underneath
 * the ones that did not, not a second opinion on the ones that did.
 */
const queryClient = new QueryClient({
  mutationCache: new MutationCache({
    onError: (error, _variables, _context, mutation) => {
      if (mutation.options.onError) return

      toast.error(describeMutationError(error))
    },
  }),
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000,

      // A 404 does not become a 200 by being asked twice. Retrying client errors only delays the
      // screen that has to be shown anyway — most visibly when a remembered id no longer exists,
      // where the page sat on a spinner before it could recover. Server and network failures are
      // still worth one more try, because those do come back.
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
          return false
        }

        return failureCount < 1
      },
    },
  },
})

interface QueryProviderProps {
  children: React.ReactNode
}

export function QueryProvider({ children }: QueryProviderProps) {
  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  )
}
