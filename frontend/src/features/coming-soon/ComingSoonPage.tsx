import { useParams } from 'react-router-dom'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Clock } from 'lucide-react'

const phaseFeatures: Record<string, string[]> = {
  '2': ['Prompt Lab', 'Experiment Tracker', 'Multi-pane Playground'],
  '3': ['Dataset Manager', 'Evaluation Suite', 'Batch Inference', 'Analytics Dashboard'],
  '4': ['RAG Workbench', 'Structured Output Designer'],
  '5': ['Agent Builder', 'JupyterLite Notebooks', 'Fine-Tuning Support'],
}

const phaseNames: Record<string, string> = {
  '2': 'Jog',
  '3': 'Run',
  '4': 'Sprint',
  '5': 'Fly',
}

export function ComingSoonPage() {
  const { phase } = useParams<{ phase: string }>()
  const features = phaseFeatures[phase ?? ''] ?? []
  const phaseName = phaseNames[phase ?? ''] ?? ''

  return (
    <div className="flex flex-1 items-center justify-center">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="mx-auto mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-zinc-800">
            <Clock className="h-8 w-8 text-violet-500" />
          </div>
          <CardTitle className="text-xl">
            Coming in Phase {phase}
            {phaseName && (
              <Badge variant="secondary" className="ml-2 text-xs">
                {phaseName}
              </Badge>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="mb-4 text-center text-sm text-muted-foreground">
            These features are planned for a future release:
          </p>
          <ul className="space-y-2">
            {features.map((feature) => (
              <li
                key={feature}
                className="flex items-center gap-2 rounded-md bg-zinc-800/50 px-3 py-2 text-sm text-zinc-300"
              >
                <span className="h-1.5 w-1.5 rounded-full bg-violet-500" />
                {feature}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  )
}
