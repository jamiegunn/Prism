import type { TokenPredictionEntry } from './types'

/**
 * Reshapes a returned distribution the way temperature would.
 *
 * A server returns the model's own probabilities — the distribution at temperature 1 — and does
 * not vary them with the temperature asked for: the same request at 0, 1 and 2 comes back
 * identical, because temperature governs which token the server *samples*, not what the model
 * computed. This page never uses the server's sampled token; it reads the distribution and steps
 * by taking the most likely entry. So the temperature control did nothing at all here, while its
 * label promised determinism at 0 and randomness above it.
 *
 * Applying it here makes it mean something exact: how the distribution the model produced would
 * look at another temperature. What it deliberately does not do is change which token Step takes
 * — scaling every logprob by the same factor cannot reorder them, so the most likely token is
 * the most likely token at every temperature.
 *
 * @param predictions Alternatives as returned, highest first.
 * @param temperature The temperature to view the distribution at. 1 returns it unchanged.
 * @returns The alternatives with probabilities reshaped, in the same order.
 */
export function applyTemperature(
  predictions: TokenPredictionEntry[],
  temperature: number
): TokenPredictionEntry[] {
  if (predictions.length === 0 || temperature === 1) {
    return predictions
  }

  const coveredMass = predictions.reduce((sum, p) => sum + p.probability, 0)

  // The zero limit is a point mass on the most likely token: greedy decoding, drawn.
  if (temperature <= 0) {
    return predictions.map((p, index) => ({
      ...p,
      probability: index === 0 ? coveredMass : 0,
      logprob: index === 0 ? Math.log(coveredMass) : -Infinity,
    }))
  }

  // Softmax over logprob/T. Shifting by the maximum first is what keeps exp() from overflowing
  // at small temperatures, and cancels out of the ratio.
  const scaled = predictions.map((p) => p.logprob / temperature)
  const max = Math.max(...scaled)
  const weights = scaled.map((s) => Math.exp(s - max))
  const total = weights.reduce((sum, w) => sum + w, 0)

  // Renormalised to the mass that was returned rather than to 1: the alternatives are the top of
  // a larger distribution, and rescaling them to sum to 1 would erase the fact that a tail was
  // never returned — which is what the panel's caveats are built on.
  return predictions.map((p, index) => {
    const probability = (weights[index] / total) * coveredMass

    return {
      ...p,
      probability,
      logprob: probability > 0 ? Math.log(probability) : -Infinity,
    }
  })
}
