import { describe, it, expect, beforeEach, vi } from 'vitest'
import { setToken, getToken } from './client'

describe('API client — token management', () => {
  beforeEach(() => {
    setToken(null)
  })

  it('stores token in memory', () => {
    setToken('my-jwt-token')
    expect(getToken()).toBe('my-jwt-token')
  })

  it('clears token when set to null', () => {
    setToken('some-token')
    setToken(null)
    expect(getToken()).toBeNull()
  })

  it('starts with no token', () => {
    expect(getToken()).toBeNull()
  })
})

describe('API client — Authorization interceptor', () => {
  beforeEach(() => {
    setToken(null)
  })

  it('does not expose token via localStorage', () => {
    setToken('secret-token')
    // Token must NOT be in localStorage
    expect(localStorage.getItem('token')).toBeNull()
    expect(localStorage.getItem('accessToken')).toBeNull()
    expect(localStorage.getItem('jwt')).toBeNull()
  })
})
