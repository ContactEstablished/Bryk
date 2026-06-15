// Shared display formatters for analytics surfaces (mirrors the per-view helpers in the workout views).

// Seconds → `h:mm:ss` (or `m:ss` under an hour).
export function formatDuration(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = Math.floor(totalSeconds % 60)
  return h > 0
    ? `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
    : `${m}:${String(s).padStart(2, '0')}`
}

// Metres → `N.N km` (or `N m` under a km).
export function formatDistance(meters: number): string {
  return meters >= 1000 ? `${(meters / 1000).toFixed(1)} km` : `${Math.round(meters)} m`
}

// Sec per unit → `m:ss` (the unit suffix — /km, /100m — is added by the caller).
export function formatPace(secPerUnit: number): string {
  const m = Math.floor(secPerUnit / 60)
  const s = Math.floor(secPerUnit % 60)
  return `${m}:${String(s).padStart(2, '0')}`
}

// 'YYYY-MM-DD' → 'Mon D' without a timezone shift.
export function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })
}

// Hours-and-minutes summary of a seconds total, e.g. `1h 12m`, `12m`, `0m`.
export function formatHm(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.round((totalSeconds % 3600) / 60)
  return h > 0 ? `${h}h ${m}m` : `${m}m`
}
