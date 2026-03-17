import { CheckCircle2, AlertTriangle, XCircle, Info } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { useValidateDataset } from '../api'

interface ValidationPanelProps {
  datasetId: string
}

const SEVERITY_CONFIG = {
  error: { icon: XCircle, color: 'text-red-400', bg: 'bg-red-950/30 border-red-800/50' },
  warning: { icon: AlertTriangle, color: 'text-amber-400', bg: 'bg-amber-950/30 border-amber-800/50' },
  info: { icon: Info, color: 'text-blue-400', bg: 'bg-blue-950/30 border-blue-800/50' },
} as const

export function ValidationPanel({ datasetId }: ValidationPanelProps) {
  const { data: report, isLoading } = useValidateDataset(datasetId)

  if (isLoading) {
    return <div className="text-xs text-zinc-500 p-3">Validating...</div>
  }

  if (!report) return null

  if (report.isValid && report.issues.length === 0) {
    return (
      <div className="flex items-center gap-2 p-3 rounded border border-emerald-800/50 bg-emerald-950/30">
        <CheckCircle2 className="h-4 w-4 text-emerald-400" />
        <span className="text-sm text-emerald-300">Dataset passes all validation checks</span>
        <Badge variant="outline" className="text-[10px] text-emerald-500 border-emerald-700 ml-auto">
          {report.totalRecords} records
        </Badge>
      </div>
    )
  }

  const errorCount = report.issues.filter((i) => i.severity === 'error').length
  const warningCount = report.issues.filter((i) => i.severity === 'warning').length

  return (
    <div className="space-y-2">
      <div className="flex items-center gap-2">
        <span className="text-xs font-medium text-zinc-400">Validation Report</span>
        {errorCount > 0 && <Badge variant="outline" className="text-[10px] text-red-400 border-red-800">{errorCount} errors</Badge>}
        {warningCount > 0 && <Badge variant="outline" className="text-[10px] text-amber-400 border-amber-800">{warningCount} warnings</Badge>}
      </div>
      <div className="space-y-1">
        {report.issues.map((issue, i) => {
          const config = SEVERITY_CONFIG[issue.severity as keyof typeof SEVERITY_CONFIG] ?? SEVERITY_CONFIG.info
          const Icon = config.icon
          return (
            <div key={i} className={cn('flex items-start gap-2 px-3 py-1.5 rounded text-xs border', config.bg)}>
              <Icon className={cn('h-3.5 w-3.5 mt-0.5 shrink-0', config.color)} />
              <span className="text-zinc-300">{issue.message}</span>
            </div>
          )
        })}
      </div>
    </div>
  )
}
