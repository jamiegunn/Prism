import { ApiError } from './apiClient'

/**
 * Turns a thrown mutation error into something worth showing a person.
 *
 * Kept separate from the provider so the wording can be tested directly, and because a
 * component file that also exports a helper trips the fast-refresh lint rule.
 */

/**
 * Describes a failed write in terms of what the reader can do about it.
 *
 * @param error Whatever the mutation threw.
 * @returns A single sentence for a toast.
 */
export function describeMutationError(error: unknown): string {
  if (error instanceof ApiError) {
    // 4xx is nearly always something in the request; 5xx is not the reader's fault and
    // saying "check your input" would send them looking in the wrong place.
    if (error.status === 404) {
      return `${error.message} (404 — it may have been deleted already)`
    }

    if (error.status === 409) {
      return `${error.message} (409 — something else changed it first)`
    }

    // 503 is this API's "a dependency did not answer" — an inference server that is down, or one
    // that refused the model asked of it. The detail names which, and it is the whole diagnosis,
    // so swallowing it into "check the API log" sends the reader to a log to re-read a sentence
    // they were already holding.
    if (error.status === 503) {
      return error.message
    }

    if (error.status >= 500) {
      return `The server failed on that request (${error.status}). Check the API log.`
    }

    return error.message
  }

  // fetch rejects rather than resolving when it cannot reach the host at all, which is the
  // common case while the API is restarting and reads very differently from a 500.
  if (error instanceof TypeError) {
    return 'Could not reach the Prism API. Is it still running?'
  }

  if (error instanceof Error && error.message.length > 0) {
    return error.message
  }

  return 'That did not work, and the error gave no reason.'
}
