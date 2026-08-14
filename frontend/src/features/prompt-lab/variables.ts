import type { PromptVariable } from './types'

/** Matches `{{name}}`, the same shape the server's renderer recognises. */
const PLACEHOLDER = /\{\{(\w+)\}\}/g

/**
 * Works out which variables a template declares, from the placeholders it uses.
 *
 * Writing `{{language}}` in a prompt *is* the declaration — there is nowhere else in the UI to
 * make one. Neither the new-template dialog nor the new-version form sent any, so every template
 * written here arrived with an empty variable list: the test panel had no fields to fill, and
 * running one failed with "Undeclared variables in template". A parameterised prompt could be
 * written and then never used, which is most of the point of the page.
 *
 * Definitions already carried by an earlier version win, so a type, default or description set
 * before is kept rather than reset to a bare required string on the next edit.
 *
 * @param userTemplate The template body.
 * @param existing Variables from the version being edited, if any.
 * @returns One variable per distinct placeholder, in order of first appearance.
 */
export function deriveVariables(
  userTemplate: string,
  existing: PromptVariable[] = []
): PromptVariable[] {
  const byName = new Map(existing.map((v) => [v.name, v]))
  const seen = new Set<string>()
  const derived: PromptVariable[] = []

  for (const match of userTemplate.matchAll(PLACEHOLDER)) {
    const name = match[1]

    if (seen.has(name)) {
      continue
    }

    seen.add(name)

    derived.push(
      byName.get(name) ?? {
        name,
        type: 'string',
        defaultValue: null,
        description: null,
        // Required, because a placeholder with nothing to put in it renders as itself and gets
        // sent to the model literally — a silent wrong answer rather than a stopped one.
        required: true,
      }
    )
  }

  return derived
}
