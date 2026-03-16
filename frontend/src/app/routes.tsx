import { Routes, Route, Navigate } from 'react-router-dom'
import { PlaygroundPage } from '@/features/playground/PlaygroundPage'
import { TokenExplorerPage } from '@/features/token-explorer/TokenExplorerPage'
import { ModelsPage } from '@/features/models/ModelsPage'
import { HistoryPage } from '@/features/history/HistoryPage'
import { ExperimentsPage } from '@/features/experiments/ExperimentsPage'
import { ProjectDetailPage } from '@/features/experiments/ProjectDetailPage'
import { ExperimentDetailPage } from '@/features/experiments/ExperimentDetailPage'
import { MultiPanePlayground } from '@/features/playground/MultiPanePlayground'
import { PromptLabPage } from '@/features/prompt-lab/PromptLabPage'
import { DatasetsPage } from '@/features/datasets/DatasetsPage'
import { DatasetDetailPage } from '@/features/datasets/DatasetDetailPage'
import { EvaluationPage } from '@/features/evaluation/EvaluationPage'
import { EvaluationDetailPage } from '@/features/evaluation/EvaluationDetailPage'
import { BatchInferencePage } from '@/features/batch-inference/BatchInferencePage'
import { AnalyticsPage } from '@/features/analytics/AnalyticsPage'
import { RagWorkbenchPage } from '@/features/rag/RagWorkbenchPage'
import { RagCollectionDetailPage } from '@/features/rag/RagCollectionDetailPage'
import { StructuredOutputPage } from '@/features/structured-output/StructuredOutputPage'
import { AgentsPage } from '@/features/agents/AgentsPage'
import { AgentDetailPage } from '@/features/agents/AgentDetailPage'
import { FineTuningPage } from '@/features/fine-tuning/FineTuningPage'
import { NotebooksPage } from '@/features/notebooks/NotebooksPage'
import { NotebookDetailPage } from '@/features/notebooks/NotebookDetailPage'
import { ComingSoonPage } from '@/features/coming-soon/ComingSoonPage'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/playground" replace />} />
      <Route path="/playground" element={<PlaygroundPage />} />
      <Route path="/playground/compare" element={<MultiPanePlayground />} />
      <Route path="/token-explorer" element={<TokenExplorerPage />} />
      <Route path="/models" element={<ModelsPage />} />
      <Route path="/history" element={<HistoryPage />} />
      <Route path="/experiments" element={<ExperimentsPage />} />
      <Route path="/experiments/projects/:projectId" element={<ProjectDetailPage />} />
      <Route path="/experiments/:experimentId" element={<ExperimentDetailPage />} />
      <Route path="/prompt-lab" element={<PromptLabPage />} />
      <Route path="/datasets" element={<DatasetsPage />} />
      <Route path="/datasets/:id" element={<DatasetDetailPage />} />
      <Route path="/evaluation" element={<EvaluationPage />} />
      <Route path="/evaluation/:id" element={<EvaluationDetailPage />} />
      <Route path="/batch" element={<BatchInferencePage />} />
      <Route path="/analytics" element={<AnalyticsPage />} />
      <Route path="/rag" element={<RagWorkbenchPage />} />
      <Route path="/rag/:id" element={<RagCollectionDetailPage />} />
      <Route path="/structured-output" element={<StructuredOutputPage />} />
      <Route path="/agents" element={<AgentsPage />} />
      <Route path="/agents/:id" element={<AgentDetailPage />} />
      <Route path="/fine-tuning" element={<FineTuningPage />} />
      <Route path="/notebooks" element={<NotebooksPage />} />
      <Route path="/notebooks/:id" element={<NotebookDetailPage />} />
      <Route path="/coming-soon/:phase" element={<ComingSoonPage />} />
      <Route path="*" element={<Navigate to="/playground" replace />} />
    </Routes>
  )
}
