import { useEffect } from 'react'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { usePromptLabStore } from '../store'
import type { PromptVariable } from '../types'

interface VariablePanelProps {
  variables: PromptVariable[]
}

export function VariablePanel({ variables }: VariablePanelProps) {
  const { variableValues, setVariableValue, setVariableValues } = usePromptLabStore()

  // Initialize defaults when variables change
  useEffect(() => {
    const defaults: Record<string, string> = {}
    for (const v of variables) {
      defaults[v.name] = variableValues[v.name] ?? v.defaultValue ?? ''
    }
    setVariableValues(defaults)
  }, [variables]) // eslint-disable-line react-hooks/exhaustive-deps

  if (variables.length === 0) {
    return (
      <div className="px-4 py-3">
        <span className="text-xs font-medium text-zinc-400">Variables</span>
        <p className="text-xs text-zinc-500 mt-2">
          No variables declared. Use {'{{name}}'} in your template.
        </p>
      </div>
    )
  }

  return (
    <div className="flex flex-col">
      <div className="px-4 py-3 border-b border-border">
        <span className="text-xs font-medium text-zinc-400">
          Variables ({variables.length})
        </span>
      </div>
      <ScrollArea className="flex-1 max-h-60">
        <div className="p-3 space-y-3">
          {variables.map((variable) => (
            <div key={variable.name} className="space-y-1">
              <div className="flex items-center gap-2">
                <label className="text-xs font-medium text-zinc-300">
                  {variable.name}
                </label>
                <Badge variant="outline" className="text-[10px] py-0">
                  {variable.type}
                </Badge>
                {variable.required && (
                  <span className="text-[10px] text-red-400">required</span>
                )}
              </div>
              {variable.description && (
                <p className="text-[10px] text-zinc-500">{variable.description}</p>
              )}
              <Input
                value={variableValues[variable.name] ?? ''}
                onChange={(e) => setVariableValue(variable.name, e.target.value)}
                placeholder={variable.defaultValue ?? `Enter ${variable.name}...`}
                className="h-8 text-xs"
              />
            </div>
          ))}
        </div>
      </ScrollArea>
    </div>
  )
}
