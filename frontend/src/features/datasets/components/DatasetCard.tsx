import { useNavigate } from 'react-router-dom'
import { Database, FileText, Hash } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import type { Dataset } from '../types'

interface DatasetCardProps {
  dataset: Dataset
}

export function DatasetCard({ dataset }: DatasetCardProps) {
  const navigate = useNavigate()

  function formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  }

  return (
    <Card
      className="cursor-pointer hover:border-primary/50 transition-colors"
      onClick={() => navigate(`/datasets/${dataset.id}`)}
    >
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base flex items-center gap-2">
            <Database className="h-4 w-4 text-muted-foreground" />
            {dataset.name}
          </CardTitle>
          <Badge variant="outline">{dataset.format}</Badge>
        </div>
      </CardHeader>
      <CardContent>
        {dataset.description && (
          <p className="text-sm text-muted-foreground mb-3 line-clamp-2">{dataset.description}</p>
        )}
        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          <span className="flex items-center gap-1">
            <Hash className="h-3 w-3" />
            {dataset.recordCount.toLocaleString()} records
          </span>
          <span className="flex items-center gap-1">
            <FileText className="h-3 w-3" />
            {formatSize(dataset.sizeBytes)}
          </span>
          <span>{dataset.schema.length} columns</span>
        </div>
        {dataset.splits.length > 0 && (
          <div className="flex gap-1 mt-2">
            {dataset.splits.map((s) => (
              <Badge key={s.id} variant="secondary" className="text-xs">
                {s.name}: {s.recordCount}
              </Badge>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
