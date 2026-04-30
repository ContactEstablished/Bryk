const BASE_URL: string =
  import.meta.env.VITE_API_BASE_URL ?? 'https://localhost:60129/api/v1'

export class ApiError extends Error {
  readonly status: number
  readonly statusText: string
  readonly body: unknown

  constructor(status: number, statusText: string, body: unknown) {
    super(`${status} ${statusText}`)
    this.name = 'ApiError'
    this.status = status
    this.statusText = statusText
    this.body = body
  }
}

export async function apiFetch<T>(
  path: string,
  init?: RequestInit,
): Promise<T | null> {
  const url = `${BASE_URL}${path}`

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...init?.headers,
  }

  const response = await fetch(url, { ...init, headers })

  if (response.status === 204) {
    return null
  }

  if (!response.ok) {
    let body: unknown
    try {
      body = await response.json()
    } catch {
      body = null
    }
    throw new ApiError(response.status, response.statusText, body)
  }

  return (await response.json()) as T
}
