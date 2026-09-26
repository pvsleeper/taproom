export function formatBytes(bytes: number | null): string {
  if (bytes === null) return '—'
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB', 'TB']
  let value = bytes / 1024
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unitIndex]}`
}

export function formatUptime(connectedSince: string | null): string {
  if (!connectedSince) return '—'
  const start = new Date(connectedSince).getTime()
  if (Number.isNaN(start)) return '—'
  const totalSeconds = Math.max(0, Math.floor((Date.now() - start) / 1000))

  const days = Math.floor(totalSeconds / 86400)
  const hours = Math.floor((totalSeconds % 86400) / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)

  if (days > 0) return `${days}d ${hours}h`
  if (hours > 0) return `${hours}h ${minutes}m`
  return `${minutes}m`
}

export function formatRelativeSeconds(from: string | null | undefined): string {
  if (!from) return '—'
  const then = new Date(from).getTime()
  if (Number.isNaN(then)) return '—'
  const seconds = Math.max(0, Math.floor((Date.now() - then) / 1000))
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  return `${hours}h ago`
}

export type SignalLevel = 'good' | 'fair' | 'poor'

export function signalLevel(rssi: number | null): SignalLevel | null {
  if (rssi === null) return null
  if (rssi >= -60) return 'good'
  if (rssi >= -70) return 'fair'
  return 'poor'
}

export function normalizeMacQuery(value: string): string {
  return value.replace(/[^a-zA-Z0-9]/g, '').toLowerCase()
}
