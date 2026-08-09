import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => {
  cleanup()
})

// jsdom implements no layout, so Element.scrollIntoView does not exist. Components that scroll
// to a ref are otherwise untestable — they throw before rendering anything. Stubbed here rather
// than guarded in each component, because the absence is a property of the test environment and
// not something the application should have to know about.
if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = () => {}
}
