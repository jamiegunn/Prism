export function getTokenColor(logprob: number): string {
  if (logprob >= -0.1) return 'text-emerald-400'
  if (logprob >= -0.5) return 'text-emerald-300'
  if (logprob >= -1.0) return 'text-yellow-400'
  if (logprob >= -2.0) return 'text-orange-400'
  if (logprob >= -3.0) return 'text-red-400'
  return 'text-red-600'
}

export function getTokenBgColor(logprob: number): string {
  if (logprob >= -0.1) return 'bg-emerald-400/20'
  if (logprob >= -0.5) return 'bg-emerald-300/15'
  if (logprob >= -1.0) return 'bg-yellow-400/15'
  if (logprob >= -2.0) return 'bg-orange-400/15'
  if (logprob >= -3.0) return 'bg-red-400/15'
  return 'bg-red-600/20'
}

export function calculatePerplexity(logprobs: number[]): number {
  if (logprobs.length === 0) return 0
  const avgNegLogprob = -logprobs.reduce((sum, lp) => sum + lp, 0) / logprobs.length
  return Math.exp(avgNegLogprob)
}

export function calculateEntropy(probs: number[]): number {
  return -probs
    .filter(p => p > 0)
    .reduce((sum, p) => sum + p * Math.log2(p), 0)
}

export function isSurpriseToken(prob: number, threshold: number = 0.1): boolean {
  return prob < threshold
}

export function logprobToProb(logprob: number): number {
  return Math.exp(logprob)
}
