export interface LogprobsData {
  tokens: TokenLogprob[]
}

export interface TokenLogprob {
  token: string
  logprob: number
  probability: number
  topLogprobs: TopLogprob[]
  byteOffset?: number
}

export interface TopLogprob {
  token: string
  logprob: number
  probability: number
}
