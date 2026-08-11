import { useMemo, useState } from 'react'
import { toast } from 'sonner'
import { FlaskConical, Plus, Search, Trash2, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import {
  useCreateQuerySet,
  useDeleteQuerySet,
  useEvaluateRetrieval,
  useQueryCollection,
  useQuerySets,
} from '../api'
import type { ChunkSearchResult, RetrievalEvaluation } from '../types'

interface EvaluatePanelProps {
  collectionId: string
}

/**
 * The Evaluate tab: pick a labelled query set, score vector, BM25 and hybrid retrieval
 * against it, and compare the modes on rank metrics — which are comparable — rather than on
 * raw scores, which are not (hybrid scores are normalized and blended). A mode that could
 * not run says why instead of showing zeros.
 */
export function EvaluatePanel({ collectionId }: EvaluatePanelProps) {
  const { data: querySets, isLoading } = useQuerySets(collectionId)
  const [selectedSetId, setSelectedSetId] = useState<string>('')
  const [topK, setTopK] = useState(10)
  const [building, setBuilding] = useState(false)
  const [result, setResult] = useState<RetrievalEvaluation | null>(null)
  const evaluate = useEvaluateRetrieval(collectionId)
  const deleteSet = useDeleteQuerySet(collectionId)

  const activeSetId = selectedSetId || querySets?.[0]?.id || ''

  const runEvaluation = () => {
    if (!activeSetId) return
    evaluate.mutate(
      { querySetId: activeSetId, topK },
      {
        onSuccess: setResult,
        onError: (error) =>
          toast.error(error instanceof Error ? error.message : 'Evaluation failed.'),
      }
    )
  }

  if (building) {
    return (
      <QuerySetBuilder
        collectionId={collectionId}
        onClose={() => setBuilding(false)}
      />
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-[220px]">
          <label className="text-xs text-zinc-500 mb-1 block">Labelled query set</label>
          <Select
            value={activeSetId}
            onChange={(e) => setSelectedSetId(e.target.value)}
            className="h-9 text-sm"
            disabled={!querySets || querySets.length === 0}
          >
            {(querySets ?? []).map((s) => (
              <option key={s.id} value={s.id}>
                {s.name} ({s.itemCount} quer{s.itemCount === 1 ? 'y' : 'ies'})
              </option>
            ))}
          </Select>
        </div>

        <div className="w-24">
          <label className="text-xs text-zinc-500 mb-1 block">Top K</label>
          <Input
            type="number"
            min={1}
            max={50}
            value={topK}
            onChange={(e) => setTopK(Math.max(1, Number(e.target.value) || 10))}
            className="h-9 text-sm"
          />
        </div>

        <Button
          size="sm"
          className="h-9"
          disabled={!activeSetId || evaluate.isPending}
          title={!activeSetId ? 'Create a labelled query set first.' : undefined}
          onClick={runEvaluation}
        >
          <FlaskConical className="h-3.5 w-3.5 mr-1.5" />
          {evaluate.isPending ? 'Evaluating…' : 'Evaluate retrieval'}
        </Button>

        <Button variant="outline" size="sm" className="h-9" onClick={() => setBuilding(true)}>
          <Plus className="h-3.5 w-3.5 mr-1.5" />
          New query set
        </Button>

        {activeSetId && (
          <Button
            variant="ghost"
            size="sm"
            className="h-9 text-zinc-500"
            onClick={() => {
              deleteSet.mutate(activeSetId, {
                onSuccess: () => {
                  setSelectedSetId('')
                  setResult(null)
                  toast.success('Query set deleted.')
                },
              })
            }}
          >
            <Trash2 className="h-3.5 w-3.5" />
          </Button>
        )}
      </div>

      {isLoading && <p className="text-sm text-zinc-500">Loading query sets…</p>}

      {!isLoading && (querySets?.length ?? 0) === 0 && (
        <div className="rounded-lg border border-zinc-700 p-6 text-sm text-zinc-400">
          <p className="font-medium text-zinc-200 mb-1">No labelled query sets yet</p>
          <p>
            Retrieval evaluation needs ground truth: queries paired with the chunks a correct
            retrieval should return. Create a set — you search, then mark which chunks are
            relevant — and vector, BM25 and hybrid can be compared on evidence.
          </p>
        </div>
      )}

      {result && <ResultsTable result={result} />}
    </div>
  )
}

const METRIC_ORDER = ['precision@1', 'precision@3', 'precision@5', 'precision@10',
  'recall@1', 'recall@3', 'recall@5', 'recall@10', 'mrr']

function ResultsTable({ result }: { result: RetrievalEvaluation }) {
  const metricKeys = useMemo(() => {
    const keys = new Set<string>()
    result.modes.forEach((m) => Object.keys(m.metrics ?? {}).forEach((k) => keys.add(k)))
    return Array.from(keys).sort((a, b) => {
      const ia = METRIC_ORDER.indexOf(a)
      const ib = METRIC_ORDER.indexOf(b)
      if (ia !== -1 && ib !== -1) return ia - ib
      if (ia !== -1) return -1
      if (ib !== -1) return 1
      return a.localeCompare(b)
    })
  }, [result])

  const okModes = result.modes.filter((m) => m.metrics !== null)
  const failedModes = result.modes.filter((m) => m.metrics === null)

  return (
    <div className="space-y-3">
      {okModes.length > 0 && (
        <div className="rounded-lg border border-zinc-700 overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-zinc-700 bg-zinc-800/60">
                <th className="px-3 py-2 text-left font-medium text-zinc-400">Metric</th>
                {okModes.map((m) => (
                  <th key={m.mode} className="px-3 py-2 text-left font-medium text-zinc-400">
                    {m.mode} <span className="font-normal">({m.queryCount} queries)</span>
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {metricKeys.map((key) => {
                const best = Math.max(
                  ...okModes
                    .map((m) => m.metrics?.[key])
                    .filter((v): v is number => v !== undefined)
                )
                return (
                  <tr key={key} className="border-b border-zinc-800">
                    <td
                      className="px-3 py-1.5 font-mono text-xs text-zinc-300"
                      title={definitionFor(result.definitions, key)}
                    >
                      {key}
                    </td>
                    {okModes.map((m) => {
                      const value = m.metrics?.[key]
                      return (
                        <td
                          key={m.mode}
                          className={`px-3 py-1.5 font-mono text-xs ${
                            value !== undefined && value === best && okModes.length > 1
                              ? 'text-emerald-400'
                              : 'text-zinc-300'
                          }`}
                        >
                          {value === undefined ? '—' : value.toFixed(4)}
                        </td>
                      )
                    })}
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}

      {failedModes.map((m) => (
        <div key={m.mode} className="rounded-lg border border-amber-900/60 bg-amber-950/30 p-3 text-sm">
          <p className="font-medium text-amber-300">{m.mode} could not be evaluated</p>
          <p className="text-amber-200/80 mt-0.5">{m.error}</p>
        </div>
      ))}

      <p className="text-[11px] text-zinc-500 leading-relaxed">
        {result.definitions['note']} Rank metrics at depth {result.topK}; hover a metric name
        for its definition.
      </p>
    </div>
  )
}

function definitionFor(definitions: Record<string, string>, key: string): string | undefined {
  if (definitions[key]) return definitions[key]
  const family = key.replace(/@\d+$/, '@k')
  return definitions[family]
}

interface DraftItem {
  queryText: string
  relevant: { chunkId: string; preview: string }[]
}

/**
 * The labelling flow: type a query, search the collection (union of BM25 and, where
 * embeddings exist, vector — so labelling is not biased toward one mode), tick the chunks
 * that are relevant, add the item, repeat, save the set.
 */
function QuerySetBuilder({
  collectionId,
  onClose,
}: {
  collectionId: string
  onClose: () => void
}) {
  const [name, setName] = useState('')
  const [items, setItems] = useState<DraftItem[]>([])
  const [queryText, setQueryText] = useState('')
  const [candidates, setCandidates] = useState<ChunkSearchResult[]>([])
  const [checked, setChecked] = useState<Set<string>>(new Set())
  const [searchNote, setSearchNote] = useState<string | null>(null)
  const search = useQueryCollection(collectionId)
  const createSet = useCreateQuerySet(collectionId)

  const runSearch = async () => {
    if (!queryText.trim()) return
    setSearchNote(null)
    setChecked(new Set())

    // Union of both lexical and (if available) vector retrieval, deduplicated — labelling
    // only from one mode's results would bias the ground truth toward that mode.
    const merged = new Map<string, ChunkSearchResult>()
    let vectorFailed = false

    const bm25 = await search
      .mutateAsync({ queryText, topK: 10, searchType: 'bm25' })
      .catch(() => null)
    bm25?.forEach((r) => merged.set(r.chunkId, r))

    const vector = await search
      .mutateAsync({ queryText, topK: 10, searchType: 'vector' })
      .catch(() => {
        vectorFailed = true
        return null
      })
    vector?.forEach((r) => {
      if (!merged.has(r.chunkId)) merged.set(r.chunkId, r)
    })

    const results = Array.from(merged.values())
    setCandidates(results)

    if (results.length === 0) {
      setSearchNote('Nothing matched. Try different words — BM25 needs a lexical match.')
    } else if (vectorFailed) {
      setSearchNote(
        'Candidates come from BM25 only: vector search is unavailable (no embeddings or no embedding provider).'
      )
    }
  }

  const addItem = () => {
    const relevant = candidates
      .filter((c) => checked.has(c.chunkId))
      .map((c) => ({ chunkId: c.chunkId, preview: c.content.slice(0, 80) }))

    setItems((prev) => [...prev, { queryText: queryText.trim(), relevant }])
    setQueryText('')
    setCandidates([])
    setChecked(new Set())
    setSearchNote(null)
  }

  const save = () => {
    createSet.mutate(
      {
        name: name.trim(),
        items: items.map((i) => ({
          queryText: i.queryText,
          relevantChunkIds: i.relevant.map((r) => r.chunkId),
        })),
      },
      {
        onSuccess: () => {
          toast.success(`Query set "${name.trim()}" saved.`)
          onClose()
        },
        onError: (error) =>
          toast.error(error instanceof Error ? error.message : 'Could not save the query set.'),
      }
    )
  }

  const canSave = name.trim().length > 0 && items.length > 0 && !createSet.isPending

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-zinc-200">New labelled query set</h3>
        <Button variant="ghost" size="sm" onClick={onClose}>
          <X className="h-4 w-4" />
        </Button>
      </div>

      <div className="max-w-md">
        <label className="text-xs text-zinc-500 mb-1 block">Set name</label>
        <Input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Transformer questions"
          className="h-9 text-sm"
        />
      </div>

      <div className="rounded-lg border border-zinc-700 p-4 space-y-3">
        <label className="text-xs text-zinc-500 block">
          Add a query, search, then tick the chunks a correct retrieval should return
        </label>
        <div className="flex gap-2">
          <Input
            value={queryText}
            onChange={(e) => setQueryText(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void runSearch()
            }}
            placeholder="Query text…"
            className="h-9 text-sm"
          />
          <Button
            variant="outline"
            size="sm"
            className="h-9"
            disabled={!queryText.trim() || search.isPending}
            onClick={() => void runSearch()}
          >
            <Search className="h-3.5 w-3.5 mr-1.5" />
            {search.isPending ? 'Searching…' : 'Search'}
          </Button>
        </div>

        {searchNote && <p className="text-xs text-amber-400">{searchNote}</p>}

        {candidates.length > 0 && (
          <div className="space-y-1 max-h-72 overflow-y-auto">
            {candidates.map((c) => (
              <label
                key={c.chunkId}
                className="flex items-start gap-2 rounded border border-zinc-800 bg-zinc-900/40 p-2 text-xs cursor-pointer hover:bg-zinc-800/40"
              >
                <input
                  type="checkbox"
                  className="mt-0.5"
                  checked={checked.has(c.chunkId)}
                  onChange={(e) => {
                    setChecked((prev) => {
                      const next = new Set(prev)
                      if (e.target.checked) next.add(c.chunkId)
                      else next.delete(c.chunkId)
                      return next
                    })
                  }}
                />
                <span className="text-zinc-300">
                  <span className="text-zinc-500">{c.documentFilename} · </span>
                  {c.content.length > 220 ? c.content.slice(0, 220) + '…' : c.content}
                </span>
              </label>
            ))}
          </div>
        )}

        {candidates.length > 0 && (
          <Button
            size="sm"
            disabled={checked.size === 0}
            title={checked.size === 0 ? 'Tick at least one relevant chunk.' : undefined}
            onClick={addItem}
          >
            Add query with {checked.size} relevant chunk{checked.size === 1 ? '' : 's'}
          </Button>
        )}
      </div>

      {items.length > 0 && (
        <div className="space-y-1">
          <p className="text-xs text-zinc-500">{items.length} labelled quer{items.length === 1 ? 'y' : 'ies'}:</p>
          {items.map((item, i) => (
            <div
              key={i}
              className="flex items-center justify-between rounded border border-zinc-800 px-3 py-1.5 text-xs"
            >
              <span className="text-zinc-300">
                “{item.queryText}” → {item.relevant.length} relevant
              </span>
              <button
                className="text-zinc-600 hover:text-zinc-300"
                onClick={() => setItems((prev) => prev.filter((_, j) => j !== i))}
              >
                <X className="h-3 w-3" />
              </button>
            </div>
          ))}
        </div>
      )}

      <div className="flex gap-2">
        <Button
          size="sm"
          disabled={!canSave}
          title={
            !name.trim()
              ? 'Name the set first.'
              : items.length === 0
                ? 'Add at least one labelled query.'
                : undefined
          }
          onClick={save}
        >
          {createSet.isPending ? 'Saving…' : `Save query set (${items.length})`}
        </Button>
        <Button variant="outline" size="sm" onClick={onClose}>
          Cancel
        </Button>
      </div>
    </div>
  )
}
