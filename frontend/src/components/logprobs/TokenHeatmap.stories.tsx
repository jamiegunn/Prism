import type { Meta, StoryObj } from '@storybook/react'
import { TokenHeatmap } from './TokenHeatmap'
import type { LogprobsData } from '@/services/types/logprobs'

const sampleLogprobs: LogprobsData = {
  tokens: [
    { token: 'The', logprob: -0.05, probability: 0.951, topLogprobs: [{ token: 'The', logprob: -0.05, probability: 0.951 }], byteOffset: 0 },
    { token: ' capital', logprob: -0.12, probability: 0.887, topLogprobs: [{ token: ' capital', logprob: -0.12, probability: 0.887 }], byteOffset: 3 },
    { token: ' of', logprob: -0.03, probability: 0.970, topLogprobs: [{ token: ' of', logprob: -0.03, probability: 0.970 }], byteOffset: 11 },
    { token: ' France', logprob: -0.08, probability: 0.923, topLogprobs: [{ token: ' France', logprob: -0.08, probability: 0.923 }], byteOffset: 14 },
    { token: ' is', logprob: -0.04, probability: 0.961, topLogprobs: [{ token: ' is', logprob: -0.04, probability: 0.961 }], byteOffset: 21 },
    { token: ' Paris', logprob: -0.02, probability: 0.980, topLogprobs: [{ token: ' Paris', logprob: -0.02, probability: 0.980 }, { token: ' Lyon', logprob: -5.80, probability: 0.003 }], byteOffset: 24 },
    { token: '.', logprob: -0.15, probability: 0.861, topLogprobs: [{ token: '.', logprob: -0.15, probability: 0.861 }], byteOffset: 30 },
  ],
}

const meta: Meta<typeof TokenHeatmap> = {
  title: 'Logprobs/TokenHeatmap',
  component: TokenHeatmap,
  parameters: { layout: 'padded' },
}

export default meta
type Story = StoryObj<typeof TokenHeatmap>

export const Default: Story = {
  args: {
    logprobsData: sampleLogprobs,
  },
}

export const WithSelection: Story = {
  args: {
    logprobsData: sampleLogprobs,
    selectedIndex: 5,
  },
}
