import { useState } from 'react'
import { Separator } from '@/components/ui/separator'
import { CreateTemplateDialog } from './components/CreateTemplateDialog'
import { TemplateList } from './components/TemplateList'
import { TemplateEditor } from './components/TemplateEditor'

export function PromptLabPage() {
  const [search, setSearch] = useState('')

  return (
    <div className="flex flex-col h-[calc(100vh-3.5rem)]">
      {/* Top bar */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-border">
        <div>
          <h1 className="text-xl font-bold tracking-tight">Prompt Lab</h1>
          <p className="text-xs text-muted-foreground">
            Design, version, and test prompts with variables and few-shot examples.
          </p>
        </div>
        <CreateTemplateDialog />
      </div>

      {/* Main content: sidebar + editor */}
      <div className="flex flex-1 overflow-hidden">
        {/* Template sidebar */}
        <div className="w-72 shrink-0 border-r border-border">
          <TemplateList search={search} onSearchChange={setSearch} />
        </div>

        <Separator orientation="vertical" />

        {/* Editor area */}
        <div className="flex-1 min-w-0">
          <TemplateEditor />
        </div>
      </div>
    </div>
  )
}
