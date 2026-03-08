import { useEffect } from 'react'
import Editor from '@monaco-editor/react'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Separator } from '@/components/ui/separator'
import { ScrollArea } from '@/components/ui/scroll-area'
import { useTemplate, useVersions } from '../api'
import { usePromptLabStore } from '../store'
import { VersionSelector } from './VersionSelector'
import { VariablePanel } from './VariablePanel'
import { TestPanel } from './TestPanel'

export function TemplateEditor() {
  const { selectedTemplateId, selectedVersionNumber, setSelectedVersionNumber } =
    usePromptLabStore()
  const { data: templateData, isLoading } = useTemplate(selectedTemplateId)
  const { data: versions } = useVersions(selectedTemplateId ?? '')

  // Select latest version when template loads
  useEffect(() => {
    if (templateData?.latestVersionContent && selectedVersionNumber === null) {
      setSelectedVersionNumber(templateData.latestVersionContent.version)
    }
  }, [templateData, selectedVersionNumber, setSelectedVersionNumber])

  if (!selectedTemplateId) {
    return (
      <div className="flex items-center justify-center h-full text-zinc-500">
        <p>Select a template from the list to edit and test.</p>
      </div>
    )
  }

  if (isLoading) {
    return (
      <div className="p-6 space-y-4">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (!templateData) {
    return (
      <div className="flex items-center justify-center h-full text-zinc-500">
        <p>Template not found.</p>
      </div>
    )
  }

  const { template, latestVersionContent } = templateData
  const currentVersion =
    versions?.find((v) => v.version === selectedVersionNumber) ??
    latestVersionContent

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <div>
          <h2 className="text-lg font-medium text-zinc-100">{template.name}</h2>
          <div className="flex items-center gap-2 mt-1">
            {template.category && (
              <Badge variant="secondary" className="text-xs">
                {template.category}
              </Badge>
            )}
            {template.tags.map((tag) => (
              <Badge key={tag} variant="outline" className="text-xs">
                {tag}
              </Badge>
            ))}
          </div>
        </div>

        <VersionSelector
          versions={versions ?? []}
          currentVersion={selectedVersionNumber ?? template.latestVersion}
          onSelect={setSelectedVersionNumber}
          templateId={template.id}
        />
      </div>

      {/* Content */}
      <div className="flex-1 flex overflow-hidden">
        {/* Left: Editor */}
        <div className="flex-1 flex flex-col min-w-0">
          {/* System Prompt */}
          {currentVersion?.systemPrompt && (
            <div className="border-b border-border">
              <div className="px-4 py-2">
                <span className="text-xs font-medium text-zinc-400">System Prompt</span>
              </div>
              <div className="h-24">
                <Editor
                  height="100%"
                  defaultLanguage="plaintext"
                  value={currentVersion.systemPrompt}
                  theme="vs-dark"
                  options={{
                    readOnly: true,
                    minimap: { enabled: false },
                    lineNumbers: 'off',
                    scrollBeyondLastLine: false,
                    wordWrap: 'on',
                    fontSize: 13,
                  }}
                />
              </div>
            </div>
          )}

          {/* User Template */}
          <div className="px-4 py-2">
            <span className="text-xs font-medium text-zinc-400">User Template</span>
          </div>
          <div className="flex-1">
            <Editor
              height="100%"
              defaultLanguage="handlebars"
              value={currentVersion?.userTemplate ?? ''}
              theme="vs-dark"
              options={{
                readOnly: true,
                minimap: { enabled: false },
                scrollBeyondLastLine: false,
                wordWrap: 'on',
                fontSize: 13,
              }}
            />
          </div>

          {/* Few-shot examples */}
          {currentVersion && currentVersion.fewShotExamples.length > 0 && (
            <div className="border-t border-border">
              <div className="px-4 py-2">
                <span className="text-xs font-medium text-zinc-400">
                  Few-Shot Examples ({currentVersion.fewShotExamples.length})
                </span>
              </div>
              <ScrollArea className="max-h-40">
                <div className="px-4 pb-3 space-y-2">
                  {currentVersion.fewShotExamples.map((ex, i) => (
                    <div
                      key={i}
                      className="rounded bg-zinc-900 p-2 text-xs space-y-1"
                    >
                      {ex.label && (
                        <Badge variant="outline" className="text-[10px]">
                          {ex.label}
                        </Badge>
                      )}
                      <div>
                        <span className="text-zinc-500">Input: </span>
                        <span className="text-zinc-300">{ex.input}</span>
                      </div>
                      <div>
                        <span className="text-zinc-500">Output: </span>
                        <span className="text-zinc-300">{ex.output}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </ScrollArea>
            </div>
          )}
        </div>

        <Separator orientation="vertical" />

        {/* Right: Variables + Test */}
        <div className="w-80 shrink-0 flex flex-col border-l border-border">
          {currentVersion && (
            <>
              <VariablePanel variables={currentVersion.variables} />
              <Separator />
              <TestPanel templateId={template.id} version={currentVersion.version} />
            </>
          )}
        </div>
      </div>
    </div>
  )
}
