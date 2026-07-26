import { afterEach, describe, expect, it, vi } from 'vitest'
import { apiFetch, ApiError } from '@/services/api'

function jsonResponse(body: unknown, init: { status?: number } = {}): Response {
  return new Response(JSON.stringify(body), {
    status: init.status ?? 200,
    headers: { 'Content-Type': 'application/json' },
  })
}

function headersOf(init: RequestInit | undefined): Record<string, string> {
  return (init?.headers ?? {}) as Record<string, string>
}

describe('apiFetch — Content-Type handling', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('sets application/json for a plain object body', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', { method: 'POST', body: '{}' })

    expect(headersOf(fetchSpy.mock.calls[0][1])['Content-Type']).toBe('application/json')
  })

  it('omits Content-Type entirely for a FormData body', async () => {
    // The regression guard for the multipart blocker: an explicit Content-Type prevents the browser
    // from supplying its own 'multipart/form-data; boundary=…', and the server cannot find the parts.
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', { method: 'POST', body: new FormData() })

    expect('Content-Type' in headersOf(fetchSpy.mock.calls[0][1])).toBe(false)
  })

  it('still honours an explicitly passed header alongside FormData', async () => {
    const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(jsonResponse({ ok: true }))

    await apiFetch('/x', {
      method: 'POST',
      body: new FormData(),
      headers: { 'X-Trace': 'abc' },
    })

    expect(headersOf(fetchSpy.mock.calls[0][1])['X-Trace']).toBe('abc')
  })

  it('returns null for a 204', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(null, { status: 204 }))

    await expect(apiFetch('/x', { method: 'DELETE' })).resolves.toBeNull()
  })

  it('throws ApiError with the parsed body for a 400', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(
      jsonResponse({ status: 400, errors: ['File: nope'] }, { status: 400 }),
    )

    await expect(apiFetch('/x')).rejects.toBeInstanceOf(ApiError)
  })
})
