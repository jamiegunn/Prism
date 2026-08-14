import { describe, it, expect } from 'vitest'
import { deriveVariables } from './variables'
import type { PromptVariable } from './types'

/*
 * Templates written in Prism used to arrive with no variables at all.
 *
 * The new-template dialog and the new-version form both sent the body and nothing else, so a
 * prompt containing {{language}} was stored with an empty variable list. The test panel then had
 * no fields to fill and running it failed with "Undeclared variables in template" — a prompt that
 * could be written and never used.
 */

function variable(name: string, over: Partial<PromptVariable> = {}): PromptVariable {
  return { name, type: 'string', defaultValue: null, description: null, required: true, ...over }
}

describe('deriveVariables', () => {
  it('declares one variable per placeholder', () => {
    expect(deriveVariables('Review this {{language}} code:\n{{code}}')).toEqual([
      variable('language'),
      variable('code'),
    ])
  })

  it('declares a repeated placeholder once, at its first appearance', () => {
    const derived = deriveVariables('{{language}} code:\n```{{language}}\n{{code}}\n```')

    expect(derived.map((v) => v.name)).toEqual(['language', 'code'])
  })

  it('finds nothing in a template with no placeholders', () => {
    expect(deriveVariables('Summarise the conversation so far.')).toEqual([])
  })

  it('keeps the definition an earlier version gave a variable', () => {
    // The point of carrying these forward: editing the body must not silently reset a type and
    // default someone set deliberately.
    const existing = [
      variable('language', { defaultValue: 'python', description: 'The language', type: 'string' }),
    ]

    const derived = deriveVariables('{{language}} and {{code}}', existing)

    expect(derived[0]).toEqual(existing[0])
    expect(derived[1]).toEqual(variable('code'))
  })

  it('drops a carried variable the template no longer uses', () => {
    const derived = deriveVariables('just {{code}} now', [variable('language'), variable('code')])

    expect(derived.map((v) => v.name)).toEqual(['code'])
  })

  it('ignores things that only look like placeholders', () => {
    // Single braces, spaces inside, and hyphens are not the server's syntax either.
    const derived = deriveVariables('{nope} {{ spaced }} {{kebab-case}} {{ok}}')

    expect(derived.map((v) => v.name)).toEqual(['ok'])
  })
})
