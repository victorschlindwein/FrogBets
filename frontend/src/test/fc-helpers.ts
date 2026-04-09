/**
 * fast-check arbitraries safe for use with Testing Library.
 *
 * Testing Library normalizes whitespace by default (trims leading/trailing
 * spaces, collapses internal whitespace). Any fc.string() used in a test that
 * calls getByText / getAllByText must not produce strings that differ from
 * their trimmed version, otherwise the matcher won't find the element.
 *
 * Use these helpers instead of raw fc.string() whenever the generated value
 * will be rendered in the DOM and queried with Testing Library.
 */
import * as fc from 'fast-check'

/** A non-empty string with no leading/trailing whitespace. */
export const safeString = (opts: { minLength?: number; maxLength?: number } = {}) =>
  fc
    .string({ minLength: opts.minLength ?? 1, maxLength: opts.maxLength ?? 20 })
    .filter(s => s.trim().length > 0 && s === s.trim())

/** A UUID string. */
export const safeId = () => fc.uuid()
