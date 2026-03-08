import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Download, Trash2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useDataset, useDeleteDataset, useExportDataset } from './api'
import { useDatasetsStore } from './store'
import { RecordsTable } from './components/RecordsTable'
import { DatasetStatsPanel } from './components/DatasetStatsPanel'
import { SplitDatasetDialog } from './components/SplitDatasetDialog'

export function DatasetDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: dataset, isLoading } = useDataset(id ?? null)
  const deleteDataset = useDeleteDataset()
  const exportDataset = useExportDataset()
  const { splitFilter, setSplitFilter } = useDatasetsStore()

  function handleDelete() {
    if (!id || !confirm('Delete this dataset and all its records?')) return
    deleteDataset.mutate(id, { onSuccess: () => navigate('/datasets') })
  }

  function handleExport(format: string) {
    if (!id) return
    exportDataset.mutate({ id, format, splitLabel: splitFilter ?? undefined })
  }

  if (isLoading) {
    return <div className="p-6 text-muted-foreground">Loading...</div>
  }

  if (!dataset) {
    return <div className="p-6 text-muted-foreground">Dataset not found.</div>
  }

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/datasets')}>
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back
        </Button>
      </div>

      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold">{dataset.name}</h1>
          {dataset.description && (
            <p className="text-sm text-muted-foreground mt-1">{dataset.description}</p>
          )}
          <div className="flex items-center gap-3 mt-2">
            <Badge variant="outline">{dataset.format}</Badge>
            <span className="text-sm text-muted-foreground">
              {dataset.recordCount.toLocaleString()} records · {dataset.schema.length} columns · v{dataset.version}
            </span>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <SplitDatasetDialog datasetId={dataset.id} />
          <Button variant="outline" size="sm" onClick={() => handleExport('csv')}>
            <Download className="h-4 w-4 mr-1" />
            CSV
          </Button>
          <Button variant="outline" size="sm" onClick={() => handleExport('json')}>
            <Download className="h-4 w-4 mr-1" />
            JSON
          </Button>
          <Button variant="destructive" size="sm" onClick={handleDelete}>
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {dataset.splits.length > 0 && (
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium">Filter by split:</span>
          <Button
            variant={splitFilter === null ? 'default' : 'outline'}
            size="sm"
            onClick={() => setSplitFilter(null)}
          >
            All
          </Button>
          {dataset.splits.map((s) => (
            <Button
              key={s.id}
              variant={splitFilter === s.name ? 'default' : 'outline'}
              size="sm"
              onClick={() => setSplitFilter(s.name)}
            >
              {s.name} ({s.recordCount})
            </Button>
          ))}
        </div>
      )}

      <Tabs defaultValue="records">
        <TabsList>
          <TabsTrigger value="records">Records</TabsTrigger>
          <TabsTrigger value="schema">Schema</TabsTrigger>
          <TabsTrigger value="stats">Statistics</TabsTrigger>
        </TabsList>

        <TabsContent value="records" className="mt-4">
          <RecordsTable datasetId={dataset.id} schema={dataset.schema} />
        </TabsContent>

        <TabsContent value="schema" className="mt-4">
          <div className="rounded-lg border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Column</th>
                  <th className="px-4 py-2 text-left font-medium">Type</th>
                  <th className="px-4 py-2 text-left font-medium">Purpose</th>
                </tr>
              </thead>
              <tbody>
                {dataset.schema.map((col) => (
                  <tr key={col.name} className="border-b">
                    <td className="px-4 py-2 font-mono">{col.name}</td>
                    <td className="px-4 py-2">
                      <Badge variant="secondary">{col.type}</Badge>
                    </td>
                    <td className="px-4 py-2 text-muted-foreground">
                      {col.purpose ?? '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </TabsContent>

        <TabsContent value="stats" className="mt-4">
          <DatasetStatsPanel datasetId={dataset.id} />
        </TabsContent>
      </Tabs>
    </div>
  )
}
