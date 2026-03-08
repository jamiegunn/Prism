import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Search, FlaskConical, Trophy } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useEvaluations, useLeaderboard } from './api'
import type { Evaluation } from './types'

const STATUS_COLORS: Record<string, string> = {
  Pending: 'bg-yellow-500/10 text-yellow-500',
  Running: 'bg-blue-500/10 text-blue-500',
  Completed: 'bg-green-500/10 text-green-500',
  Failed: 'bg-red-500/10 text-red-500',
  Cancelled: 'bg-gray-500/10 text-gray-500',
}

export function EvaluationPage() {
  const [search, setSearch] = useState('')
  const navigate = useNavigate()
  const { data: evaluations, isLoading } = useEvaluations(undefined, search || undefined)
  const { data: leaderboard } = useLeaderboard()

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">Evaluation Suite</h1>
          <p className="text-sm text-muted-foreground">
            Score model outputs against datasets with pluggable metrics
          </p>
        </div>
      </div>

      <Tabs defaultValue="evaluations">
        <TabsList>
          <TabsTrigger value="evaluations">Evaluations</TabsTrigger>
          <TabsTrigger value="leaderboard">Leaderboard</TabsTrigger>
        </TabsList>

        <TabsContent value="evaluations" className="mt-4 space-y-4">
          <div className="relative max-w-sm">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Search evaluations..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>

          {isLoading ? (
            <div className="grid gap-4 md:grid-cols-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <Skeleton key={i} className="h-32" />
              ))}
            </div>
          ) : evaluations && evaluations.length > 0 ? (
            <div className="grid gap-4 md:grid-cols-2">
              {evaluations.map((ev) => (
                <EvaluationCard key={ev.id} evaluation={ev} onClick={() => navigate(`/evaluation/${ev.id}`)} />
              ))}
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <FlaskConical className="h-12 w-12 text-muted-foreground mb-4" />
              <h3 className="text-lg font-medium mb-1">No evaluations yet</h3>
              <p className="text-sm text-muted-foreground">
                Start an evaluation from a dataset to compare models
              </p>
            </div>
          )}
        </TabsContent>

        <TabsContent value="leaderboard" className="mt-4">
          {leaderboard && leaderboard.length > 0 ? (
            <div className="rounded-lg border overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-left font-medium">#</th>
                    <th className="px-4 py-2 text-left font-medium">Model</th>
                    <th className="px-4 py-2 text-left font-medium">Evaluation</th>
                    <th className="px-4 py-2 text-left font-medium">Records</th>
                    <th className="px-4 py-2 text-left font-medium">Avg Latency</th>
                    <th className="px-4 py-2 text-left font-medium">Scores</th>
                  </tr>
                </thead>
                <tbody>
                  {leaderboard.map((entry, i) => (
                    <tr key={`${entry.evaluationId}-${entry.model}`} className="border-b hover:bg-muted/30">
                      <td className="px-4 py-2">
                        {i === 0 && <Trophy className="h-4 w-4 text-yellow-500 inline" />}
                        {i > 0 && i + 1}
                      </td>
                      <td className="px-4 py-2 font-medium">{entry.model}</td>
                      <td className="px-4 py-2 text-muted-foreground">{entry.evaluationName}</td>
                      <td className="px-4 py-2">{entry.recordCount}</td>
                      <td className="px-4 py-2">{entry.averageLatencyMs.toFixed(0)}ms</td>
                      <td className="px-4 py-2">
                        <div className="flex gap-2">
                          {Object.entries(entry.averageScores).map(([key, val]) => (
                            <Badge key={key} variant="secondary" className="text-xs">
                              {key}: {val.toFixed(3)}
                            </Badge>
                          ))}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <Trophy className="h-12 w-12 text-muted-foreground mb-4" />
              <h3 className="text-lg font-medium">No leaderboard data</h3>
              <p className="text-sm text-muted-foreground">Complete evaluations to populate the leaderboard</p>
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}

function EvaluationCard({ evaluation, onClick }: { evaluation: Evaluation; onClick: () => void }) {
  return (
    <Card className="cursor-pointer hover:border-primary/50 transition-colors" onClick={onClick}>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">{evaluation.name}</CardTitle>
          <Badge className={STATUS_COLORS[evaluation.status] ?? ''}>
            {evaluation.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex items-center gap-3 text-xs text-muted-foreground mb-2">
          <span>{evaluation.models.length} model(s)</span>
          <span>{evaluation.scoringMethods.join(', ')}</span>
        </div>
        {(evaluation.status === 'Running' || evaluation.status === 'Completed') && (
          <div className="space-y-1">
            <div className="flex justify-between text-xs">
              <span>{evaluation.completedRecords} / {evaluation.totalRecords}</span>
              <span>{(evaluation.progress * 100).toFixed(0)}%</span>
            </div>
            <div className="h-1.5 rounded-full bg-muted overflow-hidden">
              <div
                className="h-full rounded-full bg-primary transition-all"
                style={{ width: `${evaluation.progress * 100}%` }}
              />
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
