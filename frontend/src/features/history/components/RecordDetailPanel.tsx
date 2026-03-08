import { useState } from 'react'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/sheet'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { Copy, Play, CheckCircle, XCircle, Clock } from 'lucide-react'
import { useHistoryRecord } from '../api'
import { TagEditor } from './TagEditor'
import { ReplayDialog } from './ReplayDialog'
import { getModuleBadgeColor, formatTimestamp } from '../utils'

interface RecordDetailPanelProps {
  recordId: string | null
  open: boolean
  onClose: () => void
}

export function RecordDetailPanel({ recordId, open, onClose }: RecordDetailPanelProps) {
  const { data: record, isLoading } = useHistoryRecord(recordId)
  const [replayOpen, setReplayOpen] = useState(false)

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
  }

  const formatJson = (json: string): string => {
    try {
      return JSON.stringify(JSON.parse(json), null, 2)
    } catch {
      return json
    }
  }

  return (
    <>
      <Sheet open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
        <SheetContent side="right" className="w-full max-w-none sm:max-w-2xl lg:max-w-4xl overflow-hidden p-0">
          {isLoading && (
            <div className="flex items-center justify-center h-full text-zinc-500">
              Loading record...
            </div>
          )}

          {record && (
            <div className="flex flex-col h-full">
              {/* Header */}
              <SheetHeader className="px-6 pt-6 pb-4 border-b border-zinc-800">
                <div className="flex items-center gap-3 pr-8">
                  {record.isSuccess ? (
                    <CheckCircle className="h-5 w-5 text-emerald-500 flex-shrink-0" />
                  ) : (
                    <XCircle className="h-5 w-5 text-red-500 flex-shrink-0" />
                  )}
                  <SheetTitle className="text-base truncate">
                    Record {record.id.slice(0, 8)}...
                  </SheetTitle>
                  <Badge className={getModuleBadgeColor(record.sourceModule)}>
                    {record.sourceModule}
                  </Badge>
                </div>
                <div className="flex items-center gap-2 text-xs text-zinc-500 mt-1">
                  <Clock className="h-3 w-3" />
                  {formatTimestamp(record.startedAt)}
                </div>
              </SheetHeader>

              <ScrollArea className="flex-1 px-6 py-4">
                {/* Metrics Row */}
                <div className="grid grid-cols-3 sm:grid-cols-6 gap-3 mb-4">
                  <MetricCard label="Prompt Tokens" value={String(record.promptTokens)} />
                  <MetricCard label="Completion Tokens" value={String(record.completionTokens)} />
                  <MetricCard label="Total Tokens" value={String(record.totalTokens)} />
                  <MetricCard label="Latency" value={`${record.latencyMs}ms`} />
                  <MetricCard
                    label="TTFT"
                    value={record.ttftMs !== null ? `${record.ttftMs}ms` : '--'}
                  />
                  <MetricCard
                    label="Perplexity"
                    value={record.perplexity !== null ? record.perplexity.toFixed(2) : '--'}
                  />
                </div>

                <Separator className="my-4" />

                {/* Environment */}
                <div className="mb-4">
                  <h3 className="text-sm font-medium text-zinc-300 mb-2">Environment</h3>
                  <div className="grid grid-cols-3 gap-3 text-xs">
                    <div>
                      <span className="text-zinc-500">Provider</span>
                      <p className="text-zinc-200 mt-0.5">{record.providerName}</p>
                    </div>
                    <div>
                      <span className="text-zinc-500">Model</span>
                      <p className="text-zinc-200 mt-0.5 truncate" title={record.model}>
                        {record.model}
                      </p>
                    </div>
                    <div>
                      <span className="text-zinc-500">Source</span>
                      <p className="text-zinc-200 mt-0.5">{record.sourceModule}</p>
                    </div>
                  </div>
                  {record.environmentJson && (
                    <details className="mt-2">
                      <summary className="text-xs text-zinc-500 cursor-pointer hover:text-zinc-400">
                        Full environment JSON
                      </summary>
                      <pre className="mt-1 rounded bg-zinc-900 p-3 text-xs font-mono text-zinc-300 overflow-x-auto max-h-40">
                        {formatJson(record.environmentJson)}
                      </pre>
                    </details>
                  )}
                </div>

                <Separator className="my-4" />

                {/* Request / Response Tabs */}
                <Tabs defaultValue="request" className="mb-4">
                  <TabsList>
                    <TabsTrigger value="request">Request</TabsTrigger>
                    <TabsTrigger value="response">Response</TabsTrigger>
                  </TabsList>
                  <TabsContent value="request">
                    <div className="relative">
                      <Button
                        variant="ghost"
                        size="sm"
                        className="absolute top-2 right-2 h-7 w-7 p-0 text-zinc-500 hover:text-zinc-200"
                        onClick={() => copyToClipboard(record.requestJson)}
                      >
                        <Copy className="h-3.5 w-3.5" />
                      </Button>
                      <pre className="rounded bg-zinc-900 p-4 text-xs font-mono text-zinc-300 overflow-x-auto max-h-[400px] overflow-y-auto">
                        {formatJson(record.requestJson)}
                      </pre>
                    </div>
                  </TabsContent>
                  <TabsContent value="response">
                    <div className="relative">
                      <Button
                        variant="ghost"
                        size="sm"
                        className="absolute top-2 right-2 h-7 w-7 p-0 text-zinc-500 hover:text-zinc-200"
                        onClick={() => copyToClipboard(record.responseJson)}
                      >
                        <Copy className="h-3.5 w-3.5" />
                      </Button>
                      <pre className="rounded bg-zinc-900 p-4 text-xs font-mono text-zinc-300 overflow-x-auto max-h-[400px] overflow-y-auto">
                        {formatJson(record.responseJson)}
                      </pre>
                    </div>
                  </TabsContent>
                </Tabs>

                <Separator className="my-4" />

                {/* Tags */}
                <div className="mb-4">
                  <h3 className="text-sm font-medium text-zinc-300 mb-2">Tags</h3>
                  <TagEditor recordId={record.id} tags={record.tags} />
                </div>

                <Separator className="my-4" />

                {/* Actions */}
                <div className="flex gap-2 mb-6">
                  <Button
                    size="sm"
                    className="bg-violet-600 hover:bg-violet-700 text-white"
                    onClick={() => setReplayOpen(true)}
                  >
                    <Play className="h-3.5 w-3.5 mr-1.5" />
                    Replay
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => copyToClipboard(record.requestJson)}
                  >
                    <Copy className="h-3.5 w-3.5 mr-1.5" />
                    Copy Request
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => copyToClipboard(record.responseJson)}
                  >
                    <Copy className="h-3.5 w-3.5 mr-1.5" />
                    Copy Response
                  </Button>
                </div>
              </ScrollArea>
            </div>
          )}
        </SheetContent>
      </Sheet>

      {record && (
        <ReplayDialog
          record={record}
          open={replayOpen}
          onClose={() => setReplayOpen(false)}
        />
      )}
    </>
  )
}

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border border-zinc-800 bg-zinc-900/50 px-3 py-2">
      <div className="text-[10px] uppercase tracking-wider text-zinc-500">{label}</div>
      <div className="text-sm font-mono text-zinc-200 mt-0.5">{value}</div>
    </div>
  )
}
