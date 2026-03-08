import { useMutation } from '@tanstack/react-query'
import { apiClient } from '@/services/apiClient'
import type {
  PredictRequest,
  StepRequest,
  BranchRequest,
  NextTokenPrediction,
  StepThroughResult,
  BranchExploration,
  TokenizeResult,
  CompareTokenizeResult,
} from './types'

export async function predictNextToken(
  params: PredictRequest
): Promise<NextTokenPrediction> {
  return apiClient<NextTokenPrediction>('/token-explorer/predict', {
    method: 'POST',
    body: params,
  })
}

export async function stepThrough(
  params: StepRequest
): Promise<StepThroughResult> {
  return apiClient<StepThroughResult>('/token-explorer/step', {
    method: 'POST',
    body: params,
  })
}

export async function exploreBranch(
  params: BranchRequest
): Promise<BranchExploration> {
  return apiClient<BranchExploration>('/token-explorer/branch', {
    method: 'POST',
    body: params,
  })
}

export function usePredictNextToken() {
  return useMutation({
    mutationFn: predictNextToken,
  })
}

export function useStepThrough() {
  return useMutation({
    mutationFn: stepThrough,
  })
}

export function useExploreBranch() {
  return useMutation({
    mutationFn: exploreBranch,
  })
}

export async function tokenizeText(
  params: { instanceId: string; text: string }
): Promise<TokenizeResult> {
  return apiClient<TokenizeResult>('/token-explorer/tokenize', {
    method: 'POST',
    body: params,
  })
}

export async function compareTokenize(
  params: { instanceIds: string[]; text: string }
): Promise<CompareTokenizeResult> {
  return apiClient<CompareTokenizeResult>('/token-explorer/tokenize/compare', {
    method: 'POST',
    body: params,
  })
}

export function useTokenize() {
  return useMutation({
    mutationFn: tokenizeText,
  })
}

export function useCompareTokenize() {
  return useMutation({
    mutationFn: compareTokenize,
  })
}

export interface DetokenizeRequest {
  instanceId: string
  tokenIds: number[]
}

export interface DetokenizeResult {
  text: string
  tokenIds: number[]
  modelId: string
}

export async function detokenizeTokens(
  params: DetokenizeRequest
): Promise<DetokenizeResult> {
  return apiClient<DetokenizeResult>('/token-explorer/detokenize', {
    method: 'POST',
    body: params,
  })
}

export function useDetokenize() {
  return useMutation({
    mutationFn: detokenizeTokens,
  })
}
