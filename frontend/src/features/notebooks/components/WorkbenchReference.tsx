import { toast } from 'sonner'
import { Copy } from 'lucide-react'
import { Button } from '@/components/ui/button'

/**
 * The workbench calls available inside JupyterLite. This list mirrors
 * frontend/jupyterlite/workbench.py — if a function is added there, add it here; the
 * anchors test pins the two lists against each other via WORKBENCH_FUNCTIONS.
 */
export const WORKBENCH_FUNCTIONS: { signature: string; description: string }[] = [
  {
    signature: 'await workbench.chat(instance_id, model, prompt, **kwargs)',
    description: 'Chat with a model; the model argument is sent and honoured.',
  },
  {
    signature: 'await workbench.logprobs(instance_id, model, prompt, top_logprobs=5)',
    description: 'Per-token logprobs for a one-token completion.',
  },
  {
    signature: 'await workbench.export_history(source_module=None, model=None, tags=None, ...)',
    description: 'Full history rows (not previews) with the History page filters; unmeasured metrics are None, never 0.',
  },
  {
    signature: 'await workbench.history_dataframe(**filters)',
    description: 'The same export as a pandas DataFrame with UTC timestamps.',
  },
  {
    signature: 'await workbench.get_experiment(experiment_id)',
    description: 'One experiment by id.',
  },
  {
    signature: 'await workbench.get_dataset(dataset_id)',
    description: 'Dataset metadata by id.',
  },
  {
    signature: 'await workbench.get_dataset_records(dataset_id, page=1, page_size=100)',
    description: 'Paged dataset records.',
  },
  {
    signature: 'await workbench.list_models()',
    description: 'All registered inference instances.',
  },
  {
    signature: 'await workbench.list_collections()',
    description: 'All RAG collections.',
  },
  {
    signature: 'await workbench.rag_query(collection_id, query, top_k=5, search_type="Hybrid")',
    description: 'Search a RAG collection.',
  },
]

/**
 * The starter snippet. It discovers its own ids (list_models) and pulls history without
 * needing any, so it runs as pasted — no placeholder ids to hand-edit.
 */
export const STARTER_SNIPPET = `import micropip
await micropip.install("pandas")   # once per session; pandas ships with Pyodide's CDN

import workbench

# What is registered? (no ids needed)
instances = await workbench.list_models()
print(instances)

# Every recorded inference call as a DataFrame — full rows, not previews.
df = await workbench.history_dataframe()
print(df.dtypes)
df.head()`

/**
 * The workbench API reference shown on the Notebooks page: every call the shipped
 * workbench.py exposes, and a copyable starter snippet that runs as pasted.
 */
export function WorkbenchReference() {
  const copy = (text: string, what: string) => {
    void navigator.clipboard.writeText(text)
    toast.success(`${what} copied.`)
  }

  return (
    <div className="space-y-4" data-tour="notebooks-workbench">
      <div>
        <h2 className="text-lg font-semibold text-zinc-100">The workbench module</h2>
        <p className="text-sm text-zinc-400 mt-1">
          Every JupyterLite notebook here can <code className="px-1 rounded bg-zinc-800 text-violet-400">import workbench</code> and
          call the Prism API directly. All calls are async — use <code className="px-1 rounded bg-zinc-800">await</code>.
        </p>
      </div>

      <div className="rounded-lg border border-zinc-700 bg-zinc-900/40">
        <div className="flex items-center justify-between px-4 py-2 border-b border-zinc-800">
          <span className="text-xs font-medium text-zinc-400">
            Starter snippet — runs as pasted, no ids to fill in
          </span>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 text-xs"
            onClick={() => copy(STARTER_SNIPPET, 'Snippet')}
          >
            <Copy className="h-3 w-3 mr-1" />
            Copy
          </Button>
        </div>
        <pre className="p-4 text-xs font-mono text-zinc-300 overflow-x-auto">{STARTER_SNIPPET}</pre>
      </div>

      <div className="rounded-lg border border-zinc-700 overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-zinc-800/60 text-xs text-zinc-400">
              <th className="px-4 py-2 text-left font-medium">Call</th>
              <th className="px-4 py-2 text-left font-medium w-2/5">What it does</th>
              <th className="px-4 py-2 w-10" />
            </tr>
          </thead>
          <tbody>
            {WORKBENCH_FUNCTIONS.map((fn) => (
              <tr key={fn.signature} className="border-t border-zinc-800">
                <td className="px-4 py-2 font-mono text-xs text-zinc-200">{fn.signature}</td>
                <td className="px-4 py-2 text-xs text-zinc-400">{fn.description}</td>
                <td className="px-2 py-2">
                  <button
                    className="text-zinc-500 hover:text-zinc-200"
                    title="Copy call"
                    aria-label={`Copy ${fn.signature}`}
                    onClick={() => copy(fn.signature.replace(/^await /, ''), 'Call')}
                  >
                    <Copy className="h-3 w-3" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
