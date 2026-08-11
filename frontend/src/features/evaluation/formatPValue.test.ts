import { describe, it, expect } from 'vitest'
import { formatPValue } from './EvaluationDetailPage'

describe('formatPValue', () => {
  it('shows ordinary p-values to three decimals', () => {
    expect(formatPValue(0.0719)).toBe('0.072')
    expect(formatPValue(0.7177)).toBe('0.718')
  })

  it('never rounds a tiny p-value to an impossible 0.000', () => {
    expect(formatPValue(0.0004)).toBe('< 0.001')
    expect(formatPValue(2.47e-13)).toBe('< 0.001')
  })

  it('keeps the boundary honest: 0.001 itself is displayable', () => {
    expect(formatPValue(0.001)).toBe('0.001')
  })

  it('renders an undefined statistic as a dash, not a number', () => {
    expect(formatPValue(null)).toBe('—')
  })
})
