export type ConnectionType = 'Wireless' | 'Wired'

export interface NetworkClient {
  mac: string
  name: string
  hostname: string | null
  ip: string | null
  connection: ConnectionType
  online: boolean
  ssid: string | null
  apName: string | null
  band: string | null
  rssi: number | null
  rxBytes: number | null
  txBytes: number | null
  connectedSince: string | null
  lastSeen: string
  isStaticLease: boolean | null
  sources: string[]
}

export interface SourceStatus {
  ok: boolean
  lastSuccess: string | null
  error: string | null
}

export interface ClientSnapshot {
  generatedAt: string
  sources: Record<string, SourceStatus>
  clients: NetworkClient[]
}
