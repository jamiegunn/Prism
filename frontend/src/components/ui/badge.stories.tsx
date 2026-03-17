import type { Meta, StoryObj } from '@storybook/react'
import { Badge } from './badge'

const meta: Meta<typeof Badge> = {
  title: 'UI/Badge',
  component: Badge,
  parameters: { layout: 'centered' },
}

export default meta
type Story = StoryObj<typeof Badge>

export const Default: Story = { args: { children: 'Default' } }
export const Secondary: Story = { args: { children: 'Secondary', variant: 'secondary' } }
export const Outline: Story = { args: { children: 'Outline', variant: 'outline' } }
export const Destructive: Story = { args: { children: 'Destructive', variant: 'destructive' } }
