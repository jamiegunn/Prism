import type { Meta, StoryObj } from '@storybook/react'
import { PerplexityBadge } from './PerplexityBadge'

const meta: Meta<typeof PerplexityBadge> = {
  title: 'Logprobs/PerplexityBadge',
  component: PerplexityBadge,
  parameters: { layout: 'centered' },
}

export default meta
type Story = StoryObj<typeof PerplexityBadge>

export const LowPerplexity: Story = { args: { perplexity: 1.5 } }
export const MediumPerplexity: Story = { args: { perplexity: 4.2 } }
export const HighPerplexity: Story = { args: { perplexity: 8.7 } }
