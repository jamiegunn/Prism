import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import { MODELS_KEY } from './api'

/** A provider found running on the machine hosting the API. */
export interface DiscoveredProvider {
  providerType: string
  endpoint: string
  suggestedName: string
  models: string[]
  supportsLogprobs: boolean
  alreadyRegistered: boolean
  note: string
}

interface DiscoveryResult {
  found: DiscoveredProvider[]
  probed: string[]
}

/**
 * Looks for inference servers on the conventional local ports.
 *
 * Probing happens on the API rather than in the browser: a page served by Vite cannot reach
 * `localhost:11434` — the cross-origin request is blocked before any answer comes back.
 */
export function useProviderDiscovery(enabled: boolean) {
  return useQuery({
    queryKey: [...MODELS_KEY, 'discover'],
    queryFn: () => apiClient<DiscoveryResult>('/models/instances/discover'),
    enabled,
    staleTime: 0,
    retry: false,
  })
}

/**
 * The empty state for a researcher who has just started Prism.
 *
 * The previous first run was an empty list and nothing else: no indication that a provider was
 * needed, which port it should be on, or which of four provider types to choose. Someone who
 * has Ollama running should not have to know any of that.
 *
 * It also states up front what each provider can and cannot do, because the difference decides
 * whether the feature they came for works at all — Ollama chats happily and produces no token
 * probabilities, which means an empty heatmap and no explanation.
 */
export function FirstRunSetup() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError, refetch, isFetching } = useProviderDiscovery(true)

  const register = useMutation({
    mutationFn: (provider: DiscoveredProvider) =>
      apiClient('/models/instances', {
        method: 'POST',
        body: {
          name: provider.suggestedName,
          endpoint: provider.endpoint,
          providerType: provider.providerType,
          modelId: provider.models[0] ?? null,
        },
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: MODELS_KEY }),
  })

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-8">
      <div className="space-y-2">
        <h1 className="text-xl font-semibold text-zinc-100">Connect a model to get started</h1>
        <p className="text-sm text-zinc-400">
          Prism reads the internals of a model as it generates — which tokens it was confident
          about, what it nearly said instead. It needs an inference server to read.
        </p>
      </div>

      {isLoading && <p className="text-sm text-zinc-400">Looking for local providers…</p>}

      {isError && (
        <div className="rounded border border-red-900 bg-red-950/40 p-4 text-sm text-red-200">
          Could not reach the Prism API to search for providers. Is the backend running?
        </div>
      )}

      {data && data.found.length === 0 && (
        <div className="space-y-3 rounded border border-zinc-800 bg-zinc-900 p-4">
          <p className="text-sm text-zinc-300">
            Nothing is listening on the usual ports
            {' '}({data.probed.join(', ')}).
          </p>
          <p className="text-sm text-zinc-400">
            The quickest way to get running: install{' '}
            <a className="text-violet-400 underline" href="https://ollama.com/download">Ollama</a>,
            then <code className="rounded bg-zinc-800 px-1">ollama serve</code> and{' '}
            <code className="rounded bg-zinc-800 px-1">ollama pull mistral:7b-instruct</code>.
            For the token-level views, run vLLM instead — it is the only local option that
            returns per-token probabilities.
          </p>
        </div>
      )}

      {data && data.found.length > 0 && (
        <ul className="space-y-3">
          {data.found.map((provider) => (
            <li
              key={provider.endpoint}
              className="space-y-3 rounded border border-zinc-800 bg-zinc-900 p-4"
            >
              <div className="flex items-start justify-between gap-4">
                <div className="space-y-1">
                  <p className="text-sm font-medium text-zinc-100">
                    {provider.suggestedName}
                    {provider.supportsLogprobs ? (
                      <span className="ml-2 rounded bg-emerald-900/60 px-1.5 py-0.5 text-[10px] text-emerald-300">
                        Full introspection
                      </span>
                    ) : (
                      <span className="ml-2 rounded bg-amber-900/60 px-1.5 py-0.5 text-[10px] text-amber-300">
                        Chat only
                      </span>
                    )}
                  </p>
                  <p className="text-xs text-zinc-500">{provider.endpoint}</p>
                  {provider.models.length > 0 && (
                    <p className="text-xs text-zinc-400">Model: {provider.models[0]}</p>
                  )}
                </div>

                <button
                  type="button"
                  disabled={provider.alreadyRegistered || register.isPending}
                  onClick={() => register.mutate(provider)}
                  className="shrink-0 rounded bg-violet-600 px-3 py-1.5 text-xs font-medium text-white disabled:cursor-not-allowed disabled:bg-zinc-700 disabled:text-zinc-400"
                >
                  {provider.alreadyRegistered ? 'Already added' : 'Use this'}
                </button>
              </div>

              <p className="text-xs leading-relaxed text-zinc-400">{provider.note}</p>
            </li>
          ))}
        </ul>
      )}

      <button
        type="button"
        onClick={() => void refetch()}
        disabled={isFetching}
        className="text-xs text-zinc-400 underline disabled:opacity-50"
      >
        {isFetching ? 'Searching…' : 'Search again'}
      </button>
    </div>
  )
}
