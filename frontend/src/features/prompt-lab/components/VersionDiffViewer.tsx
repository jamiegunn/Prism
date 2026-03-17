import { useState } from 'react'
import { GitCompare, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { useVersionDiff, useVersions } from '../api'

interface VersionDiffViewerProps {
  templateId: string
  currentVersion: number
  onClose: () => void
}

export function VersionDiffViewer({ templateId, currentVersion, onClose }: VersionDiffViewerProps) {
  const { data: versions } = useVersions(templateId)
  const [compareVersion, setCompareVersion] = useState(Math.max(1, currentVersion - 1))
  const { data: diff, isLoading } = useVersionDiff(templateId, compareVersion, currentVersion)

  const otherVersions = versions?.filter((v) => v.version !== currentVersion) ?? []

  return (
    <div className="flex flex-col h-full border-l border-zinc-800 bg-zinc-900/70">
      <div className="flex items-center justify-between px-4 py-2 border-b border-zinc-800">
        <div className="flex items-center gap-2">
          <GitCompare className="h-4 w-4 text-violet-400" />
          <span className="text-sm font-medium text-zinc-300">Version Diff</span>
        </div>
        <div className="flex items-center gap-2">
          <select
            value={compareVersion}
            onChange={(e) => setCompareVersion(Number(e.target.value))}
            className="bg-zinc-800 border border-zinc-700 rounded px-2 py-1 text-xs text-zinc-300"
          >
            {otherVersions.map((v) => (
              <option key={v.version} value={v.version}>v{v.version}</option>
            ))}
          </select>
          <span className="text-xs text-zinc-500">vs</span>
          <Badge variant="secondary" className="text-xs">v{currentVersion}</Badge>
          <Button variant="ghost" size="icon" onClick={onClose} className="h-6 w-6">
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

      {isLoading ? (
        <div className="flex-1 flex items-center justify-center text-zinc-500 text-sm">Loading diff...</div>
      ) : diff ? (
        <ScrollArea className="flex-1">
          <div className="p-4 space-y-4">
            {/* System prompt diff */}
            <DiffSection
              label="System Prompt"
              left={diff.version1.systemPrompt ?? ''}
              right={diff.version2.systemPrompt ?? ''}
            />

            {/* User template diff */}
            <DiffSection
              label="User Template"
              left={diff.version1.userTemplate}
              right={diff.version2.userTemplate}
            />

            {/* Variables diff */}
            {(diff.version1.variables.length > 0 || diff.version2.variables.length > 0) && (
              <div>
                <span className="text-xs font-medium text-zinc-400 mb-2 block">Variables</span>
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div className="space-y-1">
                    <span className="text-zinc-600">v{diff.version1.version}</span>
                    {diff.version1.variables.map((v, i) => (
                      <div key={i} className="bg-zinc-800 rounded px-2 py-1">
                        <span className="text-violet-400">{`{{${v.name}}}`}</span>
                        <span className="text-zinc-500 ml-1">({v.type})</span>
                      </div>
                    ))}
                  </div>
                  <div className="space-y-1">
                    <span className="text-zinc-600">v{diff.version2.version}</span>
                    {diff.version2.variables.map((v, i) => (
                      <div key={i} className="bg-zinc-800 rounded px-2 py-1">
                        <span className="text-violet-400">{`{{${v.name}}}`}</span>
                        <span className="text-zinc-500 ml-1">({v.type})</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}

            {/* Few-shot examples diff */}
            {(diff.version1.fewShotExamples.length > 0 || diff.version2.fewShotExamples.length > 0) && (
              <div>
                <span className="text-xs font-medium text-zinc-400 mb-2 block">Few-Shot Examples</span>
                <div className="grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <span className="text-zinc-600">v{diff.version1.version}: {diff.version1.fewShotExamples.length} examples</span>
                  </div>
                  <div>
                    <span className="text-zinc-600">v{diff.version2.version}: {diff.version2.fewShotExamples.length} examples</span>
                  </div>
                </div>
              </div>
            )}

            {/* Notes diff */}
            {(diff.version1.notes || diff.version2.notes) && (
              <DiffSection
                label="Notes"
                left={diff.version1.notes ?? ''}
                right={diff.version2.notes ?? ''}
              />
            )}
          </div>
        </ScrollArea>
      ) : (
        <div className="flex-1 flex items-center justify-center text-zinc-500 text-sm">
          Select a version to compare.
        </div>
      )}
    </div>
  )
}

function DiffSection({ label, left, right }: { label: string; left: string; right: string }) {
  const same = left === right

  if (same) {
    return (
      <div>
        <span className="text-xs font-medium text-zinc-400 mb-1 block">
          {label} <Badge variant="outline" className="text-[9px] ml-1 text-zinc-600">identical</Badge>
        </span>
      </div>
    )
  }

  const leftLines = left.split('\n')
  const rightLines = right.split('\n')
  const maxLines = Math.max(leftLines.length, rightLines.length)

  return (
    <div>
      <span className="text-xs font-medium text-zinc-400 mb-1 block">
        {label} <Badge variant="outline" className="text-[9px] ml-1 text-amber-600 border-amber-800">changed</Badge>
      </span>
      <div className="grid grid-cols-2 gap-1 text-[11px] font-mono">
        {Array.from({ length: maxLines }).map((_, i) => {
          const lLine = leftLines[i] ?? ''
          const rLine = rightLines[i] ?? ''
          const isDiff = lLine !== rLine

          return (
            <div key={i} className="contents">
              <div className={cn(
                'px-2 py-0.5 rounded-sm whitespace-pre-wrap break-all',
                isDiff ? 'bg-red-950/30 text-red-300' : 'text-zinc-500'
              )}>
                {lLine || '\u00A0'}
              </div>
              <div className={cn(
                'px-2 py-0.5 rounded-sm whitespace-pre-wrap break-all',
                isDiff ? 'bg-emerald-950/30 text-emerald-300' : 'text-zinc-500'
              )}>
                {rLine || '\u00A0'}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
