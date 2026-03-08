import { useState } from 'react'
import { Search, Database } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useDatasets } from './api'
import { DatasetCard } from './components/DatasetCard'
import { UploadDatasetDialog } from './components/UploadDatasetDialog'

export function DatasetsPage() {
  const [search, setSearch] = useState('')
  const { data: datasets, isLoading } = useDatasets(undefined, search || undefined)

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Datasets</h1>
          <p className="text-sm text-muted-foreground">
            Upload, browse, and manage datasets for evaluation and fine-tuning
          </p>
        </div>
        <UploadDatasetDialog />
      </div>

      <div className="relative max-w-sm">
        <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Search datasets..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>

      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-36" />
          ))}
        </div>
      ) : datasets && datasets.length > 0 ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {datasets.map((ds) => (
            <DatasetCard key={ds.id} dataset={ds} />
          ))}
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Database className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-medium mb-1">No datasets yet</h3>
          <p className="text-sm text-muted-foreground mb-4">
            Upload a CSV, JSON, or JSONL file to get started
          </p>
          <UploadDatasetDialog />
        </div>
      )}
    </div>
  )
}
