import { readFileSync, readdirSync } from 'node:fs'
import { join } from 'node:path'
import { describe, it, expect } from 'vitest'
import { allTours } from './tours'

/*
 * Every anchor a tour names must exist in the source.
 *
 * This is the failure this whole feature is most exposed to. A tour stop whose anchor has been
 * renamed or deleted does not crash — the overlay politely degrades to a centred card — so the
 * tour goes on describing a panel while pointing at the middle of the screen, and nothing in
 * the app or the test suite notices. Fifteen tours across fifteen pages makes that a matter of
 * time rather than luck, so the check is mechanical: scan the tree for `data-tour` values and
 * compare them against what the tours ask for.
 */

const SRC = join(process.cwd(), 'src')

/** Collects every file under a directory that could carry an anchor. */
function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name)

    if (entry.isDirectory()) return sourceFiles(path)
    return entry.name.endsWith('.tsx') ? [path] : []
  })
}

/** Every `data-tour` value present in the app, including template-literal ones. */
function anchorsInSource(): Set<string> {
  const found = new Set<string>()

  for (const file of sourceFiles(SRC)) {
    const contents = readFileSync(file, 'utf8')

    for (const match of contents.matchAll(/data-tour="([^"{]+)"/g)) {
      found.add(match[1])
    }

    // The sidebar builds its anchors from the route: data-tour={`nav-${...}`}. Recover the
    // real values from the nav list rather than pretending the pattern does not exist.
    if (contents.includes('data-tour={`nav-')) {
      for (const route of contents.matchAll(/path: '\/([a-z-]+)'/g)) {
        found.add(`nav-${route[1]}`)
      }
    }
  }

  return found
}

describe('tour anchors', () => {
  const available = anchorsInSource()

  it('finds anchors in the source at all', () => {
    // Guards the scanner itself: a regex that matched nothing would make every assertion
    // below vacuous, which is how this kind of test quietly stops testing.
    expect(available.size).toBeGreaterThan(10)
    expect(available).toContain('sidebar')
  })

  it('every anchor a tour points at exists', () => {
    const missing: string[] = []

    for (const tour of allTours) {
      for (const step of tour.steps) {
        if (step.anchor && !available.has(step.anchor)) {
          missing.push(`${tour.id}/${step.id} -> ${step.anchor}`)
        }
      }
    }

    expect(missing, `anchors named by a tour but absent from the app:\n${missing.join('\n')}`)
      .toEqual([])
  })
})

describe('tour routes', () => {
  const routes = readFileSync(join(SRC, 'app', 'routes.tsx'), 'utf8')

  /** Route paths declared in the router, e.g. '/playground', '/experiments/:experimentId'. */
  const declared = [...routes.matchAll(/path="([^"]+)"/g)].map((match) => match[1])

  it('reads the router', () => {
    expect(declared.length).toBeGreaterThan(5)
    expect(declared).toContain('/playground')
  })

  it('every route a step navigates to is real and needs no id', () => {
    // A route with a parameter cannot be navigated to blind, and the router's catch-all
    // silently redirects to /playground — so a bad route does not error, it just dumps the
    // reader somewhere else mid-tour, which is the worst way for this to fail.
    const bad: string[] = []

    for (const tour of allTours) {
      for (const step of tour.steps) {
        if (!step.route) continue

        if (!declared.includes(step.route)) bad.push(`${tour.id}/${step.id} -> unknown ${step.route}`)
        if (step.route.includes(':')) bad.push(`${tour.id}/${step.id} -> needs an id: ${step.route}`)
      }
    }

    expect(bad, bad.join('\n')).toEqual([])
  })

  it('every page tour is anchored to a real, id-free route', () => {
    const bad: string[] = []

    for (const tour of allTours.filter((candidate) => candidate.kind === 'page')) {
      if (!tour.area) bad.push(`${tour.id} has no area`)
      else if (!declared.includes(tour.area)) bad.push(`${tour.id} -> unknown area ${tour.area}`)
      else if (tour.area.includes(':')) bad.push(`${tour.id} -> area needs an id`)
    }

    expect(bad, bad.join('\n')).toEqual([])
  })

  it('gives every sidebar destination a tour', () => {
    // The ask was "one for each of the tabs". This fails when a nav item is added without one.
    const sidebar = readFileSync(join(SRC, 'components', 'layout', 'Sidebar.tsx'), 'utf8')
    const navPaths = [...sidebar.matchAll(/path: '(\/[a-z-]+)'/g)].map((match) => match[1])

    const toured = new Set(
      allTours.filter((tour) => tour.kind === 'page').map((tour) => tour.area)
    )

    expect(navPaths.length).toBeGreaterThan(10)
    expect(navPaths.filter((path) => !toured.has(path))).toEqual([])
  })
})
