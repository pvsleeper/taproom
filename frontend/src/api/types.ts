export type ConnectionType = 'Wireless' | 'Wired' | 'Unknown'

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

export type NameSource = 'Dns' | 'Ptr' | 'Asn' | 'Ip'

export interface EnrichedConnection {
  remoteIp: string
  remotePort: number
  protocol: string
  name: string
  nameSource: NameSource
  otherNames: string[]
  country: string | null
  city: string | null
  lat: number | null
  lon: number | null
  asn: number | null
  org: string | null
  bytes: number
  packets: number
  ageSeconds: number
  state: string
  downBps: number | null
  upBps: number | null
  /** Only populated on the dashboard's network-wide connection list. */
  mac: string | null
  deviceName: string | null
}

export interface MarkerDeviceBreakdown {
  mac: string
  name: string
  connectionCount: number
  downBps: number
  upBps: number
}

export interface ConnectionMarker {
  key: string
  lat: number
  lon: number
  label: string
  connectionCount: number
  bytes: number
  downBps: number
  upBps: number
  /** Only populated on the dashboard's network-wide markers. */
  devices: MarkerDeviceBreakdown[]
}

export interface LocalConnection {
  remoteIp: string
  remotePort: number
  protocol: string
  name: string
}

export interface HomeLocation {
  lat: number
  lon: number
}

export interface ConnectionsResult {
  mac: string
  ip: string | null
  generatedAt: string
  home: HomeLocation
  connections: EnrichedConnection[]
  markers: ConnectionMarker[]
  local: LocalConnection[]
  unknownLocation: EnrichedConnection[]
  hints: string[]
  sources: Record<string, SourceStatus>
}

export interface TopDomain {
  domain: string
  count: number
  blocked: boolean
}

export interface TopBlockedDomain {
  domain: string
  count: number
  blocklist: string
}

export interface RecentQuery {
  time: string
  domain: string
  type: string
  action: 'pass' | 'block'
  failed: boolean
  rcode: string
}

export interface DnsResult {
  windowMinutes: number
  totals: { queries: number; blocked: number; failed: number }
  topDomains: TopDomain[]
  topBlocked: TopBlockedDomain[]
  recent: RecentQuery[]
  source: SourceStatus
}

export interface BandwidthResult {
  mac: string
  sampledAt: string
  downBps: number
  upBps: number
  addresses: string[]
  source: SourceStatus
}

export interface ClientCounts {
  online: number
  wireless: number
  wired: number
}

export interface DashboardDnsSummary {
  queries: number
  blocked: number
  failed: number
}

export interface DashboardSummaryResult {
  generatedAt: string
  clients: ClientCounts
  dns: DashboardDnsSummary
  sources: Record<string, SourceStatus>
}

export interface DashboardWanResult {
  sampledAt: string
  downBps: number
  upBps: number
  ok: boolean
}

export interface DashboardConnectionsResult {
  generatedAt: string
  home: HomeLocation
  connections: EnrichedConnection[]
  markers: ConnectionMarker[]
  sources: Record<string, SourceStatus>
}

export interface DeviceRate {
  mac: string
  name: string
  downBps: number
  upBps: number
}

export interface TopTalkersResult {
  sampledAt: string
  devices: DeviceRate[]
  ok: boolean
}
